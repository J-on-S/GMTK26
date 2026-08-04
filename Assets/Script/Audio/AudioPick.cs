/// <summary>How a set of interchangeable clips decides which one to play next.</summary>
/// <remarks>
/// Shared by <see cref="AudioSet"/> and <see cref="PlayerHitSound"/> so the two cannot drift apart.
/// The values match the order of the private enum PlayerHitSound used before, so components already
/// set up in scenes and prefabs keep the mode they were authored with.
/// </remarks>
public enum AudioPick
{
    Random,
    InOrder
}
