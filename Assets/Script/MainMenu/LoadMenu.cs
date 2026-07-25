using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LoadMenu : MonoBehaviour
{
    [SerializeField] private string mainScene = "MainScene";
    [SerializeField] private Button[] dayButtons;

    void Start()
    {
        SceneManager.LoadScene(mainScene);
    }
}
