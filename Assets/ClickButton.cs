using UnityEngine;

public class ClickButton : MonoBehaviour
{
  public AudioEventChannel channel;
  public Audio clickSound;

  public void makeSound(){
    channel.Play(clickSound);
  }
}
