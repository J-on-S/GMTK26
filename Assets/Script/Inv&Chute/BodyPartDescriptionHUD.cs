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
    private GrabbableObject _selected;

    /// <summary>Whether the current target carries health worth a bar. A tool has none, so the slider is switched off and the HUD shows only the name.</summary>
    private bool _showHealth;

    /// <summary>Owner of a raw-text line (e.g. a hovered CuttableObject showing its required tool), and the line itself. Kept separate from <see cref="_selected"/>: the text has no GrabbableObject behind it.</summary>
    private object _textOwner;
    private string _text;

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
            Debug.LogError($"{name}: no bodyPartDescriptionText assigned; the hovered item cannot be named.", this);
        }

        if (bodyPartHealthSlider == null)
        {
            Debug.LogError($"{name}: no bodyPartHealthSlider assigned; a hovered part cannot show its health.", this);
        }
    }

    private void Update()
    {
        if (_visible)
        {
            if (bodyPartDescriptionText != null)
            {
                // a GrabbableObject's name, or the raw requirement line when there is no object behind it
                bodyPartDescriptionText.text = _selected != null ? _selected.DisplayName : _text;
            }

            if (bodyPartHealthSlider != null)
            {
                // off for a tool (no health), on for a part -- so the same HUD names both but only bars the part.
                bodyPartHealthSlider.gameObject.SetActive(_showHealth);
                if (_showHealth && _selected is DetachedBodyPart part)
                {
                    bodyPartHealthSlider.value = part.health / part.maxHealth;
                }
            }
        }

        if (_animator == null) return;
        _animator.SetBool(Visible, _visible);
    }

    /// <summary>Shows a part's name and its decay bar.</summary>
    public void ShowBodyPartDescription(DetachedBodyPart bodyPart)
    {
        _selected = bodyPart;
        _text = null;
        _textOwner = null;
        _showHealth = true;
        _visible = true;
    }

    /// <summary>Shows just an item's name, with the health bar switched off -- for tools and anything with no health.</summary>
    public void ShowName(GrabbableObject item)
    {
        _selected = item;
        _text = null;
        _textOwner = null;
        _showHealth = false;
        _visible = true;
    }

    /// <summary>Shows a raw line with no object behind it, e.g. a hovered cut's required tool. No health bar.</summary>
    public void ShowText(object owner, string text)
    {
        _selected = null;
        _textOwner = owner;
        _text = text;
        _showHealth = false;
        _visible = true;
    }

    /// <summary>Hides a raw line, but only if <paramref name="owner"/> is the one that showed it, so a stale hide cannot blank a line the player already moved onto.</summary>
    public void HideText(object owner)
    {
        if (!ReferenceEquals(_textOwner, owner)) return;
        _textOwner = null;
        _text = null;
        _visible = false;
    }

    /// <summary>Hides the HUD, but only if <paramref name="item"/> is the one currently shown, so a stale hide from an item the player already left cannot blank the one they moved onto.</summary>
    public void HideDescription(GrabbableObject item)
    {
        if (_selected != item) return;
        _selected = null;
        _visible = false;
    }

    /// <summary>Typed overload kept for existing part callers.</summary>
    public void HideBodyPartDescription(DetachedBodyPart bodyPart)
    {
        HideDescription(bodyPart);
    }
}
