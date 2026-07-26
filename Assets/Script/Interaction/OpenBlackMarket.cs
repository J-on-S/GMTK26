using UnityEngine;

public class OpenBlackMarket : MonoBehaviour, IInteractable
{
  public bool UIvisible;
  public GameObject UImarket;
  public void Interact(Interactor player){
    UImarket.SetActive(true);
    UIvisible = true;
    Cursor.lockState = CursorLockMode.None;
    Time.timeScale = 0f;
  }
  public void notVisible(){
    UImarket.SetActive(false);
    UIvisible = false;
    Cursor.lockState = CursorLockMode.Locked;
    Time.timeScale = 1f;
  }
}
