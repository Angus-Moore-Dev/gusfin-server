#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Extensions;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Session;
using MediaBrowser.Controller.SyncPlay;
using MediaBrowser.Controller.SyncPlay.GroupStates;
using MediaBrowser.Controller.SyncPlay.PlaybackRequests;
using MediaBrowser.Controller.SyncPlay.Queue;
using MediaBrowser.Controller.SyncPlay.Requests;
using MediaBrowser.Model.SyncPlay;
using Microsoft.Extensions.Logging;

namespace Emby.Server.Implementations.SyncPlay
{
    /// <summary>
    /// Class Group.
    /// </summary>
    /// <remarks>
    /// Class is not thread-safe, external locking is required when accessing methods.
    /// </remarks>
    public class Group : IGroupStateContext
    {
        /// <summary>
        /// The logger.
        /// </summary>
        private readonly ILogger<Group> _logger;

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
        /// The participants, or members of the group.
        /// </summary>
        private readonly Dictionary<string, GroupMember> _participants =
            new Dictionary<string, GroupMember>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// The invites to the group, keyed by invitee user identifier. Gusfin extension.
        /// </summary>
        private readonly Dictionary<Guid, GroupInvite> _invites = new Dictionary<Guid, GroupInvite>();

        /// <summary>
        /// The internal group state.
        /// </summary>
        private IGroupState _state;

        /// <summary>
        /// The minimum interval between diagnostics broadcasts, in milliseconds. Gusfin extension.
        /// </summary>
        private const long DiagnosticsBroadcastIntervalMs = 1000;

        /// <summary>
        /// The time of the last diagnostics broadcast. Gusfin extension.
        /// </summary>
        private DateTime _lastDiagnosticsBroadcast = DateTime.MinValue;

        /// <summary>
        /// Gets or sets the lifetime of a pending invite, in seconds. Gusfin extension.
        /// </summary>
        /// <remarks>
        /// Settable internally so tests can exercise expiry.
        /// </remarks>
        internal int InviteLifetimeSeconds { get; set; } = 300;

        /// <summary>
        /// Initializes a new instance of the <see cref="Group" /> class.
        /// </summary>
        /// <param name="loggerFactory">The logger factory.</param>
        /// <param name="userManager">The user manager.</param>
        /// <param name="sessionManager">The session manager.</param>
        /// <param name="libraryManager">The library manager.</param>
        public Group(
            ILoggerFactory loggerFactory,
            IUserManager userManager,
            ISessionManager sessionManager,
            ILibraryManager libraryManager)
        {
            _loggerFactory = loggerFactory;
            _userManager = userManager;
            _sessionManager = sessionManager;
            _libraryManager = libraryManager;
            _logger = loggerFactory.CreateLogger<Group>();

            _state = new IdleGroupState(loggerFactory);
        }

        /// <summary>
        /// Gets the default ping value used for sessions.
        /// </summary>
        /// <value>The default ping.</value>
        public long DefaultPing { get; } = 500;

        /// <summary>
        /// Gets the maximum time offset error accepted for dates reported by clients, in milliseconds.
        /// </summary>
        /// <value>The maximum time offset error.</value>
        public long TimeSyncOffset { get; } = 2000;

        /// <summary>
        /// Gets the maximum offset error accepted for position reported by clients, in milliseconds.
        /// </summary>
        /// <value>The maximum offset error.</value>
        public long MaxPlaybackOffset { get; } = 500;

        /// <summary>
        /// Gets the group identifier.
        /// </summary>
        /// <value>The group identifier.</value>
        public Guid GroupId { get; } = Guid.NewGuid();

        /// <summary>
        /// Gets the group name.
        /// </summary>
        /// <value>The group name.</value>
        public string GroupName { get; private set; }

        /// <summary>
        /// Gets the group visibility. Gusfin extension.
        /// </summary>
        /// <value>The group visibility.</value>
        public SyncPlayGroupVisibility Visibility { get; private set; }

        /// <summary>
        /// Gets the identifier of the user that created the group. Gusfin extension.
        /// </summary>
        /// <value>The creator's user identifier.</value>
        public Guid CreatedByUserId { get; private set; }

        /// <summary>
        /// Gets the group identifier.
        /// </summary>
        /// <value>The group identifier.</value>
        public PlayQueueManager PlayQueue { get; } = new PlayQueueManager();

        /// <summary>
        /// Gets the runtime ticks of current playing item.
        /// </summary>
        /// <value>The runtime ticks of current playing item.</value>
        public long RunTimeTicks { get; private set; }

        /// <summary>
        /// Gets or sets the position ticks.
        /// </summary>
        /// <value>The position ticks.</value>
        public long PositionTicks { get; set; }

        /// <summary>
        /// Gets or sets the last activity.
        /// </summary>
        /// <value>The last activity.</value>
        public DateTime LastActivity { get; set; }

        /// <summary>
        /// Adds the session to the group.
        /// </summary>
        /// <param name="session">The session.</param>
        private void AddSession(SessionInfo session)
        {
            _participants.TryAdd(
                session.Id,
                new GroupMember(session)
                {
                    Ping = DefaultPing,
                    IsBuffering = false
                });

            // Gusfin extension: record membership as an accepted invite so that past members
            // can always see and rejoin a private group (reconnects, second devices, the
            // creator leaving and coming back) for as long as the group lives.
            MarkInviteAccepted(session.UserId);
        }

        /// <summary>
        /// Removes the session from the group.
        /// </summary>
        /// <param name="session">The session.</param>
        private void RemoveSession(SessionInfo session)
        {
            _participants.Remove(session.Id);
        }

        /// <summary>
        /// Filters sessions of this group.
        /// </summary>
        /// <param name="fromId">The current session identifier.</param>
        /// <param name="type">The filtering type.</param>
        /// <returns>The list of sessions matching the filter.</returns>
        private IEnumerable<string> FilterSessions(string fromId, SyncPlayBroadcastType type)
        {
            return type switch
            {
                SyncPlayBroadcastType.CurrentSession => new string[] { fromId },
                SyncPlayBroadcastType.AllGroup => _participants
                    .Values
                    .Select(member => member.SessionId),
                SyncPlayBroadcastType.AllExceptCurrentSession => _participants
                    .Values
                    .Select(member => member.SessionId)
                    .Where(sessionId => !sessionId.Equals(fromId, StringComparison.OrdinalIgnoreCase)),
                SyncPlayBroadcastType.AllReady => _participants
                    .Values
                    .Where(member => !member.IsBuffering)
                    .Select(member => member.SessionId),
                _ => Enumerable.Empty<string>()
            };
        }

        /// <summary>
        /// Checks if a given user can access all items of a given queue, that is,
        /// the user has the required minimum parental access and has access to all required folders.
        /// </summary>
        /// <param name="user">The user.</param>
        /// <param name="queue">The queue.</param>
        /// <returns><c>true</c> if the user can access all the items in the queue, <c>false</c> otherwise.</returns>
        private bool HasAccessToQueue(User user, IReadOnlyList<Guid> queue)
        {
            // Check if queue is empty.
            if (queue is null || queue.Count == 0)
            {
                return true;
            }

            foreach (var itemId in queue)
            {
                var item = _libraryManager.GetItemById(itemId);

                if (item is null || !item.IsVisibleStandalone(user))
                {
                    return false;
                }
            }

            return true;
        }

        private bool AllUsersHaveAccessToQueue(IReadOnlyList<Guid> queue)
        {
            // Check if queue is empty.
            if (queue is null || queue.Count == 0)
            {
                return true;
            }

            // Get list of users.
            var users = _participants
                .Values
                .Select(participant => _userManager.GetUserById(participant.UserId));

            // Find problematic users.
            var usersWithNoAccess = users.Where(user => !HasAccessToQueue(user, queue));

            // All users must be able to access the queue.
            return !usersWithNoAccess.Any();
        }

        /// <summary>
        /// Checks if the group is empty.
        /// </summary>
        /// <returns><c>true</c> if the group is empty, <c>false</c> otherwise.</returns>
        public bool IsGroupEmpty() => _participants.Count == 0;

        /// <summary>
        /// Initializes the group with the session's info.
        /// </summary>
        /// <param name="session">The session.</param>
        /// <param name="request">The request.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        public void CreateGroup(SessionInfo session, NewGroupRequest request, CancellationToken cancellationToken)
        {
            GroupName = request.GroupName;
            Visibility = request.Visibility;
            CreatedByUserId = session.UserId;
            AddSession(session);

            var sessionIsPlayingAnItem = session.FullNowPlayingItem is not null;

            RestartCurrentItem();

            if (sessionIsPlayingAnItem)
            {
                var playlist = session.NowPlayingQueue.Select(item => item.Id).ToList();
                PlayQueue.Reset();
                PlayQueue.SetPlaylist(playlist);
                PlayQueue.SetPlayingItemById(session.FullNowPlayingItem.Id);
                RunTimeTicks = session.FullNowPlayingItem.RunTimeTicks ?? 0;
                PositionTicks = session.PlayState.PositionTicks ?? 0;

                // Maintain playstate.
                var waitingState = new WaitingGroupState(_loggerFactory)
                {
                    ResumePlaying = !session.PlayState.IsPaused
                };
                SetState(waitingState);
            }

            var updateSession = new SyncPlayGroupJoinedUpdate(GroupId, GetInfo());
            SendGroupUpdate(session, SyncPlayBroadcastType.CurrentSession, updateSession, cancellationToken);

            _state.SessionJoined(this, _state.Type, session, cancellationToken);

            _logger.LogInformation("Session {SessionId} created group {GroupId}.", session.Id, GroupId.ToString());
        }

        /// <summary>
        /// Adds the session to the group.
        /// </summary>
        /// <param name="session">The session.</param>
        /// <param name="request">The request.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        public void SessionJoin(SessionInfo session, JoinGroupRequest request, CancellationToken cancellationToken)
        {
            AddSession(session);

            var updateSession = new SyncPlayGroupJoinedUpdate(GroupId, GetInfo());
            SendGroupUpdate(session, SyncPlayBroadcastType.CurrentSession, updateSession, cancellationToken);

            var updateOthers = new SyncPlayUserJoinedUpdate(GroupId, session.UserName);
            SendGroupUpdate(session, SyncPlayBroadcastType.AllExceptCurrentSession, updateOthers, cancellationToken);

            _state.SessionJoined(this, _state.Type, session, cancellationToken);

            _logger.LogInformation("Session {SessionId} joined group {GroupId}.", session.Id, GroupId.ToString());
        }

        /// <summary>
        /// Removes the session from the group.
        /// </summary>
        /// <param name="session">The session.</param>
        /// <param name="request">The request.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        public void SessionLeave(SessionInfo session, LeaveGroupRequest request, CancellationToken cancellationToken)
        {
            _state.SessionLeaving(this, _state.Type, session, cancellationToken);

            RemoveSession(session);

            var updateSession = new SyncPlayGroupLeftUpdate(GroupId, GroupId.ToString());
            SendGroupUpdate(session, SyncPlayBroadcastType.CurrentSession, updateSession, cancellationToken);

            var updateOthers = new SyncPlayUserLeftUpdate(GroupId, session.UserName);
            SendGroupUpdate(session, SyncPlayBroadcastType.AllExceptCurrentSession, updateOthers, cancellationToken);

            _logger.LogInformation("Session {SessionId} left group {GroupId}.", session.Id, GroupId.ToString());
        }

        /// <summary>
        /// Handles the requested action by the session.
        /// </summary>
        /// <param name="session">The session.</param>
        /// <param name="request">The requested action.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        public void HandleRequest(SessionInfo session, IGroupPlaybackRequest request, CancellationToken cancellationToken)
        {
            // The server's job is to maintain a consistent state for clients to reference
            // and notify clients of state changes. The actual syncing of media playback
            // happens client side. Clients are aware of the server's time and use it to sync.
            _logger.LogInformation("Session {SessionId} requested {RequestType} in group {GroupId} that is {StateType}.", session.Id, request.Action, GroupId.ToString(), _state.Type);

            // Apply requested changes to this group given its current state.
            // Every request has a slightly different outcome depending on the group's state.
            // There are currently four different group states that accomplish different goals:
            // - Idle: in this state no media is playing and clients should be idle (playback is stopped).
            // - Waiting: in this state the group is waiting for all the clients to be ready to start the playback,
            //      that is, they've either finished loading the media for the first time or they've finished buffering.
            //      Once all clients report to be ready the group's state can change to Playing or Paused.
            // - Playing: clients have some media loaded and playback is unpaused.
            // - Paused: clients have some media loaded but playback is currently paused.
            request.Apply(this, _state, session, cancellationToken);
        }

        /// <summary>
        /// Gets the info about the group for the clients.
        /// </summary>
        /// <returns>The group info for the clients.</returns>
        public GroupInfoDto GetInfo()
        {
            var participants = _participants.Values.Select(session => session.UserName).Distinct().ToList();
            return new GroupInfoDto(GroupId, GroupName, _state.Type, participants, DateTime.UtcNow)
            {
                Visibility = Visibility,
                NowPlaying = GetNowPlayingInfo()
            };
        }

        /// <summary>
        /// Builds the now-playing info for the group, or <c>null</c> when nothing is playing. Gusfin extension.
        /// </summary>
        /// <returns>The now-playing info.</returns>
        private GroupNowPlayingInfo GetNowPlayingInfo()
        {
            var itemId = PlayQueue.GetPlayingItemId();
            if (itemId.IsEmpty())
            {
                return null;
            }

            var item = _libraryManager.GetItemById(itemId);
            if (item is null)
            {
                return null;
            }

            var isPlaying = _state.Type.Equals(GroupStateType.Playing);
            var positionTicks = PositionTicks;
            if (isPlaying)
            {
                // Elapsed time is negative while playback start is delayed to account
                // for latency, in which case LastActivity is in the future.
                var elapsedTime = DateTime.UtcNow - LastActivity;
                positionTicks += Math.Max(elapsedTime.Ticks, 0);
            }

            var episode = item as Episode;
            return new GroupNowPlayingInfo(
                itemId,
                item.Name,
                episode?.SeriesName,
                episode?.ParentIndexNumber,
                episode?.IndexNumber,
                SanitizePositionTicks(positionTicks),
                RunTimeTicks,
                isPlaying);
        }

        /// <summary>
        /// Checks if a user has access to all content in the play queue.
        /// </summary>
        /// <param name="user">The user.</param>
        /// <returns><c>true</c> if the user can access the play queue; <c>false</c> otherwise.</returns>
        public bool HasAccessToPlayQueue(User user)
        {
            var items = PlayQueue.GetPlaylist().Select(item => item.ItemId).ToList();
            return HasAccessToQueue(user, items);
        }

        // Gusfin extension: private groups.

        /// <summary>
        /// Gets a value indicating whether the group is public. Gusfin extension.
        /// </summary>
        /// <value><c>true</c> if the group is public; <c>false</c> otherwise.</value>
        public bool IsPublic => Visibility == SyncPlayGroupVisibility.Public;

        /// <summary>
        /// Checks whether any session of the given user is a participant of the group. Gusfin extension.
        /// </summary>
        /// <param name="userId">The user identifier.</param>
        /// <returns><c>true</c> if the user is a member of the group; <c>false</c> otherwise.</returns>
        public bool HasMember(Guid userId)
            => _participants.Values.Any(member => member.UserId.Equals(userId));

        /// <summary>
        /// Checks whether the group should be visible to the given user in listings and lookups. Gusfin extension.
        /// </summary>
        /// <param name="userId">The user identifier.</param>
        /// <returns><c>true</c> if the group is visible to the user; <c>false</c> otherwise.</returns>
        public bool IsVisibleTo(Guid userId)
            => IsPublic || HasMember(userId)
               || (_invites.TryGetValue(userId, out var invite) && invite.GrantsJoin(DateTime.UtcNow));

        /// <summary>
        /// Checks whether the given user may join the group. Gusfin extension.
        /// Identical to <see cref="IsVisibleTo"/> today, kept separate so the two can diverge.
        /// </summary>
        /// <param name="userId">The user identifier.</param>
        /// <returns><c>true</c> if the user may join the group; <c>false</c> otherwise.</returns>
        public bool CanJoin(Guid userId)
            => IsVisibleTo(userId);

        /// <summary>
        /// Checks whether the given user holds an invite that no longer grants joining. Gusfin extension.
        /// Distinguishes "invite lapsed" from "never invited" so the right error can be picked
        /// without leaking the group's existence.
        /// </summary>
        /// <param name="userId">The user identifier.</param>
        /// <returns><c>true</c> if the user holds a lapsed invite; <c>false</c> otherwise.</returns>
        public bool HasLapsedInvite(Guid userId)
            => _invites.TryGetValue(userId, out var invite) && !invite.GrantsJoin(DateTime.UtcNow);

        /// <summary>
        /// Checks whether the given user has a pending invite to the group. Gusfin extension.
        /// </summary>
        /// <param name="userId">The user identifier.</param>
        /// <returns><c>true</c> if the user has a pending invite; <c>false</c> otherwise.</returns>
        public bool IsInvitePending(Guid userId)
            => _invites.TryGetValue(userId, out var invite) && invite.IsPending(DateTime.UtcNow);

        /// <summary>
        /// Adds an invite for the given user, or refreshes an existing one. Gusfin extension.
        /// Refreshing clears a previous decline, preserves a previous accept, resets the expiry
        /// and overwrites the inviter.
        /// </summary>
        /// <param name="from">The inviting session.</param>
        /// <param name="userId">The identifier of the user to invite.</param>
        /// <returns>The invite info to push to the invitee, or <c>null</c> if the user is already a member.</returns>
        public GroupInviteInfo AddOrRefreshInvite(SessionInfo from, Guid userId)
        {
            if (HasMember(userId))
            {
                return null;
            }

            var now = DateTime.UtcNow;
            var expiresAt = now.AddSeconds(InviteLifetimeSeconds);
            if (_invites.TryGetValue(userId, out var invite))
            {
                invite.Refresh(from.UserId, from.UserName, now, expiresAt);
            }
            else
            {
                invite = new GroupInvite(from.UserId, from.UserName, now, expiresAt);
                _invites[userId] = invite;
            }

            return GetInviteInfo(invite);
        }

        /// <summary>
        /// Marks the given user's invite as accepted, creating the record if none exists. Gusfin extension.
        /// An accepted invite never expires for the group's lifetime.
        /// </summary>
        /// <param name="userId">The user identifier.</param>
        public void MarkInviteAccepted(Guid userId)
        {
            var now = DateTime.UtcNow;
            if (_invites.TryGetValue(userId, out var invite))
            {
                invite.MarkAccepted(now);
            }
            else
            {
                invite = new GroupInvite(userId, string.Empty, now, now);
                invite.MarkAccepted(now);
                _invites[userId] = invite;
            }
        }

        /// <summary>
        /// Marks the given user's pending invite as declined. Gusfin extension.
        /// </summary>
        /// <param name="userId">The user identifier.</param>
        /// <returns><c>true</c> if a pending invite was declined; <c>false</c> otherwise.</returns>
        public bool MarkInviteDeclined(Guid userId)
        {
            var now = DateTime.UtcNow;
            if (_invites.TryGetValue(userId, out var invite) && invite.IsPending(now))
            {
                invite.MarkDeclined(now);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Gets the identifier of the user that invited the given user. Gusfin extension.
        /// </summary>
        /// <param name="userId">The invitee's user identifier.</param>
        /// <returns>The inviter's user identifier, or <c>null</c> if the user was never invited.</returns>
        public Guid? GetInviterUserId(Guid userId)
            => _invites.TryGetValue(userId, out var invite) ? invite.InvitedByUserId : null;

        /// <summary>
        /// Gets the identifiers of all users with a pending invite. Gusfin extension.
        /// Used to notify invitees when the group is torn down.
        /// </summary>
        /// <returns>The identifiers of all users with a pending invite.</returns>
        public IReadOnlyList<Guid> GetPendingInviteeIds()
        {
            var now = DateTime.UtcNow;
            return _invites
                .Where(pair => pair.Value.IsPending(now))
                .Select(pair => pair.Key)
                .ToList();
        }

        /// <summary>
        /// Gets the invite info for the given user's pending invite. Gusfin extension.
        /// </summary>
        /// <param name="userId">The user identifier.</param>
        /// <returns>The invite info, or <c>null</c> if the user has no pending invite.</returns>
        public GroupInviteInfo GetPendingInviteInfo(Guid userId)
        {
            if (_invites.TryGetValue(userId, out var invite) && invite.IsPending(DateTime.UtcNow))
            {
                return GetInviteInfo(invite);
            }

            return null;
        }

        /// <summary>
        /// Builds the wire payload for an invite. Gusfin extension.
        /// </summary>
        /// <param name="invite">The invite.</param>
        /// <returns>The invite info.</returns>
        private GroupInviteInfo GetInviteInfo(GroupInvite invite)
        {
            var participantCount = _participants.Values.Select(member => member.UserName).Distinct().Count();
            return new GroupInviteInfo(GroupId, GroupName, invite.InvitedByUserId, invite.InvitedByUserName, invite.ExpiresAt, participantCount);
        }

        /// <inheritdoc />
        public void SetIgnoreGroupWait(SessionInfo session, bool ignoreGroupWait)
        {
            if (_participants.TryGetValue(session.Id, out GroupMember value))
            {
                value.IgnoreGroupWait = ignoreGroupWait;
            }
        }

        /// <inheritdoc />
        public void SetState(IGroupState state)
        {
            _logger.LogInformation("Group {GroupId} switching from {FromStateType} to {ToStateType}.", GroupId.ToString(), _state.Type, state.Type);
            this._state = state;
        }

        /// <inheritdoc />
        public Task SendGroupUpdate<T>(SessionInfo from, SyncPlayBroadcastType type, GroupUpdate<T> message, CancellationToken cancellationToken)
        {
            IEnumerable<Task> GetTasks()
            {
                foreach (var sessionId in FilterSessions(from.Id, type))
                {
                    yield return _sessionManager.SendSyncPlayGroupUpdate(sessionId, message, cancellationToken);
                }
            }

            return Task.WhenAll(GetTasks());
        }

        /// <inheritdoc />
        public Task SendCommand(SessionInfo from, SyncPlayBroadcastType type, SendCommand message, CancellationToken cancellationToken)
        {
            IEnumerable<Task> GetTasks()
            {
                foreach (var sessionId in FilterSessions(from.Id, type))
                {
                    yield return _sessionManager.SendSyncPlayCommand(sessionId, message, cancellationToken);
                }
            }

            return Task.WhenAll(GetTasks());
        }

        /// <inheritdoc />
        public SendCommand NewSyncPlayCommand(SendCommandType type)
        {
            return new SendCommand(
                GroupId,
                PlayQueue.GetPlayingItemPlaylistId(),
                LastActivity,
                type,
                PositionTicks,
                DateTime.UtcNow);
        }

        /// <inheritdoc />
        public long SanitizePositionTicks(long? positionTicks)
        {
            var ticks = positionTicks ?? 0;
            return Math.Clamp(ticks, 0, RunTimeTicks);
        }

        /// <inheritdoc />
        public void UpdatePing(SessionInfo session, long ping)
        {
            if (_participants.TryGetValue(session.Id, out GroupMember value))
            {
                value.Ping = ping;
            }
        }

        /// <inheritdoc />
        public long GetHighestPing()
        {
            long max = long.MinValue;
            foreach (var session in _participants.Values)
            {
                max = Math.Max(max, session.Ping);
            }

            return max;
        }

        /// <inheritdoc />
        public void UpdateDiagnostics(SessionInfo session, DiagnosticsGroupRequest request)
        {
            if (_participants.TryGetValue(session.Id, out GroupMember value))
            {
                value.SupportsDiagnostics = true;
                value.LastPositionTicks = SanitizePositionTicks(request.PositionTicks);
                value.LastPlaybackDiffMillis = request.PlaybackDiffMillis;
                value.LastIsPlaying = request.IsPlaying;
                value.LastDiagnosticsReportAt = DateTime.UtcNow;
            }
        }

        /// <inheritdoc />
        public Task BroadcastDiagnosticsIfDue(SessionInfo from, CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;
            if ((now - _lastDiagnosticsBroadcast).TotalMilliseconds < DiagnosticsBroadcastIntervalMs)
            {
                return Task.CompletedTask;
            }

            _lastDiagnosticsBroadcast = now;

            var isPlaying = _state.Type.Equals(GroupStateType.Playing);
            var groupPositionTicks = PositionTicks;
            if (isPlaying)
            {
                var elapsedTime = now - LastActivity;
                // Elapsed time is negative while playback start is delayed to account for latency,
                // in which case LastActivity is in the future. See GetPlayQueueUpdate.
                groupPositionTicks += Math.Max(elapsedTime.Ticks, 0);
            }

            var members = new List<MemberDiagnosticsInfo>(_participants.Count);
            foreach (var member in _participants.Values)
            {
                members.Add(new MemberDiagnosticsInfo(
                    member.UserId,
                    member.UserName,
                    member.Ping,
                    member.IsBuffering,
                    member.SupportsDiagnostics,
                    member.SupportsDiagnostics ? member.LastPositionTicks : (long?)null,
                    member.SupportsDiagnostics ? member.LastPlaybackDiffMillis : (double?)null,
                    member.LastIsPlaying,
                    member.SupportsDiagnostics ? member.LastDiagnosticsReportAt : (DateTime?)null));
            }

            var update = new SyncPlayGroupDiagnosticsUpdate(
                GroupId,
                new GroupDiagnosticsUpdate(SanitizePositionTicks(groupPositionTicks), now, isPlaying, members));

            // Only diagnostics-capable members receive this update type,
            // so that clients unaware of the extension never see it.
            IEnumerable<Task> GetTasks()
            {
                foreach (var member in _participants.Values)
                {
                    if (member.SupportsDiagnostics)
                    {
                        yield return _sessionManager.SendSyncPlayGroupUpdate(member.SessionId, update, cancellationToken);
                    }
                }
            }

            return Task.WhenAll(GetTasks());
        }

        /// <inheritdoc />
        public void SetBuffering(SessionInfo session, bool isBuffering)
        {
            if (_participants.TryGetValue(session.Id, out GroupMember value))
            {
                value.IsBuffering = isBuffering;
            }
        }

        /// <inheritdoc />
        public void SetAllBuffering(bool isBuffering)
        {
            foreach (var session in _participants.Values)
            {
                session.IsBuffering = isBuffering;
            }
        }

        /// <inheritdoc />
        public bool IsBuffering()
        {
            foreach (var session in _participants.Values)
            {
                if (session.IsBuffering && !session.IgnoreGroupWait)
                {
                    return true;
                }
            }

            return false;
        }

        /// <inheritdoc />
        public bool SetPlayQueue(IReadOnlyList<Guid> playQueue, int playingItemPosition, long startPositionTicks)
        {
            // Ignore on empty queue or invalid item position.
            if (playQueue.Count == 0 || playingItemPosition >= playQueue.Count || playingItemPosition < 0)
            {
                return false;
            }

            // Check if participants can access the new playing queue.
            if (!AllUsersHaveAccessToQueue(playQueue))
            {
                return false;
            }

            PlayQueue.Reset();
            PlayQueue.SetPlaylist(playQueue);
            PlayQueue.SetPlayingItemByIndex(playingItemPosition);
            var item = _libraryManager.GetItemById(PlayQueue.GetPlayingItemId());
            RunTimeTicks = item.RunTimeTicks ?? 0;
            PositionTicks = startPositionTicks;
            LastActivity = DateTime.UtcNow;

            return true;
        }

        /// <inheritdoc />
        public bool SetPlayingItem(Guid playlistItemId)
        {
            var itemFound = PlayQueue.SetPlayingItemByPlaylistId(playlistItemId);

            if (itemFound)
            {
                var item = _libraryManager.GetItemById(PlayQueue.GetPlayingItemId());
                RunTimeTicks = item.RunTimeTicks ?? 0;
            }
            else
            {
                RunTimeTicks = 0;
            }

            RestartCurrentItem();

            return itemFound;
        }

        /// <inheritdoc />
        public void ClearPlayQueue(bool clearPlayingItem)
        {
            PlayQueue.ClearPlaylist(clearPlayingItem);
            if (clearPlayingItem)
            {
                RestartCurrentItem();
            }
        }

        /// <inheritdoc />
        public bool RemoveFromPlayQueue(IReadOnlyList<Guid> playlistItemIds)
        {
            var playingItemRemoved = PlayQueue.RemoveFromPlaylist(playlistItemIds);
            if (playingItemRemoved)
            {
                var itemId = PlayQueue.GetPlayingItemId();
                if (!itemId.IsEmpty())
                {
                    var item = _libraryManager.GetItemById(itemId);
                    RunTimeTicks = item.RunTimeTicks ?? 0;
                }
                else
                {
                    RunTimeTicks = 0;
                }

                RestartCurrentItem();
            }

            return playingItemRemoved;
        }

        /// <inheritdoc />
        public bool MoveItemInPlayQueue(Guid playlistItemId, int newIndex)
        {
            return PlayQueue.MovePlaylistItem(playlistItemId, newIndex);
        }

        /// <inheritdoc />
        public bool AddToPlayQueue(IReadOnlyList<Guid> newItems, GroupQueueMode mode)
        {
            // Ignore on empty list.
            if (newItems.Count == 0)
            {
                return false;
            }

            // Check if participants can access the new playing queue.
            if (!AllUsersHaveAccessToQueue(newItems))
            {
                return false;
            }

            if (mode.Equals(GroupQueueMode.QueueNext))
            {
                PlayQueue.QueueNext(newItems);
            }
            else
            {
                PlayQueue.Queue(newItems);
            }

            return true;
        }

        /// <inheritdoc />
        public void RestartCurrentItem()
        {
            PositionTicks = 0;
            LastActivity = DateTime.UtcNow;
        }

        /// <inheritdoc />
        public bool NextItemInQueue()
        {
            var update = PlayQueue.Next();
            if (update)
            {
                var item = _libraryManager.GetItemById(PlayQueue.GetPlayingItemId());
                RunTimeTicks = item.RunTimeTicks ?? 0;
                RestartCurrentItem();
                return true;
            }

            return false;
        }

        /// <inheritdoc />
        public bool PreviousItemInQueue()
        {
            var update = PlayQueue.Previous();
            if (update)
            {
                var item = _libraryManager.GetItemById(PlayQueue.GetPlayingItemId());
                RunTimeTicks = item.RunTimeTicks ?? 0;
                RestartCurrentItem();
                return true;
            }

            return false;
        }

        /// <inheritdoc />
        public void SetRepeatMode(GroupRepeatMode mode)
        {
            PlayQueue.SetRepeatMode(mode);
        }

        /// <inheritdoc />
        public void SetShuffleMode(GroupShuffleMode mode)
        {
            PlayQueue.SetShuffleMode(mode);
        }

        /// <inheritdoc />
        public PlayQueueUpdate GetPlayQueueUpdate(PlayQueueUpdateReason reason)
        {
            var startPositionTicks = PositionTicks;
            var isPlaying = _state.Type.Equals(GroupStateType.Playing);

            if (isPlaying)
            {
                var currentTime = DateTime.UtcNow;
                var elapsedTime = currentTime - LastActivity;
                // Elapsed time is negative if event happens
                // during the delay added to account for latency.
                // In this phase clients haven't started the playback yet.
                // In other words, LastActivity is in the future,
                // when playback unpause is supposed to happen.
                // Adjust ticks only if playback actually started.
                startPositionTicks += Math.Max(elapsedTime.Ticks, 0);
            }

            return new PlayQueueUpdate(
                reason,
                PlayQueue.LastChange,
                PlayQueue.GetPlaylist(),
                PlayQueue.PlayingItemIndex,
                startPositionTicks,
                isPlaying,
                PlayQueue.ShuffleMode,
                PlayQueue.RepeatMode);
        }

        /// <summary>
        /// Class GroupInvite. Gusfin extension: the state of an invite to this group.
        /// Access is serialized by the group lock, like the rest of the group's state.
        /// </summary>
        private sealed class GroupInvite
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="GroupInvite"/> class.
            /// </summary>
            /// <param name="invitedByUserId">The inviter's user identifier.</param>
            /// <param name="invitedByUserName">The inviter's user name.</param>
            /// <param name="createdAt">The UTC time the invite was created.</param>
            /// <param name="expiresAt">The UTC time the invite expires.</param>
            public GroupInvite(Guid invitedByUserId, string invitedByUserName, DateTime createdAt, DateTime expiresAt)
            {
                InvitedByUserId = invitedByUserId;
                InvitedByUserName = invitedByUserName;
                CreatedAt = createdAt;
                ExpiresAt = expiresAt;
            }

            /// <summary>
            /// Gets the inviter's user identifier.
            /// </summary>
            public Guid InvitedByUserId { get; private set; }

            /// <summary>
            /// Gets the inviter's user name.
            /// </summary>
            public string InvitedByUserName { get; private set; }

            /// <summary>
            /// Gets the UTC time the invite was created.
            /// </summary>
            public DateTime CreatedAt { get; private set; }

            /// <summary>
            /// Gets the UTC time the invite expires.
            /// </summary>
            public DateTime ExpiresAt { get; private set; }

            /// <summary>
            /// Gets the UTC time the invitee joined. An accepted invite never expires for the group's lifetime.
            /// </summary>
            public DateTime? AcceptedAt { get; private set; }

            /// <summary>
            /// Gets the UTC time the invitee declined. A declined invite needs a fresh invite to retry.
            /// </summary>
            public DateTime? DeclinedAt { get; private set; }

            /// <summary>
            /// Checks whether the invite is awaiting an answer.
            /// </summary>
            /// <param name="now">The current UTC time.</param>
            /// <returns><c>true</c> if the invite is pending; <c>false</c> otherwise.</returns>
            public bool IsPending(DateTime now)
                => AcceptedAt is null && DeclinedAt is null && ExpiresAt > now;

            /// <summary>
            /// Checks whether the invite grants joining the group.
            /// </summary>
            /// <param name="now">The current UTC time.</param>
            /// <returns><c>true</c> if the invite grants joining; <c>false</c> otherwise.</returns>
            public bool GrantsJoin(DateTime now)
                => AcceptedAt is not null || IsPending(now);

            /// <summary>
            /// Re-issues the invite: clears a previous decline, preserves a previous accept,
            /// resets the expiry and overwrites the inviter.
            /// </summary>
            /// <param name="byUserId">The new inviter's user identifier.</param>
            /// <param name="byUserName">The new inviter's user name.</param>
            /// <param name="now">The current UTC time.</param>
            /// <param name="expiresAt">The new UTC expiry time.</param>
            public void Refresh(Guid byUserId, string byUserName, DateTime now, DateTime expiresAt)
            {
                InvitedByUserId = byUserId;
                InvitedByUserName = byUserName;
                CreatedAt = now;
                ExpiresAt = expiresAt;
                DeclinedAt = null;
            }

            /// <summary>
            /// Marks the invite as accepted. Idempotent: the first accept time is kept.
            /// </summary>
            /// <param name="when">The current UTC time.</param>
            public void MarkAccepted(DateTime when)
                => AcceptedAt ??= when;

            /// <summary>
            /// Marks the invite as declined.
            /// </summary>
            /// <param name="when">The current UTC time.</param>
            public void MarkDeclined(DateTime when)
                => DeclinedAt = when;
        }
    }
}
