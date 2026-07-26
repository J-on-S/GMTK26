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

    private void Awake()
    {
        if (enterPosition == null)
            Debug.LogError($"{name}: Chute has no enterPosition assigned; parts cannot travel down it.", this);
        if (exitPosition == null)
            Debug.LogError($"{name}: Chute has no exitPosition assigned; parts cannot travel down it.", this);
        if (audioEventChannel == null)
            Debug.LogError($"{name}: Chute has no AudioEventChannel assigned; the drop sound will not play.", this);
        if (dropSoundEffect == null)
            Debug.LogError($"{name}: Chute has no dropSoundEffect assigned; the drop sound will not play.", this);
        if (temporaryBlackMarketTaskGenerator == null)
            Debug.LogError($"{name}: Chute has no BlackMarketGenerator assigned; sold parts will not be registered.", this);
        if (cameraSwitch == null)
            Debug.LogError($"{name}: Chute has no CameraSwitch assigned; the black market view will not open.", this);
        if (returnMain == null)
            Debug.LogError($"{name}: Chute has no ReturnMain assigned; the player will not be able to leave the black market view.", this);
    }

    public IEnumerator SellPart(DetachedBodyPart part)
    {
        if (part == null)
        {
            Debug.LogError($"{name}: asked to sell a null body part.", this);
            yield break;
        }

        if (enterPosition == null || exitPosition == null)
        {
            Debug.LogError($"{name}: cannot send {part.name} down the chute, enterPosition/exitPosition are not assigned.", this);
            yield break;
        }

        if (audioEventChannel != null && dropSoundEffect != null)
        {
            audioEventChannel.Play(dropSoundEffect);
        }

        part.ReleaseFromHolder();
        part.transform.SetParent(null, true);
        part.SetCollidersEnabled(false);

        var startTime = Time.time;
        while (Time.time - startTime < transitionDuration)
        {
            if (part == null) yield break;

            part.transform.position = Vector3.Lerp(
                enterPosition.position, exitPosition.position,
                (Time.time - startTime) / transitionDuration);
            yield return new WaitForEndOfFrame();
        }

        if (part.bodyPart == null)
        {
            Debug.LogError($"{name}: sold {part.name} but it carries no BodyPart, so listeners are told nothing was sold.", this);
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
            AddToBlackMarket(grabbableObject.bodyPartType);
            CheckBlackMarket();
            grabbableObject.ReleaseFromHolder();
            Destroy(grabbableGO);
            return;
        }

        if (detachedBodyPart.bodyPart == null)
        {
            Debug.LogError($"{name}: {detachedBodyPart.name} has no BodyPart assigned; it cannot be registered on the black market.", this);
        }
        else
        {
            AddToBlackMarket(detachedBodyPart.GetBodyPartType());
        }

        StartCoroutine(SellPart(detachedBodyPart));
    }

    private void AddToBlackMarket(BodyPartType bodyPartType)
    {
        if (temporaryBlackMarketTaskGenerator == null)
        {
            Debug.LogError($"{name}: no BlackMarketGenerator assigned, so the {bodyPartType} sale is lost.", this);
            return;
        }

        temporaryBlackMarketTaskGenerator.AddBodyPartInBlackMarket(bodyPartType);
    }

    private void CheckBlackMarket()
    {
        if (cameraSwitch == null)
        {
            Debug.LogError($"{name}: no CameraSwitch assigned, so the black market view cannot be opened.", this);
            return;
        }

        cameraSwitch.SwitchToOtherCamera();

        if (returnMain == null)
        {
            Debug.LogError($"{name}: no ReturnMain assigned, so the player cannot leave the black market view.", this);
            return;
        }

        returnMain.enabled = true;
    }
}
