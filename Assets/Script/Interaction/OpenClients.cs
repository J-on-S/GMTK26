using UnityEngine;

public class OpenClients : MonoBehaviour, IInteractable
{
  public GameObject page1;
  public GameObject page2;

  public MouseMovement mouseMov;

  public AudioEventChannel channel;
  public Audio paperAudio;
  private GenerateClientInfoUI generateClientInfoUI;
  public void Start()
  {
      generateClientInfoUI = GetComponent<GenerateClientInfoUI>();
  }
  public void Interact(Interactor player){
    Debug.Log("Book Interact called"); 
    page1.SetActive(true);
    Cursor.lockState = CursorLockMode.None;
    Time.timeScale = 0f;
    mouseMov.Pause();
    channel.Play(paperAudio);
    generateClientInfoUI.GenerateCards();
  }
  public void notVisible(){
    page1.SetActive(false);
    page2.SetActive(false);
    Cursor.lockState = CursorLockMode.Locked;
    Time.timeScale = 1f;
    mouseMov.Unpause();
    channel.Play(paperAudio);
  }
  public void nextPage1(){
    channel.Play(paperAudio);
    page1.SetActive(false);
    page2.SetActive(true);
  }
  public void backPage2(){
    channel.Play(paperAudio);
    page2.SetActive(false);
    page1.SetActive(true);
  }
}
