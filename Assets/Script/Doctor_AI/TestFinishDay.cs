using UnityEngine;
using UnityEngine.SceneManagement;

public class TestFinishDay : MonoBehaviour
{
    [SerializeField] private TemporaryBlackMarketTaskGenerator temporaryBlackMarketTaskGenerator;

    [ContextMenu("Finish Day")]
    public void FinishDay()
    {
        bool succeed = IsEnoughBPForBlackMarket();
        if (succeed)
        {
            Debug.Log("suceed!");
            SceneManager.LoadScene("Scenes/Win");
        }
        else
        {
            Debug.Log("loose!");
            SceneManager.LoadScene("Scenes/Loose");
        }
    }
    public bool IsEnoughBPForBlackMarket()
    {
        //TODO: ADD BODY PART IN BLACK MARKET
        //PUT BODY PART IN FRIDGE
        temporaryBlackMarketTaskGenerator.IsSucceedBlackMarket();
        return temporaryBlackMarketTaskGenerator;
    }
}
