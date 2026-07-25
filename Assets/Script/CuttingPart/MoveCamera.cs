
using UnityEngine;
using System.Collections;
using UnityEngine.InputSystem;

public class MoveCamera  :  MonoBehaviour {

    public float speedH = 2.0f;
    public float speedV = 2.0f;

    private float yaw = 0.0f;
    private float pitch = 0.0f;

    public Camera c;
    private MeshRenderer highlighted;
    private Color originalColor;

    void Start() {
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
        yaw = c.transform.eulerAngles.y;
        pitch = c.transform.eulerAngles.x;
    }

    void Update() {

        Vector2 move = CuttingManager.mouseDelta.ReadValue<Vector2>();
        yaw += speedH * move.x;
        pitch -= speedV * move.y;

        c.transform.eulerAngles = new Vector3(pitch, yaw, 0.0f);

        CheckStartMinigame();
    }

    void CheckStartMinigame()
    {
        bool pressed = Mouse.current.leftButton.isPressed;

        // aim is the screen centre: the cursor is locked, so a mouse position carries no information.
        Ray ray = c.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        MeshRenderer hitRenderer = null;

        if (Physics.Raycast(ray, out RaycastHit hit)
            && hit.collider.TryGetComponent(out CuttingManager cm)
            && cm.canEnterMinigame())
        {
            hit.collider.TryGetComponent(out hitRenderer);

            if (pressed)
            {
                // drop the tint before handing over: this script stops updating during the cut.
                Highlight(null);
                cm.EnterMinigame();
                return;
            }
        }

        // null when aiming at nothing, or at something that isn't an enterable cut: clears the tint.
        Highlight(hitRenderer);
    }

    /// <summary>Tints the aimed-at cuttable red, restoring the colour of the one it replaces. Pass null to clear.</summary>
    void Highlight(MeshRenderer target)
    {
        if (target == highlighted) return;

        if (highlighted != null)
        {
            highlighted.material.color = originalColor;
        }

        if (target != null)
        {
            originalColor = target.material.color;
            target.material.color = Color.red;
        }

        highlighted = target;
    }
}
