using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A group of interchangeable clips that plays as one: asked for a clip, it hands back one of its variants.
/// </summary>
/// <remarks>
/// <para>
/// The inherited leaf fields (AudioClip, Volume, Loop, Pan, Pitch...) are unused here.The custom editor hides them so they cannot be mistaken for live.
/// </para>
/// </remarks>
[CreateAssetMenu(fileName = "AudioSet", menuName = "Audio Set")]
public class AudioSet : Audio
{
    /// <summary>How deep nesting may go before a set is treated as referencing itself in a loop.</summary>
    private const int MaxNesting = 8;

    [Tooltip("Clips this set can hand back. A variant can be another Audio Set, which is then asked in turn.")]
    [SerializeField] private List<Audio> variants = new();

    [Tooltip("Random hands back any variant; In Order walks the list, wrapping at the end.")]
    [SerializeField] private AudioPick pick = AudioPick.Random;

    [Tooltip("Random only: never hand back the same variant twice in a row. Two identical footsteps back to back are exactly what a set of them is meant to avoid.")]
    [SerializeField] private bool avoidRepeat = true;

    /// <summary>Round-robin cursor for <see cref="AudioPick.InOrder"/>.</summary>
    private int index;

    /// <summary>The variant handed back last, so <see cref="avoidRepeat"/> can skip it. -1 before the first pick.</summary>
    private int lastIndex = -1;

    public IReadOnlyList<Audio> Variants => variants;

    public AudioPick Pick => pick;

    /// <summary>Where the In Order cursor currently sits.</summary>
    public int Index => index;

    /// <summary>A ScriptableObject outlives a play session, so the cursors are put back rather than resuming mid-list on the next run.</summary>
    private void OnEnable()
    {
        index = 0;
        lastIndex = -1;
    }

    /// <inheritdoc/>
    /// <remarks>Walks nested sets iteratively rather than recursing, so a set that ends up referencing itself stops at <see cref="MaxNesting"/> instead of overflowing the stack.</remarks>
    public override Audio GetAudio()
    {
        Audio current = this;

        for (int depth = 0; depth < MaxNesting; depth++)
        {
            AudioSet set = current as AudioSet;
            if (set == null) return current;

            Audio next = set.Take();
            if (next == null)
            {
                Debug.LogWarning($"AudioSet '{set.name}': nothing to play -- the variant list is empty or the picked entry is empty.", set);
                return null;
            }

            current = next;
        }

        Debug.LogWarning($"AudioSet '{name}': variants nest more than {MaxNesting} deep, or a set contains itself.", this);
        return null;
    }

    /// <inheritdoc/>
    public override bool Contains(Audio other) => ContainsInternal(other, MaxNesting);

    private bool ContainsInternal(Audio other, int depth)
    {
        if (other == this) return true;
        if (depth <= 0) return false;

        foreach (Audio variant in variants)
        {
            if (variant == null) continue;

            bool hit = variant is AudioSet set
                ? set.ContainsInternal(other, depth - 1)
                : variant.Contains(other);

            if (hit) return true;
        }

        return false;
    }

    /// <summary>The variant the next <see cref="GetAudio"/> would take, without advancing the cursor.</summary>
    /// <remarks>Only meaningful for <see cref="AudioPick.InOrder"/>; a random set has no next until it picks one. Returns the variant as authored, so a nested set reads as the set rather than as whatever it would resolve to.</remarks>
    public Audio Peek()
    {
        if (!HasVariants()) return null;
        return pick == AudioPick.InOrder ? variants[index % variants.Count] : null;
    }

    /// <summary>Puts the In Order cursor back to the start of the list, and forgets what Random handed back last.</summary>
    public void ResetOrder()
    {
        index = 0;
        lastIndex = -1;
    }

    /// <summary>One variant, advancing the cursor when the picker is In Order. May be null if that list slot is empty.</summary>
    private Audio Take()
    {
        if (!HasVariants()) return null;

        if (pick == AudioPick.Random)
        {
            lastIndex = RandomIndex();
            return variants[lastIndex];
        }

        lastIndex = index % variants.Count;
        index = (index + 1) % variants.Count;
        return variants[lastIndex];
    }

    /// <summary>A random slot, skipping the one picked last when Avoid Repeat is on.</summary>
    /// <remarks>
    /// Rolls over the other slots and steps past the excluded one, rather than re-rolling until it lands
    /// somewhere else: one roll, no loop that could in principle run long, and every remaining variant stays
    /// equally likely. With one variant there is nothing to avoid, so it comes back every time.
    /// </remarks>
    private int RandomIndex()
    {
        int count = variants.Count;

        if (!avoidRepeat || count < 2 || lastIndex < 0 || lastIndex >= count)
        {
            return Random.Range(0, count);
        }

        int picked = Random.Range(0, count - 1);
        return picked >= lastIndex ? picked + 1 : picked;
    }

    private bool HasVariants() => variants != null && variants.Count > 0;
}
