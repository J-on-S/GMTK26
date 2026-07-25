using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class Chute : MonoBehaviour
{
    [SerializeField] private float transitionDuration = 3.0f;
    [SerializeField] private Transform enterPosition;
    [SerializeField] private Transform exitPosition;

    [SerializeField] private AudioEventChannel audioEventChannel;
    [SerializeField] private Audio dropSoundEffect;
    
    [Serializable] public class PartSoldEvent : UnityEvent {}
    [Header("Events")]
    [SerializeField] private PartSoldEvent onPartSold = new PartSoldEvent();
    public PartSoldEvent OnPartSold => onPartSold;

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
        OnPartSold.Invoke();
    }
}
