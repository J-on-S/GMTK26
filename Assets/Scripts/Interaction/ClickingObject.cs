using UnityEngine;

public class ClickingObject : MonoBehaviour, IInteractable{
  private Color initialColor; // no need of null
  public Color changeColor = Color.blue;
  private Color currentColor;

  private Renderer objRenderer;
  void Awake(){
    objRenderer = GetComponent<Renderer>();
    initialColor = objRenderer.material.color;
  }
  
  public void Interact(Interactor player){
    if (objRenderer.material.color == changeColor){
      objRenderer.material.color = initialColor;
    } else {
    objRenderer.material.color = changeColor;
    }
  }
}
