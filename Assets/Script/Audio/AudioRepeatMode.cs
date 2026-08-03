using UnityEngine;

/// <summary>How a repeating sound decides when to fire the next clip.</summary>
/// <remarks>Shared by <see cref="AudioRepeater"/> at runtime and the Audio Set inspector's auto-play, so auditioning a set uses the same timing the game will.</remarks>
public enum AudioRepeatMode
{
    /// <summary>Next clip starts the moment the current one finishes. No gap, and the interval is unused.</summary>
    [InspectorName("On end")] OnEnd,

    /// <summary>Current clip finishes, then the interval passes, then the next one starts.</summary>
    [InspectorName("On end + gap")] OnEndGap,

    /// <summary>A clip starts every interval regardless of how long they are, so they overlap if the interval is shorter than the clip.</summary>
    [InspectorName("Every interval")] EveryInterval,
}
