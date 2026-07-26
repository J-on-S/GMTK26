using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Owns the code-built input actions the cutting minigame and free-look share, independent of any scene object.</summary>
/// <remarks>
/// These used to be declared and built by <c>CuttingManager.Start</c>, so scroll, arrow and raw mouse-motion
/// input were dead in any scene that had no active <c>CuttingManager</c> -- nothing existed to declare them,
/// so <see cref="MouseDelta"/> stayed <c>null</c> and every reader bailed. They are declared here instead and
/// built once at startup, so every reader has them whether or not a cut is present.
/// <para>
/// Built through <see cref="RuntimeInitializeOnLoadMethod"/> and held for the whole play session, never
/// disposed: unlike a per-scene owner there is no teardown that could null a static another object is still
/// reading. Re-enabling an already-enabled action is a no-op, and the <c>== null</c> guards make a second
/// build harmless.
/// </para>
/// </remarks>
public static class GameInputActions
{
    /// <summary>Mouse scroll wheel; y carries the wheel delta. Same role as <see cref="Arrows"/>.</summary>
    public static InputAction Scroll { get; private set; }

    /// <summary>Arrow/WASD keys as a 2D vector, built in code so the input asset needs no entry. Same effect as the wheel.</summary>
    public static InputAction Arrows { get; private set; }

    /// <summary>Per-frame mouse motion in pixels, both axes: x = horizontal, y = vertical.</summary>
    public static InputAction MouseDelta { get; private set; }

    /// <summary>Builds and enables every shared action before the first scene loads, so readers never race their creation.</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Build()
    {
        if (Scroll == null)
        {
            Scroll = new InputAction(
                name: "MouseScroll",
                type: InputActionType.Value,
                binding: "<Mouse>/scroll");
            Scroll.Enable();
        }

        if (Arrows == null)
        {
            Arrows = new InputAction("Arrows", InputActionType.Value, expectedControlType: "Vector2");
            Arrows.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/downArrow")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/leftArrow")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d")
                .With("Right", "<Keyboard>/rightArrow");
            Arrows.Enable();
        }

        if (MouseDelta == null)
        {
            MouseDelta = new InputAction(
                name: "MouseDelta",
                type: InputActionType.Value,
                binding: "<Mouse>/delta",
                expectedControlType: "Vector2");
            MouseDelta.Enable();
        }
    }
}
