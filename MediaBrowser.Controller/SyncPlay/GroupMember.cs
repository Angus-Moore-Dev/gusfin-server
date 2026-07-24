#nullable disable

using System;
using MediaBrowser.Controller.Session;

namespace MediaBrowser.Controller.SyncPlay
{
    /// <summary>
    /// Class GroupMember.
    /// </summary>
    public class GroupMember
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GroupMember"/> class.
        /// </summary>
        /// <param name="session">The session.</param>
        public GroupMember(SessionInfo session)
        {
            SessionId = session.Id;
            UserId = session.UserId;
            UserName = session.UserName;
        }

        /// <summary>
        /// Gets the identifier of the session.
        /// </summary>
        /// <value>The session identifier.</value>
        public string SessionId { get; }

        /// <summary>
        /// Gets the identifier of the user.
        /// </summary>
        /// <value>The user identifier.</value>
        public Guid UserId { get; }

        /// <summary>
        /// Gets the username.
        /// </summary>
        /// <value>The username.</value>
        public string UserName { get; }

        /// <summary>
        /// Gets or sets the ping, in milliseconds.
        /// </summary>
        /// <value>The ping.</value>
        public long Ping { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this member is buffering.
        /// </summary>
        /// <value><c>true</c> if member is buffering; <c>false</c> otherwise.</value>
        public bool IsBuffering { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this member is following group playback.
        /// </summary>
        /// <value><c>true</c> to ignore member on group wait; <c>false</c> if they're following group playback.</value>
        public bool IgnoreGroupWait { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this member reports playback diagnostics. Gusfin extension.
        /// </summary>
        /// <value><c>true</c> if the member has sent at least one diagnostics report; <c>false</c> otherwise.</value>
        public bool SupportsDiagnostics { get; set; }

        /// <summary>
        /// Gets or sets the last reported position ticks. Gusfin extension.
        /// </summary>
        /// <value>The position ticks.</value>
        public long LastPositionTicks { get; set; }

        /// <summary>
        /// Gets or sets the last reported playback diff, in milliseconds. Gusfin extension.
        /// </summary>
        /// <value>The playback diff, in milliseconds.</value>
        public double LastPlaybackDiffMillis { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the member's playback was unpaused in the last report. Gusfin extension.
        /// </summary>
        /// <value><c>true</c> if playback is unpaused; <c>false</c> otherwise.</value>
        public bool LastIsPlaying { get; set; }

        /// <summary>
        /// Gets or sets when the member last reported diagnostics, in UTC. Gusfin extension.
        /// </summary>
        /// <value>The date of the last report, or <see cref="DateTime.MinValue"/> if never.</value>
        public DateTime LastDiagnosticsReportAt { get; set; }
    }
}
