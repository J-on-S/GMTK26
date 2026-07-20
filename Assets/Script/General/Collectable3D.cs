using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>
/// Universal 3D collectable. Same design as <see cref="Collectable2D"/> but uses
/// 3D physics (SphereCollider, Physics.Raycast) and a generic Renderer for the
/// highlight. Game-agnostic: raises <see cref="onCollected"/> so any project can
/// react in the Inspector or via code.
///
/// Collection modes:
///  - Automatic: collects as soon as the player is in range (and no wall blocks).
///  - Manual: call <see cref="TryCollect"/> from anywhere, or bind an optional
///    InputAction (collectAction) that fires while the player is in range.
/// </summary>
[RequireComponent(typeof(SphereCollider))]
public class Collectable3D : MonoBehaviour
{
    [Header("Detection")]
    [Tooltip("Tag the player object (the one carrying the collider) must have.")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private float collectRadius = 1.5f;

    [Header("Collection")]
    [SerializeField] private bool collectAutomatically = false;
    [Tooltip("Optional. If set, a wall on this layer between collectable and player blocks auto-collect.")]
    [SerializeField] private LayerMask wallLayer;
    [Tooltip("Optional. Manual-collect input. Fires only while player is in range.")]
    [SerializeField] private InputActionReference collectAction;

    [Header("Movement")]
    [Tooltip("Fly speed toward the player, in units/second. 0 = instant snap.")]
    [SerializeField] private float moveSpeed = 8f;

    [Header("Feedback")]
    [Tooltip("Optional. Tint applied to the Renderer's material while the player is in range.")]
    [SerializeField] private bool highlightInRange = true;
    [SerializeField] private Color highlightColor = Color.red;

    [Header("Events")]
    [Tooltip("Raised once, passing the collecting player, right before this object is destroyed.")]
    public UnityEvent<GameObject> onCollected;
    [Tooltip("If true, destroy this GameObject after onCollected. Turn off to pool/reuse.")]
    [SerializeField] private bool destroyOnCollect = true;

    private SphereCollider col;
    private Renderer rend;
    private Color baseColor = Color.white;
    private bool hasColor;

    private GameObject playerInRange;
    private bool isCollecting;

    private void Awake()
    {
        col = GetComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = collectRadius;

        rend = GetComponent<Renderer>();
        if (rend != null && rend.material.HasProperty("_Color"))
        {
            hasColor = true;
            baseColor = rend.material.color;
        }
    }

    private void OnEnable()
    {
        if (collectAction != null && collectAction.action != null)
        {
            collectAction.action.performed += OnCollectInput;
            collectAction.action.Enable();
        }
    }

    private void OnDisable()
    {
        if (collectAction != null && collectAction.action != null)
            collectAction.action.performed -= OnCollectInput;
    }

    private void Update()
    {
        if (playerInRange == null || isCollecting) return;

        if (collectAutomatically && !IsWallBetween(playerInRange))
            StartCollect(playerInRange);
    }

    private void OnCollectInput(InputAction.CallbackContext _)
    {
        TryCollect();
    }

    /// <summary>Manual collect. Safe to call from any script; no-op if not collectable now.</summary>
    public bool TryCollect()
    {
        if (playerInRange == null || isCollecting) return false;
        if (IsWallBetween(playerInRange)) return false;

        StartCollect(playerInRange);
        return true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;

        playerInRange = other.gameObject;
        SetColor(highlightColor);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;
        if (other.gameObject != playerInRange) return;

        playerInRange = null;
        SetColor(baseColor);
    }

    private void SetColor(Color c)
    {
        if (highlightInRange && hasColor) rend.material.color = c;
    }

    private bool IsWallBetween(GameObject player)
    {
        if (wallLayer.value == 0) return false;

        Vector3 origin = transform.position;
        Vector3 dir = player.transform.position - origin;
        float dist = dir.magnitude;
        if (dist <= Mathf.Epsilon) return false;

        return Physics.Raycast(origin, dir.normalized, dist, wallLayer);
    }

    private void StartCollect(GameObject player)
    {
        isCollecting = true;
        col.enabled = false;
        StartCoroutine(MoveTowardsPlayer(player));
    }

    private IEnumerator MoveTowardsPlayer(GameObject player)
    {
        // Speed <= 0 means instant.
        while (moveSpeed > 0f)
        {
            if (player == null) { Finish(null); yield break; }

            Vector3 target = player.transform.position;
            transform.position = Vector3.MoveTowards(transform.position, target, moveSpeed * Time.deltaTime);

            if ((transform.position - target).sqrMagnitude <= 0.0001f)
                break;

            yield return null;
        }

        if (player != null)
            transform.position = player.transform.position;

        Finish(player);
    }

    private void Finish(GameObject player)
    {
        onCollected?.Invoke(player);
        if (destroyOnCollect) Destroy(gameObject);
        else isCollecting = false;
    }

    private void OnValidate()
    {
        col = GetComponent<SphereCollider>();
        if (col != null) col.radius = collectRadius;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, collectRadius * transform.lossyScale.x);
    }
#endif
}
