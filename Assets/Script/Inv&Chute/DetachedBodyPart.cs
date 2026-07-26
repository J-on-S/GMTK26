using System;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class DetachedBodyPart : GrabbableObject
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
        health = Math.Clamp(health - Time.deltaTime, 0, maxHealth);
        if (_material == null) return;

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
        _bodyPartDescriptionHUD.HideBodyPartDescription();
    }

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
        detachedBodyPart.itemType = ItemType.BodyPart;
        detachedBodyPart.audioPreset = preset;
        return detachedBodyPart;
    }
    
}
