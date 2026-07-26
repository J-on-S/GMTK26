using UnityEngine;

/// <summary>The channel and clips a <see cref="GrabbableObject"/> plays when it is picked up or dropped.</summary>
/// <remarks>
/// One asset shared by every grabbable of the same material family, rather than four clip fields on each
/// object: the sounds are a property of what the item is made of, not of the individual prop, so a scene
/// full of scalpels does not mean re-picking the same two clips on every one of them.
/// Clips are looked up by <see cref="ItemType"/> so adding a type is a change here, not at each call site.
/// </remarks>
[CreateAssetMenu(fileName = "AudioGrappablePreset", menuName = "Interaction/Audio Grabbable Preset")]
public class AudioGrappablePreset : ScriptableObject
{
    [Tooltip("Channel the clips are played on. Must be the one the scene's AudioMaster listens to, or they play into nothing.")]
    public AudioEventChannel channel;

    [Header("Tool (metal)")]
    [Tooltip("One-shot when a Tool is picked up.")]
    public Audio toolPickup;

    [Tooltip("One-shot when a Tool is dropped.")]
    public Audio toolDrop;

    [Header("Body part (soft)")]
    [Tooltip("One-shot when a BodyPart is picked up.")]
    public Audio bodyPartPickup;

    [Tooltip("One-shot when a BodyPart is dropped.")]
    public Audio bodyPartDrop;

    [Header("Our tool (black market)")]
    [Tooltip("One-shot when an OurTool is picked up. Left empty, the Tool clip is used.")]
    public Audio ourToolPickup;

    [Tooltip("One-shot when an OurTool is dropped. Left empty, the Tool clip is used.")]
    public Audio ourToolDrop;

    /// <summary>Pickup clip for <paramref name="itemType"/>, or null when none is assigned.</summary>
    /// <remarks><see cref="ItemType.OurTool"/> falls back to the Tool clip: it is metal too, so an unfilled
    /// slot should stay silent-free rather than force a duplicate of the same clip in two fields.</remarks>
    public Audio GetPickup(ItemType itemType) => itemType switch
    {
        ItemType.Tool => toolPickup,
        ItemType.BodyPart => bodyPartPickup,
        ItemType.OurTool => ourToolPickup != null ? ourToolPickup : toolPickup,
        _ => null,
    };

    /// <summary>Drop clip for <paramref name="itemType"/>, or null when none is assigned.</summary>
    public Audio GetDrop(ItemType itemType) => itemType switch
    {
        ItemType.Tool => toolDrop,
        ItemType.BodyPart => bodyPartDrop,
        ItemType.OurTool => ourToolDrop != null ? ourToolDrop : toolDrop,
        _ => null,
    };

    /// <summary>Plays the pickup clip for <paramref name="itemType"/>. No-op when the channel or the clip is missing.</summary>
    /// <remarks>Guarded here so a half-authored preset costs a silent grab instead of a NullReferenceException
    /// in the middle of an interaction.</remarks>
    public void PlayPickup(ItemType itemType) => Play(GetPickup(itemType));

    /// <summary>Plays the drop clip for <paramref name="itemType"/>. No-op when the channel or the clip is missing.</summary>
    public void PlayDrop(ItemType itemType) => Play(GetDrop(itemType));

    private void Play(Audio clip)
    {
        if (channel == null || clip == null) return;
        channel.Play(clip);
    }
}
