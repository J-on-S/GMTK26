using System;
using UnityEngine;

public class DisembodiedPart : MonoBehaviour
{
    [SerializeField] public float maxHealth = 100.0f;
    [SerializeField] public float health = 100.0f;
    [SerializeField] private BodyPartDescriptionHUD bodyPartDescriptionHUD;

    private Material _material;

    private void OnEnable()
    {
        _material =  GetComponent<MeshRenderer>().material;
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
        bodyPartDescriptionHUD.ShowBodyPartDescription(this);
    }
    
    private void OnMouseExit()
    {
        bodyPartDescriptionHUD.HideBodyPartDescription();
    }
}
