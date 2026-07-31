using UnityEngine;

public class Tutorial : MonoBehaviour
{
    public static Tutorial Instance { get; private set; }
    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }
    public void Start()
    {
        StartTutorial();
    }
    private void StartTutorial()
    {
        
    }
    public void FinishTutorial()
    {
        
    }
    
}
