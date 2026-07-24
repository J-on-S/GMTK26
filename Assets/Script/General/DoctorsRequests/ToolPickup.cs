using UnityEngine;
using System.Collections;
using System.ComponentModel;


// currently request is fulfilled just by clicking on item need to change so you are handing the item to the doctor to fulfill the order 

public class ToolPickup : MonoBehaviour
{

    //tool stuff
    public string itemName;
    public float respawnTime = 4f;   // should be same or less than the cooldown time
    public ToolRequestManager.ItemType itemType;


    private Collider col;
    private Renderer rend;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        col = GetComponent<Collider>();
        rend = GetComponent<Renderer>();
    }

    // this is called when the player picks up the object
    public void OnItemCollected()
    {
        SetToolVisible(false);  // hide the tool when it is picked up
       // StartCoroutine(RespawnRoutine());
    }

    // this is called when the doctor gets the right item delivered
    public void StartRespawnTimer()
    {
        StartCoroutine(RespawnRoutine());
    }

    IEnumerator RespawnRoutine()
    {
        yield return new WaitForSeconds(respawnTime);
        SetToolVisible(true);

    }

    void SetToolVisible(bool isVisible)
    {
        if (col != null) col.enabled = isVisible;
        if (rend != null) rend.enabled = isVisible;
    }
}
