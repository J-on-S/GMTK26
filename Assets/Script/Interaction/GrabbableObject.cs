using UnityEngine;
using System.Collections;

public class GrabbableObject : MonoBehaviour, IInteractable{
  public string itemName;
  public ItemType itemType;
  public BodyPartType bodyPartType;
  public float respawnTime = 3f;

  public AudioEventChannel channel;
  public Audio metalPickupAudio;
  public Audio clothPickupAudio;

  private Vector3 initialPosition;
  private Quaternion initialRotation;

  private Rigidbody rb;
  void Awake(){
    rb = GetComponent<Rigidbody>();
    initialPosition = transform.position;
    initialRotation = transform.rotation;
  }
  //GRAB
  public void Interact(Interactor player){
    if (player.heldObject == null){ 
      // make an object as a child for holdPoint
      Debug.Log("player holdp"+player.holdPoint);
      transform.SetParent(player.holdPoint, true);

      // local means relative to the parent
      this.transform.localPosition = Vector3.zero; // centre of holdPoint
      this.transform.localRotation = Quaternion.identity; //without rotation
      rb.isKinematic = true; // no physics for object;
      player.heldObject = this;
      if (itemType == ItemType.Tool){
        channel.Play(metalPickupAudio);
      } else {
        channel.Play(clothPickupAudio);
      }
    }
  }
  // DROP
  public void Drop(){
    // remove parenting
    transform.parent = null;
    rb.isKinematic = false; // no physics for object;
  }
  public void ReturnToStart(){
    transform.parent = null;
    transform.position = initialPosition;
    transform.rotation = initialRotation;
    rb.isKinematic = false; // physics work back
  }

  // this is called when the doctor gets the right item delivered
  public void StartRespawnTimer(){
    StartCoroutine(RespawnRoutine());
  }

  // respawn items after a certain amount of time
  IEnumerator RespawnRoutine(){
    transform.parent = null;
    Renderer rend = GetComponent<Renderer>(); // hide appearence
    Collider coll = GetComponent<Collider>(); // hide physical
    rend.enabled = false;
    coll.enabled = false;
    yield return new WaitForSeconds(respawnTime);
    ReturnToStart();
    rend.enabled = true;
    coll.enabled = true;
  }
}
