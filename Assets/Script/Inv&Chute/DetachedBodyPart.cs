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
        _material =  GetComponent<MeshRenderer>().material;
        _bodyPartDescriptionHUD = BodyPartDescriptionHUD.LastActiveInstance;
    }

    private void Update()
    {
        health = Math.Clamp(health - Time.deltaTime, 0, maxHealth);
        var c = _material.color;
        _material.color = new Color(c.r, c.g, c.b, health / maxHealth);
        if (health <= 0)
        {
            _material.color = new Color(255, c.g, c.b, 0.7f);
        }
    }

    private void OnMouseEnter()
    {
        _bodyPartDescriptionHUD.ShowBodyPartDescription(this);
    }
    
    private void OnMouseExit()
    {
        _bodyPartDescriptionHUD.HideBodyPartDescription();
    }

    public void Interact(Interactor player)
    {
        if (fridge != null && !fridge.TryEvictItemFromFridge(this))
        {
            Debug.LogWarning("Failed to eject self from fridge: Am I not in a fridge?");
        }
    }

    /// <param name="preset">Grab/drop sounds for the piece. Added here rather than left to the inspector because
    /// the component is created at runtime on a mesh the slicer just made, so there is nobody to author it on.</param>
    public static DetachedBodyPart MakeDetachedBodyPart(float startingHealth, float maxHealth, BodyPart bodyPart, GameObject gameObject, AudioGrappablePreset preset)
    {
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
