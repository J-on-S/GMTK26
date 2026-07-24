using UnityEngine;

public class InteractionManager : MonoBehaviour
{ 
  void Update() {
    Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
    RaycastHit hit;
    if (Physics.Raycast(ray, out hit)){
      var selectionTransform = hit.transform; // transform of object that we look on
      if (selectionTransform.GetComponent<GrabbableObject>()){

      }
    }
  }
}
