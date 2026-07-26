using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Displays the persistent combined chute + fridge counts on result screens.
/// Each assigned TMP field contains only "x {count}"; the row image identifies
/// its body-part type.
/// </summary>
public class BlackMarketBodyPartSummaryUI : MonoBehaviour
{
    [SerializeField] private TMP_Text handCountText;
    [SerializeField] private TMP_Text legCountText;
    [SerializeField] private TMP_Text noseCountText;
    [SerializeField] private TMP_Text earCountText;

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
        {
            Debug.LogWarning(
                "BlackMarketBodyPartSummaryUI could not find every Hand, " +
                "Leg, Nose, and Ear NumberText. Assign the missing TMP " +
                "fields in this result scene.",
                this);
        }

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
            legCountText,
            summary.GetTotalCount(BodyPartType.Leg));
        SetCount(
            noseCountText,
            summary.GetTotalCount(BodyPartType.Nose));
        SetCount(
            earCountText,
            summary.GetTotalCount(BodyPartType.Ear));
    }

    private void AutoBindExistingRows()
    {
        TMP_Text[] allText =
            FindObjectsByType<TMP_Text>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

        DisableUnusedRow(
            allText,
            "Mouth WinLoseStatRow");

        handCountText ??=
            FindRowCount(allText, "Hand WinLoseStatRow");
        legCountText ??=
            FindRowCount(allText, "Leg WinLoseStatRow") ??
            FindRowCount(allText, "Foot WinLoseStatRow");
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

    private static void DisableUnusedRow(
        TMP_Text[] allText,
        string rowName)
    {
        foreach (TMP_Text text in allText)
        {
            Transform row = text.transform.parent;
            if (row == null || row.name != rowName)
                continue;

            row.gameObject.SetActive(false);
            return;
        }
    }

    private bool HasAllIndividualRows()
    {
        return handCountText != null &&
               legCountText != null &&
               noseCountText != null &&
               earCountText != null;
    }

    private static void SetCount(
        TMP_Text target,
        int count)
    {
        if (target != null)
            target.text = $"x {count}";
    }
}
