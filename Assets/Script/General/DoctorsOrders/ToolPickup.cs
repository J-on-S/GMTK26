using UnityEngine;
using System.Collections;

public class ToolPickup : MonoBehaviour
{

    //tool stuff
    public string toolName;
    public float respawnTime = 5f;

    private Collider col;
    private Renderer rend;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        col = GetComponent<Collider>();
        rend = GetComponent<Renderer>();
    }


    // this should be elsewhere probably with the rest of the controls
    private void OnMouseDown()
    {
        PickupTool();
    }

    // Update is called once per frame
    void PickupTool()
    {
        ToolRequestManager manager = FindFirstObjectByType<ToolRequestManager>();
        if (manager != null)
        {
            manager.PlayerSubmittedTool(toolName);
        }

        // dont destroy tool -- want infinite supply
        SetToolVisible(false);
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
