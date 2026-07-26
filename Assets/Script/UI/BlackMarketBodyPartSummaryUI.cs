using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Displays the persistent combined chute + fridge counts on result screens.
/// Automatically binds the existing Win rows and creates a text fallback on
/// result scenes that do not contain those rows.
/// </summary>
public class BlackMarketBodyPartSummaryUI : MonoBehaviour
{
    [SerializeField] private TMP_Text handCountText;
    [SerializeField] private TMP_Text mouthCountText;
    [SerializeField] private TMP_Text noseCountText;
    [SerializeField] private TMP_Text earCountText;
    [SerializeField] private TMP_Text fallbackSummaryText;

    private BodyPartRunSummary summary;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterResultSceneCallback()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private static void HandleSceneLoaded(
        Scene scene,
        LoadSceneMode loadMode)
    {
        string sceneName = scene.name;
        if (sceneName != "Win" && sceneName != "Lost")
            return;

        if (FindFirstObjectByType<
                BlackMarketBodyPartSummaryUI>() != null)
        {
            return;
        }

        GameObject displayObject =
            new("Black Market Body Part Summary UI");
        displayObject.AddComponent<
            BlackMarketBodyPartSummaryUI>();
    }

    private void OnEnable()
    {
        summary = BodyPartRunSummary.Instance;
        summary.CountsChanged += Refresh;
    }

    private void Start()
    {
        AutoBindExistingRows();

        if (!HasAllIndividualRows())
            CreateFallbackText();

        Refresh();
    }

    private void OnDisable()
    {
        if (summary != null)
            summary.CountsChanged -= Refresh;
    }

    public void Refresh()
    {
        if (summary == null)
            summary = BodyPartRunSummary.Instance;

        SetCount(
            handCountText,
            summary.GetTotalCount(BodyPartType.Hand));
        SetCount(
            mouthCountText,
            summary.GetTotalCount(BodyPartType.Mouth));
        SetCount(
            noseCountText,
            summary.GetTotalCount(BodyPartType.Nose));
        SetCount(
            earCountText,
            summary.GetTotalCount(BodyPartType.Ear));

        if (fallbackSummaryText != null)
        {
            fallbackSummaryText.text =
                BuildFallbackSummary();
        }
    }

    private void AutoBindExistingRows()
    {
        TMP_Text[] allText =
            FindObjectsByType<TMP_Text>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

        handCountText ??=
            FindRowCount(allText, "Hand WinLoseStatRow");
        mouthCountText ??=
            FindRowCount(allText, "Mouth WinLoseStatRow");
        noseCountText ??=
            FindRowCount(allText, "Nose WinLoseStatRow");
        earCountText ??=
            FindRowCount(allText, "Ear WinLoseStatRow");
    }

    private static TMP_Text FindRowCount(
        TMP_Text[] allText,
        string rowName)
    {
        foreach (TMP_Text text in allText)
        {
            if (text.gameObject.name != "NumberText" ||
                text.transform.parent == null ||
                text.transform.parent.name != rowName)
            {
                continue;
            }

            return text;
        }

        return null;
    }

    private bool HasAllIndividualRows()
    {
        return handCountText != null &&
               mouthCountText != null &&
               noseCountText != null &&
               earCountText != null;
    }

    private void CreateFallbackText()
    {
        if (fallbackSummaryText != null)
            return;

        Canvas[] activeCanvases =
            FindObjectsByType<Canvas>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
        Canvas canvas = activeCanvases.Length > 0
            ? activeCanvases[0]
            : CreateFallbackCanvas();

        if (canvas == null)
        {
            Debug.LogWarning(
                "Body-part summary could not find a Canvas.",
                this);
            return;
        }

        GameObject textObject =
            new(
                "BodyPartSummaryText",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
        textObject.transform.SetParent(
            canvas.transform,
            false);

        RectTransform rect =
            textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, -100f);
        rect.sizeDelta = new Vector2(520f, 220f);

        TextMeshProUGUI text =
            textObject.GetComponent<TextMeshProUGUI>();
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 30f;
        text.color = Color.white;
        fallbackSummaryText = text;
    }

    private static Canvas CreateFallbackCanvas()
    {
        GameObject canvasObject =
            new(
                "Black Market Summary Canvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler =
            canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode =
            CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution =
            new Vector2(1920f, 1080f);

        return canvas;
    }

    private string BuildFallbackSummary()
    {
        return
            "BLACK MARKET BODY PARTS\n" +
            $"Hand  x {summary.GetTotalCount(BodyPartType.Hand)}\n" +
            $"Mouth x {summary.GetTotalCount(BodyPartType.Mouth)}\n" +
            $"Nose  x {summary.GetTotalCount(BodyPartType.Nose)}\n" +
            $"Ear   x {summary.GetTotalCount(BodyPartType.Ear)}";
    }

    private static void SetCount(
        TMP_Text target,
        int count)
    {
        if (target != null)
            target.text = $"x {count}";
    }
}
