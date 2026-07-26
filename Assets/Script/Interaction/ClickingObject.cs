using UnityEngine;

public class ClickingObject : MonoBehaviour, IInteractable{
  private Color initialColor; // no need of null
  public Color changeColor = Color.blue;
  private Color currentColor;

  private Renderer objRenderer;
  void Awake(){
    objRenderer = GetComponent<Renderer>();
    if (objRenderer == null){
      Debug.LogError($"{name}: no Renderer, so clicking this object can change nothing.", this);
      return;
    }
    initialColor = objRenderer.material.color;
  }
  //Doctor scrit
  public void Interact(Interactor player){
    //player.heldobj
    //heldObj information: type of it:
    //
    if (objRenderer == null) return;
    if (objRenderer.material.color == changeColor){
      objRenderer.material.color = initialColor;
    } else {
    objRenderer.material.color = changeColor;
    }
  }
}
