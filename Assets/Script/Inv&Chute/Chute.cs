using System.Collections;
using UnityEngine;

public class Chute : MonoBehaviour
{
    [SerializeField] private float transitionDuration = 3.0f;
    [SerializeField] private Transform enterPosition;
    [SerializeField] private Transform exitPosition;

    [SerializeField] private AudioEventChannel audioEventChannel;
    [SerializeField] private Audio dropSoundEffect;

    public IEnumerator SellPart(GameObject part)
    {
        audioEventChannel.Play(dropSoundEffect);
        var startTime = Time.time;
        while (Time.time - startTime < transitionDuration)
        {
            part.transform.position = Vector3.Lerp(
                enterPosition.position, exitPosition.position, 
                (Time.time - startTime) / transitionDuration);
            yield return new WaitForEndOfFrame();
        }
    }
}
