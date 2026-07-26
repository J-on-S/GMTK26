using UnityEngine;

public class OpenBlackMarket : MonoBehaviour, IInteractable
{
  public GameObject UImarket;
  public void Interact(Interactor player){
    UImarket.SetActive(true);
  }
}
