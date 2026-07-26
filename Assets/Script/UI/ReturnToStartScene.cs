using UnityEngine;
using UnityEngine.SceneManagement;

public class ReturnToStartScene : MonoBehaviour
{
    public void GoToStartScene()
    {
        SceneManager.LoadScene("StartScreen");
    }
}
