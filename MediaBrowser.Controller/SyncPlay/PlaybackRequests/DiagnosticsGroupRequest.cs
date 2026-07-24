#nullable disable

using System;
using System.Threading;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.SyncPlay;

namespace MediaBrowser.Controller.SyncPlay.PlaybackRequests
{
    /// <summary>
    /// Class DiagnosticsGroupRequest. Gusfin extension: periodic playback diagnostics report.
    /// </summary>
    public class DiagnosticsGroupRequest : AbstractPlaybackRequest
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DiagnosticsGroupRequest"/> class.
        /// </summary>
        /// <param name="when">When the request has been made, as reported by the client.</param>
        /// <param name="positionTicks">The position ticks.</param>
        /// <param name="playbackDiffMillis">The playback diff, in milliseconds.</param>
        /// <param name="isPlaying">Whether the client playback is unpaused.</param>
        public DiagnosticsGroupRequest(DateTime when, long positionTicks, double playbackDiffMillis, bool isPlaying)
        {
            When = when;
            PositionTicks = positionTicks;
            PlaybackDiffMillis = playbackDiffMillis;
            IsPlaying = isPlaying;
        }

        /// <summary>
        /// Gets when the request has been made by the client.
        /// </summary>
        /// <value>The date of the request.</value>
        public DateTime When { get; }

        /// <summary>
        /// Gets the position ticks.
        /// </summary>
        /// <value>The position ticks.</value>
        public long PositionTicks { get; }

        /// <summary>
        /// Gets the difference between the client's playback position and the group position, in milliseconds.
        /// </summary>
        /// <value>The playback diff, in milliseconds. Positive means the client is behind the group.</value>
        public double PlaybackDiffMillis { get; }

        /// <summary>
        /// Gets a value indicating whether the client playback is unpaused.
        /// </summary>
        /// <value>The client playback status.</value>
        public bool IsPlaying { get; }

        /// <inheritdoc />
        public override PlaybackRequestType Action { get; } = PlaybackRequestType.Diagnostics;

        /// <inheritdoc />
        public override void Apply(IGroupStateContext context, IGroupState state, SessionInfo session, CancellationToken cancellationToken)
        {
            state.HandleRequest(this, context, state.Type, session, cancellationToken);
        }
    }
}
