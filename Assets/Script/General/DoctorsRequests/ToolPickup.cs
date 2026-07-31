using UnityEngine;
using System.Collections;

public class ToolPickup : MonoBehaviour
{

    public float respawnTime = 4f;   // should be same or less than the cooldown time
    public Tool tool;


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

    // respawn items after a certain amount of time
    IEnumerator RespawnRoutine()
    {
        yield return new WaitForSeconds(respawnTime);
        SetToolVisible(true);

    }

    // make the item visible again (respawn it essentially)
    void SetToolVisible(bool isVisible)
    {
        if (col != null) col.enabled = isVisible;
        if (rend != null) rend.enabled = isVisible;
    }
}
