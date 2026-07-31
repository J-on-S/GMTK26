using UnityEngine;

public class DoctorAccepting : MonoBehaviour, IInteractable {

  private ToolRequestManager manager;
  public PlayerHitSound angerSound;

  void Start() {
    manager = FindFirstObjectByType<ToolRequestManager>();
    if (manager == null)
      Debug.LogError("Can't find a ToolRequestManager");
  }

  public void Interact(Interactor player){
    if (player.heldObject == null) return; // no object
    //TODO: Change to Type
    bool deliveryAccepted = ReceiveItem(player.heldObject.item);
    if (deliveryAccepted){
      // remove from inventory if item is accepted
      player.heldObject.StartRespawnTimer();
      Debug.Log("Item accepted. Inventory cleared.");
    } else {
        // just keep the item otherwise -- would need to drop it
        Debug.Log($"The doctor rejected the {player.heldObject.item.Name}.");
        angerSound.playAudio();
    }
  }
  
  // returns boolean indicating if the item was correctly received
  public bool ReceiveItem(Item receivedItem) {
    if (manager != null){
      // only respawn once the item is given to the doctor
      // check its correct item
      return manager.PlayerSubmittedTool(receivedItem);
    }
    return false;
  }
}
