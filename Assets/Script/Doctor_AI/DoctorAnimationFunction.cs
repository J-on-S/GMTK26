using UnityEngine;

public class DoctorAnimationFunction : MonoBehaviour
{
    [SerializeField] private CheckState checkState;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    public void FinishCheckAnimation()
    {
        checkState.SetFinishCheck();
    }
    public void GetCheckLooping()
    {
        checkState.SetGetCheckLooping();
    }
    
}
