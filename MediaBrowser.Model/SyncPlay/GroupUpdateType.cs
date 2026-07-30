namespace MediaBrowser.Model.SyncPlay
{
    /// <summary>
    /// Enum GroupUpdateType.
    /// </summary>
    public enum GroupUpdateType
    {
        /// <summary>
        /// The user-joined update. Tells members of a group about a new user.
        /// </summary>
        UserJoined,

        /// <summary>
        /// The user-left update. Tells members of a group that a user left.
        /// </summary>
        UserLeft,

        /// <summary>
        /// The group-joined update. Tells a user that the group has been joined.
        /// </summary>
        GroupJoined,

        /// <summary>
        /// The group-left update. Tells a user that the group has been left.
        /// </summary>
        GroupLeft,

        /// <summary>
        /// The group-state update. Tells members of the group that the state changed.
        /// </summary>
        StateUpdate,

        /// <summary>
        /// The play-queue update. Tells a user the playing queue of the group.
        /// </summary>
        PlayQueue,

        /// <summary>
        /// The not-in-group error. Tells a user that they don't belong to a group.
        /// </summary>
        NotInGroup,

        /// <summary>
        /// The group-does-not-exist error. Sent when trying to join a non-existing group.
        /// </summary>
        GroupDoesNotExist,

        /// <summary>
        /// The library-access-denied error. Sent when a user tries to join a group without required access to the library.
        /// </summary>
        LibraryAccessDenied,

        /// <summary>
        /// The group-diagnostics update. Gusfin extension: periodic per-member playback diagnostics,
        /// only sent to members that have reported diagnostics themselves.
        /// </summary>
        GroupDiagnostics,

        /// <summary>
        /// The group-invite update. Gusfin extension: a group invite addressed to this user.
        /// </summary>
        GroupInvite,

        /// <summary>
        /// The group-invite-cancelled update. Gusfin extension: a previously delivered invite is no longer actionable.
        /// </summary>
        GroupInviteCancelled,

        /// <summary>
        /// The group-invite-required error. Gusfin extension: join rejected, the private group requires a valid invite.
        /// </summary>
        GroupInviteRequired,

        /// <summary>
        /// The group-invite-declined update. Gusfin extension: an invitee declined; sent to the inviter.
        /// </summary>
        GroupInviteDeclined
    }
}
