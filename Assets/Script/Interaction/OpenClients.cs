using UnityEngine;

public class OpenClients : MonoBehaviour, IInteractable
{
  public GameObject page1;
  public GameObject page2;
  public MouseMovement mouseMov;
  public void Interact(Interactor player){
    Debug.Log("Book Interact called"); 
    page1.SetActive(true);
    Cursor.lockState = CursorLockMode.None;
    Time.timeScale = 0f;
    mouseMov.Pause();
  }
  public void notVisible(){
    page1.SetActive(false);
    page2.SetActive(false);
    Cursor.lockState = CursorLockMode.Locked;
    Time.timeScale = 1f;
    mouseMov.Unpause();
  }
}
