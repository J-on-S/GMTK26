using UnityEngine;

public class GameManagement: MonoBehaviour
{
    public static GameManagement Instance { get; private set; }
    public BodyParts bodyParts;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }
}