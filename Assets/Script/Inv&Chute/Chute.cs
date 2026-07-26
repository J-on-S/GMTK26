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
    [SerializeField] private BlackMarketGenerator temporaryBlackMarketTaskGenerator;
    [SerializeField] private CameraSwitch cameraSwitch;
    [SerializeField] private ReturnMain returnMain;
    
    [Serializable] public class PartSoldEvent : UnityEvent<BodyPart> {}
    [Header("Events")]
    [SerializeField] private PartSoldEvent onPartSold = new PartSoldEvent();
    //[SerializeField] private BodyPartType
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
        CheckBlackMarket();
        Destroy(part.gameObject);
    }

    public void Interact(Interactor player)
    {
        if(!player.heldObject) {
            CheckBlackMarket();
            return;
        }
        GrabbableObject grabbableObject = player.heldObject;
        GameObject grabbableGO = grabbableObject.gameObject;

        DetachedBodyPart detachedBodyPart = grabbableGO.GetComponent<DetachedBodyPart>();
        
        if (detachedBodyPart == null) {
            temporaryBlackMarketTaskGenerator.AddBodyPartInBlackMarket(grabbableObject.bodyPartType);
            CheckBlackMarket();
            player.heldObject = null;
            Destroy(grabbableGO);
            return;
        }
        temporaryBlackMarketTaskGenerator.AddBodyPartInBlackMarket(detachedBodyPart.GetBodyPartType());
        SellPart(detachedBodyPart);
        player.heldObject = null;
    }
    private void CheckBlackMarket()
    {
        cameraSwitch.SwitchToOtherCamera();
        returnMain.enabled = true;
    }
}
