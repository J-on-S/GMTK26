using UnityEngine;

public class GrabbableObject : MonoBehaviour, IInteractable{
  private Rigidbody rb;
  void Awake(){
    rb = GetComponent<Rigidbody>();
  }
  //GRAB
  public void Interact(Interactor player){
    if (player.heldObject == null){ 
      // make an object as a child for holdPoint
      Debug.Log("player holdp"+player.holdPoint);
      transform.parent = player.holdPoint;

      // local means relative to the parent
      this.transform.localPosition = Vector3.zero; // centre of holdPoint
      this.transform.localRotation = Quaternion.identity; //without rotation
      rb.isKinematic = true; // no physics for object;
      player.heldObject = this;
    }
  }
  // DROP
  public void Drop(){
    // remove parenting
    transform.parent = null;
    rb.isKinematic = false; // no physics for object;
  }
}
