using UnityEngine;
using System.Collections;

public class GrabbableObject : MonoBehaviour, IInteractable{
  public string itemName;
  public ItemType itemType;
  public BodyPartType bodyPartType;
  public float respawnTime = 3f;

  [Tooltip("Channel and clips this object plays on grab and drop. When set, it wins over the four fields below; they stay as the fallback so objects placed before presets existed keep sounding the same.")]
  public AudioGrappablePreset audioPreset;

  public AudioEventChannel channel;
  public Audio metalPickupAudio;
  public Audio clothPickupAudio;
  public Audio metalDropAudio;
  public Audio clothDropAudio;

  private Vector3 initialPosition;
  private Quaternion initialRotation;

  private Rigidbody rb;
  private Interactor holder;
  private Coroutine respawnRoutine;

  void Awake(){
    rb = GetComponent<Rigidbody>();
    SetStartPose(transform.position, transform.rotation);
  }

  public void SetStartPose(Vector3 position, Quaternion rotation){
    initialPosition = position;
    initialRotation = rotation;
  }

  //GRAB
  public virtual void Interact(Interactor player){
    if (player.heldObject == null){
      // make an object as a child for holdPoint
      Debug.Log("player holdp"+player.holdPoint);
      PlayPickupSound();
      transform.SetParent(player.holdPoint, true);

      // local means relative to the parent
      this.transform.localPosition = Vector3.zero; // centre of holdPoint
      this.transform.localRotation = Quaternion.identity; //without rotation
      EnsureRigidbody().isKinematic = true; // no physics for object;
      SetCollidersEnabled(false);
      player.heldObject = this;
      holder = player;
    }
  }

  private void PlayPickupSound(){
    if (audioPreset != null){
      audioPreset.PlayPickup(itemType);
      return;
    }
    if (channel == null) return;
    channel.Play(itemType == ItemType.Tool ? metalPickupAudio : clothPickupAudio);
  }

  private void PlayDropSound(){
    if (audioPreset != null){
      audioPreset.PlayDrop(itemType);
      return;
    }
    if (channel == null) return;
    channel.Play(itemType == ItemType.Tool ? metalDropAudio : clothDropAudio);
  }

  private Rigidbody EnsureRigidbody(){
    if (rb == null && !TryGetComponent(out rb)){
      rb = gameObject.AddComponent<Rigidbody>();
    }
    return rb;
  }

  private void GoDynamic(){
    Rigidbody body = EnsureRigidbody();
    body.isKinematic = false;
    body.linearVelocity = Vector3.zero;
    body.angularVelocity = Vector3.zero;
  }

  public void SetCollidersEnabled(bool enabled){
    Collider[] colliders = GetComponentsInChildren<Collider>(true);
    for (int i = 0; i < colliders.Length; i++){
      colliders[i].enabled = enabled;
    }
  }

  private void SetRenderersEnabled(bool enabled){
    Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
    for (int i = 0; i < renderers.Length; i++){
      renderers[i].enabled = enabled;
    }
  }

  public void ReleaseFromHolder(){
    if (holder != null && holder.heldObject == this){
      holder.heldObject = null;
    }
    holder = null;
  }

  // DROP
  public void Drop(){
    PlayDropSound();
    ReleaseFromHolder();
    // remove parenting
    transform.parent = null;
    SetCollidersEnabled(true);
    GoDynamic(); // physics back on for object;
  }

  public void ReturnToStart(){
    ReleaseFromHolder();
    transform.parent = null;
    transform.position = initialPosition;
    transform.rotation = initialRotation;
    SetCollidersEnabled(true);
    GoDynamic(); // physics work back
  }

  // this is called when the doctor gets the right item delivered
  public void StartRespawnTimer(){
    if (respawnRoutine != null){
      StopCoroutine(respawnRoutine);
    }
    ReleaseFromHolder();
    respawnRoutine = StartCoroutine(RespawnRoutine());
  }

  // respawn items after a certain amount of time
  IEnumerator RespawnRoutine(){
    transform.parent = null;
    SetRenderersEnabled(false); // hide appearence
    SetCollidersEnabled(false); // hide physical
    yield return new WaitForSeconds(respawnTime);
    ReturnToStart();
    SetRenderersEnabled(true);
    respawnRoutine = null;
  }
}
