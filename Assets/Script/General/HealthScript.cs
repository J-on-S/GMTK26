using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class HealthScript : MonoBehaviour
{
    public static HealthScript Instance { get; private set; }

    public static int HP = 3;

    [SerializeField] private Image[] healthImages; // Size = 3
    [SerializeField] private Sprite fullHeartSprite;
    [SerializeField] private Sprite brokenHeartSprite;
    [SerializeField] private int waitSecond = 1;
    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);

        UpdateHealthUI();
    }
    [ContextMenu("TakeDamage")]
    public void TestTakeDamage()
    {
        TakeDamage();
    }
    public void TakeDamage(int amount = 1)
    {
        HP = Mathf.Max(0, HP - amount);
        UpdateHealthUI();
        if(HP <= 0)
        {
            StartCoroutine(WaitBeforeSwitchScene());
        }
    }
    
    IEnumerator WaitBeforeSwitchScene()
    {
        yield return new WaitForSeconds(waitSecond);
        SceneManager.LoadScene("Scenes/Menu/Lost");
    }

    private void UpdateHealthUI()
    {
        for (int i = 0; i < healthImages.Length; i++)
        {
            healthImages[i].sprite = i < HP
                ? fullHeartSprite
                : brokenHeartSprite;
        }
    }
}