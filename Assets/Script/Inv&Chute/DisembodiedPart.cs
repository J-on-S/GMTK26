using System;
using UnityEngine;

[DisallowMultipleComponent]
public class BodyPartHealth : MonoBehaviour
{
    [SerializeField] public float maxHealth = 100.0f;
    [SerializeField] public float health = 100.0f;
    
    private BodyPartDescriptionHUD _bodyPartDescriptionHUD;
    private Material _material;
    
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

    public static BodyPartHealth AttachBodyPartHealth(float startingHealth, float maxHealth, GameObject gameObject)
    {
        var bodyPart = gameObject.GetComponent<BodyPartHealth>();
        
        if (bodyPart == null)
        {
            bodyPart = gameObject.AddComponent<BodyPartHealth>();
        }
        
        bodyPart.health = startingHealth;
        bodyPart.maxHealth = maxHealth;
        return bodyPart;
    }
}
