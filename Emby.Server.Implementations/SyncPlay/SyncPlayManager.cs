#nullable disable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Jellyfin.Database.Implementations.Enums;
using Jellyfin.Extensions;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Session;
using MediaBrowser.Controller.SyncPlay;
using MediaBrowser.Controller.SyncPlay.Requests;
using MediaBrowser.Model.Session;
using MediaBrowser.Model.SyncPlay;
using Microsoft.Extensions.Logging;

namespace Emby.Server.Implementations.SyncPlay
{
    /// <summary>
    /// Class SyncPlayManager.
    /// </summary>
    public class SyncPlayManager : ISyncPlayManager, IDisposable
    {
        /// <summary>
        /// The logger.
        /// </summary>
        private readonly ILogger<SyncPlayManager> _logger;

        /// <summary>
        /// The logger factory.
        /// </summary>
        private readonly ILoggerFactory _loggerFactory;

        /// <summary>
        /// The user manager.
        /// </summary>
        private readonly IUserManager _userManager;

        /// <summary>
        /// The session manager.
        /// </summary>
        private readonly ISessionManager _sessionManager;

        /// <summary>
        /// The library manager.
        /// </summary>
        private readonly ILibraryManager _libraryManager;

        /// <summary>
        /// The map between users and counter of active sessions.
        /// </summary>
        private readonly ConcurrentDictionary<Guid, int> _activeUsers =
            new ConcurrentDictionary<Guid, int>();

        /// <summary>
        /// The map between sessions and groups.
        /// </summary>
        private readonly ConcurrentDictionary<string, Group> _sessionToGroupMap =
            new ConcurrentDictionary<string, Group>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// The groups.
        /// </summary>
        private readonly ConcurrentDictionary<Guid, Group> _groups =
            new ConcurrentDictionary<Guid, Group>();

        /// <summary>
        /// Lock used for accessing multiple groups at once.
        /// </summary>
        /// <remarks>
        /// This lock has priority on locks made on <see cref="Group"/>.
        /// </remarks>
        private readonly Lock _groupsLock = new();

        private bool _disposed = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="SyncPlayManager" /> class.
        /// </summary>
        /// <param name="loggerFactory">The logger factory.</param>
        /// <param name="userManager">The user manager.</param>
        /// <param name="sessionManager">The session manager.</param>
        /// <param name="libraryManager">The library manager.</param>
        public SyncPlayManager(
            ILoggerFactory loggerFactory,
            IUserManager userManager,
            ISessionManager sessionManager,
            ILibraryManager libraryManager)
        {
            _loggerFactory = loggerFactory;
            _userManager = userManager;
            _sessionManager = sessionManager;
            _libraryManager = libraryManager;
            _logger = loggerFactory.CreateLogger<SyncPlayManager>();
            _sessionManager.SessionEnded += OnSessionEnded;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <inheritdoc />
        public GroupInfoDto NewGroup(SessionInfo session, NewGroupRequest request, CancellationToken cancellationToken)
        {
            if (session is null)
            {
                throw new InvalidOperationException("Session is null!");
            }

            if (request is null)
            {
                throw new InvalidOperationException("Request is null!");
            }

            // Locking required to access list of groups.
            lock (_groupsLock)
            {
                // Make sure that session has not joined another group.
                if (_sessionToGroupMap.ContainsKey(session.Id))
                {
                    var leaveGroupRequest = new LeaveGroupRequest();
                    LeaveGroup(session, leaveGroupRequest, cancellationToken);
                }

                var group = new Group(_loggerFactory, _userManager, _sessionManager, _libraryManager);
                _groups[group.GroupId] = group;

                if (!_sessionToGroupMap.TryAdd(session.Id, group))
                {
                    throw new InvalidOperationException("Could not add session to group!");
                }

                UpdateSessionsCounter(session.UserId, 1);
                group.CreateGroup(session, request, cancellationToken);
                return group.GetInfo();
            }
        }

        /// <inheritdoc />
        public void JoinGroup(SessionInfo session, JoinGroupRequest request, CancellationToken cancellationToken)
        {
            if (session is null)
            {
                throw new InvalidOperationException("Session is null!");
            }

            if (request is null)
            {
                throw new InvalidOperationException("Request is null!");
            }

            var user = _userManager.GetUserById(session.UserId);

            // Locking required to access list of groups.
            lock (_groupsLock)
            {
                _groups.TryGetValue(request.GroupId, out Group group);

                if (group is null)
                {
                    _logger.LogWarning("Session {SessionId} tried to join group {GroupId} that does not exist.", session.Id, request.GroupId);

                    var error = new SyncPlayGroupDoesNotExistUpdate(Guid.Empty, string.Empty);
                    _sessionManager.SendSyncPlayGroupUpdate(session.Id, error, CancellationToken.None);
                    return;
                }

                // Group lock required to let other requests end first.
                lock (group)
                {
                    // Gusfin extension: private groups require an invite. Checked before the
                    // library-access guard because that error carries the real group id, and
                    // must deny existence so private groups cannot be enumerated by probing ids.
                    if (!group.CanJoin(session.UserId))
                    {
                        _logger.LogWarning("Session {SessionId} tried to join private group {GroupId} without a valid invite.", session.Id, group.GroupId.ToString());

                        if (group.HasLapsedInvite(session.UserId))
                        {
                            // The user demonstrably knew about the group; report the lapse honestly.
                            var lapsed = new SyncPlayGroupInviteRequiredUpdate(group.GroupId, group.GroupName);
                            _sessionManager.SendSyncPlayGroupUpdate(session.Id, lapsed, CancellationToken.None);
                        }
                        else
                        {
                            var error = new SyncPlayGroupDoesNotExistUpdate(Guid.Empty, string.Empty);
                            _sessionManager.SendSyncPlayGroupUpdate(session.Id, error, CancellationToken.None);
                        }

                        return;
                    }

                    if (!group.HasAccessToPlayQueue(user))
                    {
                        _logger.LogWarning("Session {SessionId} tried to join group {GroupId} but does not have access to some content of the playing queue.", session.Id, group.GroupId.ToString());

                        var error = new SyncPlayLibraryAccessDeniedUpdate(group.GroupId, string.Empty);
                        _sessionManager.SendSyncPlayGroupUpdate(session.Id, error, CancellationToken.None);
                        return;
                    }

                    if (_sessionToGroupMap.TryGetValue(session.Id, out var existingGroup))
                    {
                        if (existingGroup.GroupId.Equals(request.GroupId))
                        {
                            // Restore session.
                            UpdateSessionsCounter(session.UserId, 1);
                            group.SessionJoin(session, request, cancellationToken);
                            return;
                        }

                        var leaveGroupRequest = new LeaveGroupRequest();
                        LeaveGroup(session, leaveGroupRequest, cancellationToken);
                    }

                    if (!_sessionToGroupMap.TryAdd(session.Id, group))
                    {
                        throw new InvalidOperationException("Could not add session to group!");
                    }

                    UpdateSessionsCounter(session.UserId, 1);
                    group.SessionJoin(session, request, cancellationToken);

                    // Gusfin extension: close any duplicate invite modal on the user's other devices.
                    NotifyInviteCancelled(new List<Guid> { session.UserId }, group.GroupId, GroupInviteCancelReason.AcceptedElsewhere, cancellationToken);
                }
            }
        }

        /// <inheritdoc />
        public void LeaveGroup(SessionInfo session, LeaveGroupRequest request, CancellationToken cancellationToken)
        {
            if (session is null)
            {
                throw new InvalidOperationException("Session is null!");
            }

            if (request is null)
            {
                throw new InvalidOperationException("Request is null!");
            }

            // Locking required to access list of groups.
            lock (_groupsLock)
            {
                if (_sessionToGroupMap.TryGetValue(session.Id, out var group))
                {
                    // Group lock required to let other requests end first.
                    lock (group)
                    {
                        if (_sessionToGroupMap.TryRemove(session.Id, out var tempGroup))
                        {
                            if (!tempGroup.GroupId.Equals(group.GroupId))
                            {
                                throw new InvalidOperationException("Session was in wrong group!");
                            }
                        }
                        else
                        {
                            throw new InvalidOperationException("Could not remove session from group!");
                        }

                        UpdateSessionsCounter(session.UserId, -1);
                        group.SessionLeave(session, request, cancellationToken);

                        if (group.IsGroupEmpty())
                        {
                            _logger.LogInformation("Group {GroupId} is empty, removing it.", group.GroupId);

                            // Gusfin extension: dismiss the invite modal of anyone still deciding.
                            var orphanedInvitees = group.GetPendingInviteeIds();
                            _groups.Remove(group.GroupId, out _);
                            if (orphanedInvitees.Count > 0)
                            {
                                NotifyInviteCancelled(orphanedInvitees, group.GroupId, GroupInviteCancelReason.GroupClosed, CancellationToken.None);
                            }
                        }
                    }
                }
                else
                {
                    _logger.LogWarning("Session {SessionId} does not belong to any group.", session.Id);

                    var error = new SyncPlayNotInGroupUpdate(Guid.Empty, string.Empty);
                    _sessionManager.SendSyncPlayGroupUpdate(session.Id, error, CancellationToken.None);
                }
            }
        }

        /// <inheritdoc />
        public List<GroupInfoDto> ListGroups(SessionInfo session, ListGroupsRequest request)
        {
            if (session is null)
            {
                throw new InvalidOperationException("Session is null!");
            }

            if (request is null)
            {
                throw new InvalidOperationException("Request is null!");
            }

            var user = _userManager.GetUserById(session.UserId);
            List<GroupInfoDto> list = new List<GroupInfoDto>();

            lock (_groupsLock)
            {
                foreach (var (_, group) in _groups)
                {
                    // Locking required as group is not thread-safe.
                    lock (group)
                    {
                        if (group.HasAccessToPlayQueue(user) && group.IsVisibleTo(session.UserId))
                        {
                            list.Add(group.GetInfo());
                        }
                    }
                }
            }

            return list;
        }

        /// <inheritdoc />
        public GroupInfoDto GetGroup(SessionInfo session, Guid groupId)
        {
            ArgumentNullException.ThrowIfNull(session);

            var user = _userManager.GetUserById(session.UserId);

            lock (_groupsLock)
            {
                foreach (var (_, group) in _groups)
                {
                    // Locking required as group is not thread-safe.
                    lock (group)
                    {
                        if (group.GroupId.Equals(groupId) && group.HasAccessToPlayQueue(user) && group.IsVisibleTo(session.UserId))
                        {
                            return group.GetInfo();
                        }
                    }
                }
            }

            return null;
        }

        /// <inheritdoc />
        public void HandleRequest(SessionInfo session, IGroupPlaybackRequest request, CancellationToken cancellationToken)
        {
            if (session is null)
            {
                throw new InvalidOperationException("Session is null!");
            }

            if (request is null)
            {
                throw new InvalidOperationException("Request is null!");
            }

            if (_sessionToGroupMap.TryGetValue(session.Id, out var group))
            {
                // Group lock required as Group is not thread-safe.
                lock (group)
                {
                    // Make sure that session still belongs to this group.
                    if (_sessionToGroupMap.TryGetValue(session.Id, out var checkGroup) && !checkGroup.GroupId.Equals(group.GroupId))
                    {
                        // Drop request.
                        return;
                    }

                    // Drop request if group is empty.
                    if (group.IsGroupEmpty())
                    {
                        return;
                    }

                    // Apply requested changes to group.
                    group.HandleRequest(session, request, cancellationToken);
                }
            }
            else
            {
                _logger.LogWarning("Session {SessionId} does not belong to any group.", session.Id);

                var error = new SyncPlayNotInGroupUpdate(Guid.Empty, string.Empty);
                _sessionManager.SendSyncPlayGroupUpdate(session.Id, error, CancellationToken.None);
            }
        }

        /// <inheritdoc />
        public bool IsUserActive(Guid userId)
        {
            if (_activeUsers.TryGetValue(userId, out var sessionsCounter))
            {
                return sessionsCounter > 0;
            }

            return false;
        }

        // Gusfin extension: private groups & invites.

        /// <inheritdoc />
        public void InviteToGroup(SessionInfo session, IReadOnlyList<Guid> userIds, CancellationToken cancellationToken)
        {
            if (session is null)
            {
                throw new InvalidOperationException("Session is null!");
            }

            if (userIds is null)
            {
                throw new InvalidOperationException("User ids are null!");
            }

            // The group is resolved from the caller's own session, never from a client-supplied
            // group id: this is the actual per-group authorization, since the SyncPlayIsInGroup
            // policy only checks that the user is active in some group.
            if (!_sessionToGroupMap.TryGetValue(session.Id, out var group))
            {
                _logger.LogWarning("Session {SessionId} does not belong to any group.", session.Id);

                var error = new SyncPlayNotInGroupUpdate(Guid.Empty, string.Empty);
                _sessionManager.SendSyncPlayGroupUpdate(session.Id, error, CancellationToken.None);
                return;
            }

            // Group lock required as Group is not thread-safe.
            lock (group)
            {
                // Drop request if group is being torn down.
                if (group.IsGroupEmpty())
                {
                    return;
                }

                var invited = new HashSet<Guid>();
                foreach (var userId in userIds)
                {
                    if (userId.IsEmpty() || userId.Equals(session.UserId) || !invited.Add(userId))
                    {
                        continue;
                    }

                    var user = _userManager.GetUserById(userId);
                    if (user is null || user.SyncPlayAccess == SyncPlayUserAccessType.None)
                    {
                        continue;
                    }

                    // Never invite someone the join guard would bounce.
                    if (group.HasMember(userId) || !group.HasAccessToPlayQueue(user))
                    {
                        continue;
                    }

                    var info = group.AddOrRefreshInvite(session, userId);
                    if (info is null)
                    {
                        continue;
                    }

                    _sessionManager.SendMessageToUserSessions(
                        new List<Guid> { userId },
                        SessionMessageType.SyncPlayGroupUpdate,
                        new SyncPlayGroupInviteUpdate(group.GroupId, info),
                        cancellationToken);

                    _logger.LogInformation("Session {SessionId} invited user {UserId} to group {GroupId}.", session.Id, userId, group.GroupId.ToString());
                }
            }
        }

        /// <inheritdoc />
        public void DeclineInvite(SessionInfo session, Guid groupId, CancellationToken cancellationToken)
        {
            if (session is null)
            {
                throw new InvalidOperationException("Session is null!");
            }

            // Locking required to access list of groups.
            lock (_groupsLock)
            {
                if (!_groups.TryGetValue(groupId, out var group))
                {
                    // Group already gone; the client has already dismissed the invite.
                    return;
                }

                // Group lock required as Group is not thread-safe.
                lock (group)
                {
                    var inviterUserId = group.GetInviterUserId(session.UserId);
                    if (!group.MarkInviteDeclined(session.UserId))
                    {
                        return;
                    }

                    _logger.LogInformation("User {UserId} declined the invite to group {GroupId}.", session.UserId, group.GroupId.ToString());

                    if (inviterUserId.HasValue && group.HasMember(inviterUserId.Value))
                    {
                        _sessionManager.SendMessageToUserSessions(
                            new List<Guid> { inviterUserId.Value },
                            SessionMessageType.SyncPlayGroupUpdate,
                            new SyncPlayGroupInviteDeclinedUpdate(group.GroupId, session.UserName),
                            cancellationToken);
                    }

                    // Close the invite modal on all of the decliner's devices.
                    NotifyInviteCancelled(new List<Guid> { session.UserId }, group.GroupId, GroupInviteCancelReason.DeclinedElsewhere, cancellationToken);
                }
            }
        }

        /// <inheritdoc />
        public IReadOnlyList<SyncPlayInviteCandidateDto> GetInviteCandidates(SessionInfo session)
        {
            if (session is null)
            {
                throw new InvalidOperationException("Session is null!");
            }

            if (!_sessionToGroupMap.TryGetValue(session.Id, out var group))
            {
                // Only members of a group may see who is online.
                return Array.Empty<SyncPlayInviteCandidateDto>();
            }

            // Snapshot sessions and resolve users outside the group lock,
            // so playback requests are not queued behind repository calls.
            var cutoff = DateTime.UtcNow.AddMinutes(-5);
            var candidateUsers = _sessionManager.Sessions
                .Where(s => !s.UserId.IsEmpty() && s.LastActivityDate >= cutoff)
                .Select(s => s.UserId)
                .Distinct()
                .Where(id => !id.Equals(session.UserId))
                .Select(id => _userManager.GetUserById(id))
                .Where(user => user is not null && user.SyncPlayAccess != SyncPlayUserAccessType.None)
                .ToList();

            var candidates = new List<SyncPlayInviteCandidateDto>();

            // Group lock required as Group is not thread-safe.
            lock (group)
            {
                if (group.IsGroupEmpty())
                {
                    return Array.Empty<SyncPlayInviteCandidateDto>();
                }

                foreach (var user in candidateUsers)
                {
                    if (group.HasMember(user.Id) || !group.HasAccessToPlayQueue(user))
                    {
                        continue;
                    }

                    candidates.Add(new SyncPlayInviteCandidateDto(user.Id, user.Username, group.IsInvitePending(user.Id)));
                }
            }

            return candidates
                .OrderBy(candidate => candidate.UserName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <inheritdoc />
        public IReadOnlyList<GroupInviteInfo> ListInvites(SessionInfo session)
        {
            if (session is null)
            {
                throw new InvalidOperationException("Session is null!");
            }

            var invites = new List<GroupInviteInfo>();

            // Locking required to access list of groups.
            lock (_groupsLock)
            {
                foreach (var (_, group) in _groups)
                {
                    // Locking required as group is not thread-safe.
                    lock (group)
                    {
                        var info = group.GetPendingInviteInfo(session.UserId);
                        if (info is not null)
                        {
                            invites.Add(info);
                        }
                    }
                }
            }

            return invites;
        }

        /// <summary>
        /// Notifies all sessions of the given users that an invite is no longer actionable. Gusfin extension.
        /// </summary>
        /// <param name="userIds">The user identifiers to notify.</param>
        /// <param name="groupId">The group identifier.</param>
        /// <param name="reason">The reason the invite was cancelled.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        private void NotifyInviteCancelled(IReadOnlyList<Guid> userIds, Guid groupId, GroupInviteCancelReason reason, CancellationToken cancellationToken)
        {
            _sessionManager.SendMessageToUserSessions(
                userIds.ToList(),
                SessionMessageType.SyncPlayGroupUpdate,
                new SyncPlayGroupInviteCancelledUpdate(groupId, new GroupInviteCancelledInfo(groupId, reason)),
                cancellationToken);
        }

        /// <summary>
        /// Releases unmanaged and optionally managed resources.
        /// </summary>
        /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            _sessionManager.SessionEnded -= OnSessionEnded;
            _disposed = true;
        }

        private void OnSessionEnded(object sender, SessionEventArgs e)
        {
            var session = e.SessionInfo;

            if (_sessionToGroupMap.TryGetValue(session.Id, out _))
            {
                var leaveGroupRequest = new LeaveGroupRequest();
                LeaveGroup(session, leaveGroupRequest, CancellationToken.None);
            }
        }

        private void UpdateSessionsCounter(Guid userId, int toAdd)
        {
            // Update sessions counter.
            var newSessionsCounter = _activeUsers.AddOrUpdate(
                userId,
                1,
                (_, sessionsCounter) => sessionsCounter + toAdd);

            // Should never happen.
            if (newSessionsCounter < 0)
            {
                throw new InvalidOperationException("Sessions counter is negative!");
            }

            // Clean record if user has no more active sessions.
            if (newSessionsCounter == 0)
            {
                _activeUsers.TryRemove(new KeyValuePair<Guid, int>(userId, newSessionsCounter));
            }
        }
    }
}
