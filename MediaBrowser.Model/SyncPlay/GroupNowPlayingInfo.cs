using System;

namespace MediaBrowser.Model.SyncPlay;

/// <summary>
/// Class GroupNowPlayingInfo. Gusfin extension: what a SyncPlay group is currently watching.
/// </summary>
public class GroupNowPlayingInfo
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GroupNowPlayingInfo"/> class.
    /// </summary>
    /// <param name="itemId">The playing item identifier.</param>
    /// <param name="name">The playing item name.</param>
    /// <param name="seriesName">The series name, when the item is an episode.</param>
    /// <param name="seasonNumber">The season number, when the item is an episode.</param>
    /// <param name="episodeNumber">The episode number, when the item is an episode.</param>
    /// <param name="positionTicks">The estimated group playback position, in ticks.</param>
    /// <param name="runTimeTicks">The runtime of the playing item, in ticks.</param>
    /// <param name="isPlaying">Whether the group playback is unpaused.</param>
    public GroupNowPlayingInfo(
        Guid itemId,
        string name,
        string? seriesName,
        int? seasonNumber,
        int? episodeNumber,
        long positionTicks,
        long runTimeTicks,
        bool isPlaying)
    {
        ItemId = itemId;
        Name = name;
        SeriesName = seriesName;
        SeasonNumber = seasonNumber;
        EpisodeNumber = episodeNumber;
        PositionTicks = positionTicks;
        RunTimeTicks = runTimeTicks;
        IsPlaying = isPlaying;
    }

    /// <summary>
    /// Gets the playing item identifier.
    /// </summary>
    /// <value>The playing item identifier.</value>
    public Guid ItemId { get; }

    /// <summary>
    /// Gets the playing item name.
    /// </summary>
    /// <value>The playing item name.</value>
    public string Name { get; }

    /// <summary>
    /// Gets the series name, when the item is an episode.
    /// </summary>
    /// <value>The series name.</value>
    public string? SeriesName { get; }

    /// <summary>
    /// Gets the season number, when the item is an episode.
    /// </summary>
    /// <value>The season number.</value>
    public int? SeasonNumber { get; }

    /// <summary>
    /// Gets the episode number, when the item is an episode.
    /// </summary>
    /// <value>The episode number.</value>
    public int? EpisodeNumber { get; }

    /// <summary>
    /// Gets the estimated group playback position at the time this DTO was created, in ticks.
    /// </summary>
    /// <value>The estimated group playback position.</value>
    public long PositionTicks { get; }

    /// <summary>
    /// Gets the runtime of the playing item, in ticks.
    /// </summary>
    /// <value>The runtime of the playing item.</value>
    public long RunTimeTicks { get; }

    /// <summary>
    /// Gets a value indicating whether the group playback is unpaused.
    /// </summary>
    /// <value><c>true</c> if the group playback is unpaused; <c>false</c> otherwise.</value>
    public bool IsPlaying { get; }
}
