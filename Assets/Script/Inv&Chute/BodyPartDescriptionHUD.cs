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
    private DetachedBodyPart _selectedBodyPart;

    private void OnEnable()
    {
        LastActiveInstance = this;
        _animator = GetComponent<Animator>();

        if (_animator == null)
        {
            Debug.LogError($"{name}: no Animator, so the body part HUD can never be shown or hidden.", this);
        }

        if (bodyPartDescriptionText == null)
        {
            Debug.LogError($"{name}: no bodyPartDescriptionText assigned; the hovered part cannot be named.", this);
        }

        if (bodyPartHealthSlider == null)
        {
            Debug.LogError($"{name}: no bodyPartHealthSlider assigned; the hovered part cannot show its health.", this);
        }
    }

    private void Update()
    {
        if (_visible && _selectedBodyPart != null)
        {
            if (bodyPartDescriptionText != null)
            {
                bodyPartDescriptionText.text = _selectedBodyPart.gameObject.name;
            }

            if (bodyPartHealthSlider != null)
            {
                bodyPartHealthSlider.value = _selectedBodyPart.health / _selectedBodyPart.maxHealth;
            }
        }

        if (_animator == null) return;
        _animator.SetBool(Visible, _visible);
    }

    public void ShowBodyPartDescription(DetachedBodyPart bodyPart)
    {
        _selectedBodyPart = bodyPart;
        _visible = true;
    }
    
    public void HideBodyPartDescription(DetachedBodyPart bodyPart)
    {
        if (_selectedBodyPart != bodyPart) return;
        _selectedBodyPart = null;
        _visible = false;
    }
}
