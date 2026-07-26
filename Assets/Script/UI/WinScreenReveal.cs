using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Shows the Win splash first, then reveals the normal result dossier when
/// the player clicks the Background image.
/// Attach this component to the Background GameObject in the Win scene.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Image))]
public class WinScreenReveal : MonoBehaviour, IPointerClickHandler
{
    [Header("Scene-specific opening image")]
    [SerializeField] private Sprite winBackground;

    [Header("Optional overrides")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Sprite pauseMenuCover;
    [SerializeField] private GameObject dossier;
    [SerializeField] private GameObject verticalLayoutGroup;

    private bool resultsRevealed;

    private void Awake()
    {
        EnsureEventSystem();
        ResolveReferences();

        // The shared prefab already uses PAUSEMENU_Cover. Capture it before
        // replacing the image with the Win splash.
        if (pauseMenuCover == null && backgroundImage != null)
            pauseMenuCover = backgroundImage.sprite;

        ShowOpeningScreen();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
            RevealResults();
    }

    [ContextMenu("Reveal Results")]
    public void RevealResults()
    {
        if (resultsRevealed)
            return;

        if (backgroundImage != null && pauseMenuCover != null)
            backgroundImage.sprite = pauseMenuCover;

        if (dossier != null)
            dossier.SetActive(true);

        if (verticalLayoutGroup != null)
            verticalLayoutGroup.SetActive(true);

        resultsRevealed = true;
    }

    [ContextMenu("Show Opening Screen")]
    public void ShowOpeningScreen()
    {
        if (backgroundImage != null && winBackground != null)
            backgroundImage.sprite = winBackground;
        else if (winBackground == null)
            Debug.LogWarning(
                "WinScreenReveal needs a Win Background sprite.",
                this);

        if (dossier != null)
            dossier.SetActive(false);

        if (verticalLayoutGroup != null)
            verticalLayoutGroup.SetActive(false);

        resultsRevealed = false;
    }

    private void ResolveReferences()
    {
        backgroundImage ??= GetComponent<Image>();
        dossier ??= FindSceneObject("Dossier");
        verticalLayoutGroup ??=
            FindSceneObject("VerticalLayoutGroup");
    }

    private static GameObject FindSceneObject(string objectName)
    {
        Transform[] sceneObjects =
            FindObjectsByType<Transform>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

        foreach (Transform sceneObject in sceneObjects)
        {
            if (sceneObject.name == objectName)
                return sceneObject.gameObject;
        }

        return null;
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null)
            return;

        GameObject eventSystem = new("EventSystem");
        eventSystem.AddComponent<EventSystem>();
        eventSystem.AddComponent<InputSystemUIInputModule>();
    }
}
