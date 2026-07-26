using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class Chute : MonoBehaviour, IInteractable
{
    [SerializeField] private float transitionDuration = 3.0f;
    [SerializeField] private Transform enterPosition;
    [SerializeField] private Transform exitPosition;

    [SerializeField] private AudioEventChannel audioEventChannel;
    [SerializeField] private Audio dropSoundEffect;
    
    [Serializable] public class PartSoldEvent : UnityEvent<BodyPart> {}
    [Header("Events")]
    [SerializeField] private PartSoldEvent onPartSold = new PartSoldEvent();
    public PartSoldEvent OnPartSold => onPartSold;

    public IEnumerator SellPart(DetachedBodyPart part)
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
        OnPartSold.Invoke(part.bodyPart);
        Destroy(part.gameObject);
    }

    public void Interact(Interactor player)
    {
        var bodyPart = player.heldObject.GetComponent<DetachedBodyPart>();
        if (bodyPart == null) return;
        SellPart(bodyPart);
    }
}
