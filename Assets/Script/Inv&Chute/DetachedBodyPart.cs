using System;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class DetachedBodyPart : GrabbableObject, IHoverable
{
    [SerializeField] public BodyPart bodyPart;
    [SerializeField] public float maxHealth = 100.0f;
    [SerializeField] public float health = 100.0f;
    [SerializeField] public Fridge fridge;
    private BodyPartDescriptionHUD _bodyPartDescriptionHUD;
    private Material _material;
    
    [Serializable] public class PartPickupEvent : UnityEvent {}
    [Header("Events")]
    [SerializeField] private PartPickupEvent onPartPickup = new PartPickupEvent();
    public PartPickupEvent OnPartPickup => onPartPickup;
    
    private void Start()
    {
        if (TryGetComponent<MeshRenderer>(out var meshRenderer))
        {
            _material = meshRenderer.material;
        }
        else
        {
            Debug.LogError($"{name}: no MeshRenderer, so the part cannot show its decay.", this);
        }

        _bodyPartDescriptionHUD = BodyPartDescriptionHUD.LastActiveInstance;
        if (_bodyPartDescriptionHUD == null)
        {
            Debug.LogError($"{name}: no BodyPartDescriptionHUD in the scene, so hovering this part shows nothing.", this);
        }

        if (bodyPart == null)
        {
            Debug.LogError($"{name}: no BodyPart assigned; selling or requesting this part cannot identify it.", this);
        }
    }
    public BodyPartType GetBodyPartType()
    {
        return bodyPart.BodyPartType;
    } 

    private void Update()
    {
        var multiplier = fridge == null ? 1.0f : 0.5f;
        health = Math.Clamp(health - Time.deltaTime * multiplier, 0, maxHealth);
        var c = _material.color;
        _material.color = new Color(c.r, c.g, c.b, health / maxHealth);
        if (health <= 0)
        {
            _material.color = new Color(255, c.g, c.b, 0.7f);
        }
    }

    private void OnMouseEnter()
    {
        if (_bodyPartDescriptionHUD == null) return;
        _bodyPartDescriptionHUD.ShowBodyPartDescription(this);
    }

    private void OnMouseExit()
    {
        if (_bodyPartDescriptionHUD == null) return;
        _bodyPartDescriptionHUD.HideBodyPartDescription(this);
    }

    /// <summary>Takes the part out of the fridge, if it is in one, then grabs it like any other object.</summary>
    /// <remarks>Invariant: the fridge is left alone when the player's hands are full. The base grab is a
    /// no-op in that case, so evicting first would strand the part loose in the room with nobody holding it.</remarks>
    public override void Interact(Interactor player)
    {
        if (player.heldObject != null) return;

        if (fridge != null && !fridge.TryEvictItemFromFridge(this))
        {
            Debug.LogWarning("Failed to eject self from fridge: Am I not in a fridge?");
            return;
        }

        base.Interact(player);
    }
    
    private bool hovering;

    public void HoverOver(Interactor player)
    {
        _bodyPartDescriptionHUD.ShowBodyPartDescription(this);
        hovering = true;
    }

    private void LateUpdate()
    {
        if (!hovering)
        {
            _bodyPartDescriptionHUD.HideBodyPartDescription(this);
        }

        hovering = false;
    }

    /// <param name="itemName">What the piece is called. Names the GameObject and fills GrabbableObject.itemName,
    /// so the doctor's requests and the black market can match it by name. Left empty, the object keeps
    /// the name the slice gave it.</param>
    public static DetachedBodyPart MakeDetachedBodyPart(float startingHealth, float maxHealth, BodyPart bodyPart, GameObject gameObject, AudioGrappablePreset preset, string itemName = null)
    {
        if (!gameObject.TryGetComponent<Rigidbody>(out _))
        {
            gameObject.AddComponent<Rigidbody>();
        }

        var detachedBodyPart = gameObject.GetComponent<DetachedBodyPart>();

        if (detachedBodyPart == null)
        {
            detachedBodyPart = gameObject.AddComponent<DetachedBodyPart>();
        }

        detachedBodyPart.health = startingHealth;
        detachedBodyPart.maxHealth = maxHealth;
        detachedBodyPart.bodyPart = bodyPart;
        detachedBodyPart.itemType = ItemType.BodyPart;
        detachedBodyPart.audioPreset = preset;

        if (!string.IsNullOrWhiteSpace(itemName))
        {
            detachedBodyPart.itemName = itemName;
            gameObject.name = itemName;
        }

        return detachedBodyPart;
    }
    
}
