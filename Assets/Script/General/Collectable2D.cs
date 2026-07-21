using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>
/// Pickup that flies to a tagged 2D player and reports the collection through <c>onCollected</c>.
/// </summary>
/// <remarks>
/// Invariant: collection happens at most once — the trigger collider is disabled the moment it starts.
/// Invariant: nothing is collected while a <c>wallLayer</c> collider sits between pickup and player.
/// </remarks>
[RequireComponent(typeof(CircleCollider2D))]
public class Collectable2D : MonoBehaviour
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
    [Tooltip("Optional. Tint applied to the SpriteRenderer while the player is in range.")]
    [SerializeField] private bool highlightInRange = true;
    [SerializeField] private Color highlightColor = Color.red;

    [Header("Events")]
    [Tooltip("Raised once, passing the collecting player, right before this object is destroyed.")]
    public UnityEvent<GameObject> onCollected;
    [Tooltip("If true, destroy this GameObject after onCollected. Turn off to pool/reuse.")]
    [SerializeField] private bool destroyOnCollect = true;

    private CircleCollider2D col;
    private SpriteRenderer sr;
    private Color baseColor = Color.white;

    private GameObject playerInRange;
    private bool isCollecting;

    private void Awake()
    {
        col = GetComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = collectRadius;

        sr = GetComponent<SpriteRenderer>();
        if (sr != null) baseColor = sr.color;
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

    /// <summary>Collects the pickup on demand instead of waiting for <c>collectAutomatically</c>.</summary>
    /// <returns><c>true</c> if collection started; <c>false</c> when no player is in range, a wall
    /// blocks the line of sight, or collection is already under way.</returns>
    public bool TryCollect()
    {
        if (playerInRange == null || isCollecting) return false;
        if (IsWallBetween(playerInRange)) return false;

        StartCollect(playerInRange);
        return true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag)) return;

        playerInRange = other.gameObject;
        if (highlightInRange && sr != null) sr.color = highlightColor;
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag)) return;
        if (other.gameObject != playerInRange) return;

        playerInRange = null;
        if (highlightInRange && sr != null) sr.color = baseColor;
    }

    private bool IsWallBetween(GameObject player)
    {
        if (wallLayer.value == 0) return false;

        Vector2 origin = transform.position;
        Vector2 target = player.transform.position;
        Vector2 dir = target - origin;
        float dist = dir.magnitude;
        if (dist <= Mathf.Epsilon) return false;

        return Physics2D.Raycast(origin, dir.normalized, dist, wallLayer).collider != null;
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

            Vector2 target = player.transform.position;
            transform.position = Vector2.MoveTowards(transform.position, target, moveSpeed * Time.deltaTime);

            if (((Vector2)transform.position - target).sqrMagnitude <= 0.0001f)
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
        col = GetComponent<CircleCollider2D>();
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
