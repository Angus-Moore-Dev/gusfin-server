#nullable disable

using System;
using System.Collections.Generic;
using System.Threading;
using MediaBrowser.Controller.Session;
using MediaBrowser.Controller.SyncPlay.Requests;
using MediaBrowser.Model.SyncPlay;

namespace MediaBrowser.Controller.SyncPlay
{
    /// <summary>
    /// Interface ISyncPlayManager.
    /// </summary>
    public interface ISyncPlayManager
    {
        /// <summary>
        /// Creates a new group.
        /// </summary>
        /// <param name="session">The session that's creating the group.</param>
        /// <param name="request">The request.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The newly created group.</returns>
        GroupInfoDto NewGroup(SessionInfo session, NewGroupRequest request, CancellationToken cancellationToken);

        /// <summary>
        /// Adds the session to a group.
        /// </summary>
        /// <param name="session">The session.</param>
        /// <param name="request">The request.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        void JoinGroup(SessionInfo session, JoinGroupRequest request, CancellationToken cancellationToken);

        /// <summary>
        /// Removes the session from a group.
        /// </summary>
        /// <param name="session">The session.</param>
        /// <param name="request">The request.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        void LeaveGroup(SessionInfo session, LeaveGroupRequest request, CancellationToken cancellationToken);

        /// <summary>
        /// Gets list of available groups for a session.
        /// </summary>
        /// <param name="session">The session.</param>
        /// <param name="request">The request.</param>
        /// <returns>The list of available groups.</returns>
        List<GroupInfoDto> ListGroups(SessionInfo session, ListGroupsRequest request);

        /// <summary>
        /// Gets available groups for a session by id.
        /// </summary>
        /// <param name="session">The session.</param>
        /// <param name="groupId">The group id.</param>
        /// <returns>The groups or null.</returns>
        GroupInfoDto GetGroup(SessionInfo session, Guid groupId);

        /// <summary>
        /// Handle a request by a session in a group.
        /// </summary>
        /// <param name="session">The session.</param>
        /// <param name="request">The request.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        void HandleRequest(SessionInfo session, IGroupPlaybackRequest request, CancellationToken cancellationToken);

        /// <summary>
        /// Checks whether a user has an active session using SyncPlay.
        /// </summary>
        /// <param name="userId">The user identifier to check.</param>
        /// <returns><c>true</c> if the user is using SyncPlay; <c>false</c> otherwise.</returns>
        bool IsUserActive(Guid userId);

        /// <summary>
        /// Invites users to the caller's group. Gusfin extension.
        /// </summary>
        /// <param name="session">The inviting session. The target group is resolved from this session.</param>
        /// <param name="userIds">The identifiers of the users to invite.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        void InviteToGroup(SessionInfo session, IReadOnlyList<Guid> userIds, CancellationToken cancellationToken);

        /// <summary>
        /// Declines a pending invite to a group. Gusfin extension.
        /// </summary>
        /// <param name="session">The declining session.</param>
        /// <param name="groupId">The group identifier.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        void DeclineInvite(SessionInfo session, Guid groupId, CancellationToken cancellationToken);

        /// <summary>
        /// Gets the online users that the caller may invite to their group. Gusfin extension.
        /// </summary>
        /// <param name="session">The session. The target group is resolved from this session.</param>
        /// <returns>The invite candidates, or an empty list if the session is in no group.</returns>
        IReadOnlyList<SyncPlayInviteCandidateDto> GetInviteCandidates(SessionInfo session);

        /// <summary>
        /// Gets the pending invites addressed to the session's user. Gusfin extension.
        /// </summary>
        /// <param name="session">The session.</param>
        /// <returns>The pending invites.</returns>
        IReadOnlyList<GroupInviteInfo> ListInvites(SessionInfo session);
    }
}
