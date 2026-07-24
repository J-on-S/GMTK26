using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BodyPartDescriptionHUD : MonoBehaviour
{
    /// <summary>
    /// Whether there's useful information on the HUD to be shown.
    /// The animator deals with timing and whatnot.
    /// </summary>
    private static readonly int Visible = Animator.StringToHash("Visible");
    
    /// <summary>
    /// Instance of HUD active in the current scene.
    /// </summary>
    public static BodyPartDescriptionHUD LastActiveInstance;
    
    [SerializeField] private TextMeshProUGUI bodyPartDescriptionText;
    [SerializeField] private Slider bodyPartHealthSlider;
    private Animator _animator;
    private bool _visible;
    private DisembodiedPart _selectedBodyPart;

    private void OnEnable()
    {
        LastActiveInstance = this;
        _animator = GetComponent<Animator>();
    }

    private void Update()
    {
        if (_visible)
        {
            bodyPartDescriptionText.text = _selectedBodyPart.gameObject.name;
            bodyPartHealthSlider.value = _selectedBodyPart.health / _selectedBodyPart.maxHealth;
        }
        _animator.SetBool(Visible, _visible);
    }

    public void ShowBodyPartDescription(DisembodiedPart bodyPart)
    {
        _selectedBodyPart = bodyPart;
        _visible = true;
    }
    
    public void HideBodyPartDescription()
    {
        _selectedBodyPart = null;
        _visible = false;
    }
}
