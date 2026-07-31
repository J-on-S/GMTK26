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

        if (BodyPartDescriptionHUD.LastActiveInstance == null)
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
    public float GetCurrentHealth()
    {
        return health;
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

    /// <summary>A part shows its name and its decay bar, so it overrides the base name-only HUD.</summary>
    protected override void ShowHudDescription()
    {
        BodyPartDescriptionHUD hud = BodyPartDescriptionHUD.LastActiveInstance;
        if (hud != null) hud.ShowBodyPartDescription(this);
    }

    /// <param name="bodyPart">What the piece IS. It carries the identity the rest of the game matches on and
    /// the name the piece takes; a null one leaves the object with whatever name the slice gave it.</param>
    /// <remarks>The name is no longer passed in alongside the part. A caller-supplied string could disagree
    /// with the asset every consumer actually reads -- <see cref="GrabbableObject.DisplayName"/> resolves
    /// through <c>item</c>, which is this same asset -- so the object ended up labelled one thing and
    /// described as another.</remarks>
    public static DetachedBodyPart MakeDetachedBodyPart(float startingHealth, float maxHealth, BodyPart bodyPart, GameObject gameObject, AudioGrappablePreset preset)
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
        detachedBodyPart.item = bodyPart;
        if (bodyPart == null)
        {
            Debug.LogError("bodypart null");
        }
        detachedBodyPart.audioPreset = preset;

        if (bodyPart != null)
        {
            gameObject.name = bodyPart.DisplayName;
        }

        return detachedBodyPart;
    }
    
}
