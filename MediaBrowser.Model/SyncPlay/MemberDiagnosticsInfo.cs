using System;

namespace MediaBrowser.Model.SyncPlay;

/// <summary>
/// Class MemberDiagnosticsInfo. Gusfin extension: per-member playback diagnostics of a SyncPlay group.
/// </summary>
public class MemberDiagnosticsInfo
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MemberDiagnosticsInfo"/> class.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="userName">The username.</param>
    /// <param name="ping">The ping, in milliseconds.</param>
    /// <param name="isBuffering">Whether the member is buffering.</param>
    /// <param name="isReporting">Whether the member reports diagnostics.</param>
    /// <param name="positionTicks">The last reported position ticks.</param>
    /// <param name="playbackDiffMillis">The last reported playback diff, in milliseconds.</param>
    /// <param name="isPlaying">Whether the member playback is unpaused.</param>
    /// <param name="lastReportedAt">When the member last reported diagnostics.</param>
    public MemberDiagnosticsInfo(
        Guid userId,
        string userName,
        long ping,
        bool isBuffering,
        bool isReporting,
        long? positionTicks,
        double? playbackDiffMillis,
        bool isPlaying,
        DateTime? lastReportedAt)
    {
        UserId = userId;
        UserName = userName;
        Ping = ping;
        IsBuffering = isBuffering;
        IsReporting = isReporting;
        PositionTicks = positionTicks;
        PlaybackDiffMillis = playbackDiffMillis;
        IsPlaying = isPlaying;
        LastReportedAt = lastReportedAt;
    }

    /// <summary>
    /// Gets the user identifier.
    /// </summary>
    /// <value>The user identifier.</value>
    public Guid UserId { get; }

    /// <summary>
    /// Gets the username.
    /// </summary>
    /// <value>The username.</value>
    public string UserName { get; }

    /// <summary>
    /// Gets the ping, in milliseconds.
    /// </summary>
    /// <value>The ping.</value>
    public long Ping { get; }

    /// <summary>
    /// Gets a value indicating whether the member is buffering.
    /// </summary>
    /// <value><c>true</c> if the member is buffering; <c>false</c> otherwise.</value>
    public bool IsBuffering { get; }

    /// <summary>
    /// Gets a value indicating whether the member reports diagnostics.
    /// </summary>
    /// <value><c>true</c> if the member has sent at least one diagnostics report; <c>false</c> otherwise.</value>
    public bool IsReporting { get; }

    /// <summary>
    /// Gets the last reported position ticks, or <c>null</c> if the member never reported.
    /// </summary>
    /// <value>The position ticks.</value>
    public long? PositionTicks { get; }

    /// <summary>
    /// Gets the last reported difference between the member's playback position and the group position, in milliseconds.
    /// </summary>
    /// <value>The playback diff, in milliseconds. Positive means the member is behind the group.</value>
    public double? PlaybackDiffMillis { get; }

    /// <summary>
    /// Gets a value indicating whether the member's playback is unpaused.
    /// </summary>
    /// <value><c>true</c> if playback is unpaused; <c>false</c> otherwise.</value>
    public bool IsPlaying { get; }

    /// <summary>
    /// Gets when the member last reported diagnostics, or <c>null</c> if never.
    /// </summary>
    /// <value>The date of the last report.</value>
    public DateTime? LastReportedAt { get; }
}
