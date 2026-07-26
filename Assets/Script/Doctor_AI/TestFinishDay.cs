using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

public class TestFinishDay : MonoBehaviour
{
    [FormerlySerializedAs("temporaryBlackMarketTaskGenerator")]
    [SerializeField] private BlackMarketGenerator blackMarketGenerator;

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
        return blackMarketGenerator != null &&
               blackMarketGenerator.IsSucceedBlackMarket();
    }
}
