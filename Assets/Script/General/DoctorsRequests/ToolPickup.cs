using UnityEngine;
using System.Collections;
using System.ComponentModel;


// currently request is fulfilled just by clicking on item need to change so you are handing the item to the doctor to fulfill the order 

public class ToolPickup : MonoBehaviour
{

    //tool stuff
    public string itemName;
    public float respawnTime = 5f;
    public ToolRequestManager.ItemType itemType;


    private Collider col;
    private Renderer rend;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        col = GetComponent<Collider>();
        rend = GetComponent<Renderer>();
    }


    //// this should be elsewhere probably with the rest of the controls
    //private void OnMouseDown()
    //{
    //    PickupTool();
    //}
    public void OnItemCollected()
    {
        SetToolVisible(false);
        StartCoroutine(RespawnRoutine());
    }

    //void PickupTool()  // this should be changed to pickup and hand to doctor
    //{
    //    ToolRequestManager manager = FindFirstObjectByType<ToolRequestManager>();
    //    if (manager != null)
    //    {
    //        manager.PlayerSubmittedTool(toolName);
    //    }

    //    // dont destroy tool -- want infinite supply
    //    SetToolVisible(false);
    //    StartCoroutine(RespawnRoutine());
    //}

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
