using UnityEngine;

public class PlayRandomAudio : MonoBehaviour
{
    public AudioEventChannel EventChannel;
    public Audio[] Clips;
    public bool PlayOnStart;

    public void Start() 
    {
       if (PlayOnStart) PlayAudio();
    }

public void Test() 
{

}
    public void PlayAudio() 
    {
        if (Clips.Length <= 0 || EventChannel == null) 
        {
            Debug.LogError("Clips or EventChannel is not assigned");
            return;
        }

        EventChannel.Play(Clips[Random.Range(0, Clips.Length-1)]);
    }

}