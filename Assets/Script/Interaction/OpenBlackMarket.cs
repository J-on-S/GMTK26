using UnityEngine;

public class OpenBlackMarket : MonoBehaviour, IInteractable
{
  public bool UIvisible;
  public GameObject UImarket;
  void Awake(){
    if (UImarket == null){
      Debug.LogError($"{name}: no UImarket assigned; the black market UI can never open, and interacting would freeze the game with no way back.", this);
    }
  }
  public void Interact(Interactor player){
    if (UImarket == null) return;
    UImarket.SetActive(true);
    UIvisible = true;
    Cursor.lockState = CursorLockMode.None;
    Time.timeScale = 0f;
  }
  public void notVisible(){
    if (UImarket == null) return;
    UImarket.SetActive(false);
    UIvisible = false;
    Cursor.lockState = CursorLockMode.Locked;
    Time.timeScale = 1f;
  }
}
