using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

public class TestFinishDay : MonoBehaviour
{
    [FormerlySerializedAs("temporaryBlackMarketTaskGenerator")]
    [SerializeField] private BlackMarketGenerator blackMarketGenerator;
    [SerializeField] private GameplayManager gameplayManager;
    [SerializeField] private string winScene =
        "Scenes/Menu/Win";
    [SerializeField] private string loseScene =
        "Scenes/Menu/Lost";

    private Coroutine pendingSceneLoad;

    private void OnEnable()
    {
        ResolveGameplayManager();
        if (gameplayManager != null)
        {
            gameplayManager.BlackMarketTaskResolved +=
                HandleBlackMarketTaskResolved;
        }
    }

    private void OnDisable()
    {
        if (gameplayManager != null)
        {
            gameplayManager.BlackMarketTaskResolved -=
                HandleBlackMarketTaskResolved;
        }
    }

    [ContextMenu("Finish Day")]
    public void FinishDay()
    {
        QueueResultScene(IsEnoughBPForBlackMarket());
    }

    private void HandleBlackMarketTaskResolved(bool succeeded)
    {
        QueueResultScene(succeeded);
    }

    private void QueueResultScene(bool succeeded)
    {
        if (pendingSceneLoad != null)
            return;

        Debug.Log(
            succeeded
                ? "Black-market task succeeded. Loading Win."
                : "Black-market task failed. Loading Lost.",
            this);

        // Wait one frame so GameplayManager can finish changing state and
        // publishing its remaining end-of-day events before the scene unloads.
        pendingSceneLoad = StartCoroutine(
            LoadResultSceneNextFrame(succeeded));
    }

    private IEnumerator LoadResultSceneNextFrame(bool succeeded)
    {
        yield return null;

        string resultScene = succeeded ? winScene : loseScene;
        if (string.IsNullOrWhiteSpace(resultScene))
        {
            Debug.LogError(
                "TestFinishDay has no result scene configured.",
                this);
            pendingSceneLoad = null;
            yield break;
        }

        SceneManager.LoadScene(resultScene);
    }

    private void ResolveGameplayManager()
    {
        if (gameplayManager == null)
            gameplayManager =
                FindFirstObjectByType<GameplayManager>();

        if (gameplayManager == null)
        {
            Debug.LogError(
                "TestFinishDay cannot find GameplayManager, so it cannot " +
                "listen for the black-market result.",
                this);
        }
    }

    public bool IsEnoughBPForBlackMarket()
    {
        //TODO: ADD BODY PART IN BLACK MARKET
        //PUT BODY PART IN FRIDGE
        return blackMarketGenerator != null &&
               blackMarketGenerator.IsSucceedBlackMarket();
    }
}
