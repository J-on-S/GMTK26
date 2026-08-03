using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class HealthScript : MonoBehaviour
{
    //public static HealthScript Instance { get; private set; }

    //public static int HP = 3;

    //[SerializeField] private int maxHP = 3;

    //[SerializeField] private Image[] healthImages; // Size = 3
    //[SerializeField] private Sprite fullHeartSprite;
    //[SerializeField] private Sprite brokenHeartSprite;
    //[SerializeField] private int waitSecond = 1;
    //[Header("Test")]
    //[SerializeField] private bool testInifiteLife = false;

    //private bool isDying;

    //public int MaxHP => Mathf.Max(1, maxHP);

    //private void Awake()
    //{
    //    if (Instance != null && Instance != this)
    //    {
    //        Destroy(gameObject);
    //        return;
    //    }

    //    Instance = this;
    //    ResetHealth();
    //}

    //private void OnDestroy()
    //{
    //    if (Instance == this)
    //        Instance = null;
    //}

    //[ContextMenu("TakeDamage")]
    //public void TestTakeDamage()
    //{
    //    TakeDamage();
    //}

    //[ContextMenu("ResetHealth")]
    //public void ResetHealth()
    //{
    //    isDying = false;
    //    HP = MaxHP;
    //    UpdateHealthUI();
    //}

    //public void TakeDamage(int amount = 1)
    //{
    //    if (testInifiteLife || isDying) return;

    //    HP = Mathf.Clamp(HP - amount, 0, MaxHP);
    //    UpdateHealthUI();

    //    if (HP <= 0)
    //    {
    //        isDying = true;
    //        StartCoroutine(WaitBeforeSwitchScene());
    //    }
    //}

    //private void OnEnable()
    //{
    //    SceneManager.activeSceneChanged += ReplenishHP;
    //}

    //private void OnDisable()
    //{
    //    SceneManager.activeSceneChanged -= ReplenishHP;
    //}

    //private void ReplenishHP(Scene from, Scene to)
    //{
    //    if (to == gameObject.scene)
    //        ResetHealth();
    //}

    //IEnumerator WaitBeforeSwitchScene()
    //{
    //    yield return new WaitForSecondsRealtime(waitSecond);
    //    Time.timeScale = 1f;
    //    SceneManager.LoadScene("Scenes/Menu/Lost");
    //}

    //private void UpdateHealthUI()
    //{
    //    for (int i = 0; i < healthImages.Length; i++)
    //    {
    //        if (healthImages[i] == null) continue;

    //        healthImages[i].sprite = i < HP
    //            ? fullHeartSprite
    //            : brokenHeartSprite;
    //    }
    //}
    public static HealthScript Instance { get; private set; }

    public static int HP = 10;  // effectively a current HP
    [SerializeField] private TextMeshProUGUI healthNumber;

    [SerializeField] private int maxHP = 10;

    //[SerializeField] private Sprite fullHeartSprite; 
    [SerializeField] private int waitSecond = 1;
    [Header("Test")]
    [SerializeField] private bool testInifiteLife = false;

    private bool isDying;

    public int MaxHP => Mathf.Max(1, maxHP);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        ResetHealth();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    [ContextMenu("TakeDamage")]
    public void TestTakeDamage()
    {
        TakeDamage();
    }

    [ContextMenu("ResetHealth")]
    public void ResetHealth()
    {
        isDying = false;
        HP = MaxHP;
        UpdateHealthUI();
    }

    public void TakeDamage(int amount = 1)
    {
        if (testInifiteLife || isDying) return;

        HP = Mathf.Clamp(HP - amount, 0, MaxHP);
        Debug.Log("take damage " + HP);
        UpdateHealthUI();

        if (HP <= 0)
        {
            isDying = true;
            StartCoroutine(WaitBeforeSwitchScene());
        }
    }

    private void OnEnable()
    {
        SceneManager.activeSceneChanged += ReplenishHP;
    }

    private void OnDisable()
    {
        SceneManager.activeSceneChanged -= ReplenishHP;
    }

    private void ReplenishHP(Scene from, Scene to)
    {
        if (to == gameObject.scene)
            ResetHealth();
    }

    IEnumerator WaitBeforeSwitchScene()
    {
        yield return new WaitForSecondsRealtime(waitSecond);
        Time.timeScale = 1f;
        SceneManager.LoadScene("Scenes/Menu/Lost");
    }


    private void UpdateHealthUI()
    {
        if (healthNumber != null)
        {
            healthNumber.text = HP.ToString();
        }
    }
}
