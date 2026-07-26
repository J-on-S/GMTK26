using UnityEngine;

public class Interactor : MonoBehaviour
{
  public float maxRange = 3f;
  public GrabbableObject heldObject = null; // null or 1 object
  public Transform holdPoint;

  Camera cam;
  void Awake(){ //search for camera once
    cam = Camera.main;
    if (cam == null){
      Debug.LogError($"{name}: no camera tagged MainCamera, so nothing can be interacted with.", this);
    }
    if (holdPoint == null){
      Debug.LogError($"{name}: no holdPoint assigned; grabbed objects would be parented to the scene root instead of the hand.", this);
    }
  }
  public void ResetHeldObj()
  {
    if (heldObject == null){
      Debug.LogError($"{name}: ResetHeldObj called with empty hands.", this);
      return;
    }
    Destroy(heldObject.gameObject);
    heldObject = null;
  }

  void Update() {
    if (cam == null) return;

    if (heldObject != null && Input.GetKeyDown(KeyCode.E)){
      heldObject.Drop();
      heldObject = null;
    }
    Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
    RaycastHit hit;
    if (Physics.Raycast(ray, out hit, maxRange)){
      // GetComponent just get or null
      // TryGetComponent if there smth put in variable
      // check if transform of object that we look on is interactable
      if (hit.transform.TryGetComponent<IInteractable>(out var interactable)
        && Input.GetMouseButtonDown(0)){
        interactable.Interact(this);
      }
    }
  }
}
