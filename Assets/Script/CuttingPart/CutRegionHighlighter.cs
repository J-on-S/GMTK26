using UnityEngine;

/// <summary>Tints the piece a cut would remove, and only that piece.</summary>
/// <remarks>
/// Draws the actual severed mesh -- the one <see cref="CuttingManager.SeveredPreviewMesh"/> gets by
/// running the real slice -- as an overlay over the body. So the highlight is the piece, exactly:
/// bounded by the cut's finite window and by mesh connectivity, not by an infinite plane.
/// <para>
/// An overlay rather than a material swap on the body: the body keeps whatever lit shader it
/// already uses, and turning the highlight on or off cannot disturb its normal appearance. Colour
/// goes through a <see cref="MaterialPropertyBlock"/>, so a material is shared by every body that
/// uses it and no instances leak.
/// </para>
/// <para>
/// Which material draws it is the body's call -- <see cref="CuttableObject.highlightMaterial"/> --
/// so an outline/overlay material authored in the project can be used as-is; the colour lands in
/// its <c>OutlineColor</c>. Bodies that leave it empty fall back to the built-in shader below, so
/// scenes authored before the field existed look the same.
/// </para>
/// </remarks>
public class CutRegionHighlighter : MonoBehaviour
{
    /// <summary>Shader path for the fallback material, built from it on first use.</summary>
    private const string ShaderName = "Cutting/CutRegionHighlight";

    /// <summary>Colour of the built-in fallback shader.</summary>
    private static readonly int HighlightColorId = Shader.PropertyToID("_HighlightColor");

    // Both spellings: a hand-written shader prefixes its properties, a Shader Graph property named
    // "OutlineColor" is exposed unprefixed unless its reference was overridden. Which one a given
    // material answers to is not knowable from here, so ask the material.
    private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
    private static readonly int OutlineColorIdUnprefixed = Shader.PropertyToID("OutlineColor");

    /// <summary>Fallback material, shared by every body that has not been given one.</summary>
    private static Material sharedHighlightMaterial;

    /// <summary>Child renderer that draws the tint. Created on demand.</summary>
    private MeshRenderer overlayRenderer;
    private MeshFilter overlayFilter;
    private MaterialPropertyBlock block;

    /// <summary>Body this highlighter belongs to; it owns the material choice.</summary>
    private CuttableObject body;

    /// <summary>Material currently on the overlay, and the colour property resolved for it. Re-resolved only when the material changes, so a swap in the inspector is picked up without a per-frame HasProperty probe.</summary>
    private Material activeMaterial;
    private int activeColorId = HighlightColorId;

    /// <summary>Lights <paramref name="severedMesh"/> in <paramref name="color"/>. The mesh must be in this object's local space, which is where a slice produces it.</summary>
    public void Show(Mesh severedMesh, Color color)
    {
        if (severedMesh == null)
        {
            Hide();
            return;
        }

        EnsureOverlay();
        if (overlayRenderer == null)
        {
            return;
        }

        overlayFilter.sharedMesh = severedMesh;

        // every Show, not only on a mesh change: this also picks up a material swapped on the body.
        SyncMaterialSlots(severedMesh);

        block ??= new MaterialPropertyBlock();
        overlayRenderer.GetPropertyBlock(block);
        block.SetColor(activeColorId, color);
        overlayRenderer.SetPropertyBlock(block);

        overlayRenderer.enabled = true;
    }

    /// <summary>Turns the tint off. Safe to call when it is already off.</summary>
    public void Hide()
    {
        if (overlayRenderer != null)
        {
            overlayRenderer.enabled = false;
            overlayRenderer.transform.localPosition = Vector3.zero;
        }
    }

    /// <summary>Displaces the highlight from the body, in the body's local space.</summary>
    /// <remarks>Invariant: the mesh is untouched, so an offset costs nothing per frame and <see cref="Hide"/> undoes it exactly.</remarks>
    public void SetOffset(Vector3 localOffset)
    {
        if (overlayRenderer != null)
        {
            overlayRenderer.transform.localPosition = localOffset;
        }
    }

    private void OnDisable()
    {
        Hide();
    }

    /// <summary>Builds the overlay child the first time it is needed.</summary>
    private void EnsureOverlay()
    {
        if (overlayRenderer != null)
        {
            return;
        }

        Material material = ResolveMaterial();
        if (material == null)
        {
            return;
        }

        // HideAndDontSave: this is presentation rebuilt on demand, and it must not be saved into
        // the scene nor survive as a stray child if the body is duplicated.
        GameObject overlay = new GameObject("~CutRegionHighlight")
        {
            hideFlags = HideFlags.HideAndDontSave
        };

        // identity local transform: a severed piece mesh is in the body's local space, so the
        // overlay renders it in place with no offset.
        overlay.transform.SetParent(transform, false);

        overlayFilter = overlay.AddComponent<MeshFilter>();
        overlayRenderer = overlay.AddComponent<MeshRenderer>();
        overlayRenderer.sharedMaterial = material;
        overlayRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        overlayRenderer.receiveShadows = false;
        overlayRenderer.enabled = false;
    }

    /// <summary>Gives the renderer one material per submesh, re-resolving the material and its colour property when the body's choice changed.</summary>
    /// <remarks>A severed piece carries the body's skin submeshes plus a cap; a renderer with fewer materials than submeshes silently drops the extra ones, so half the highlight would go missing.</remarks>
    private void SyncMaterialSlots(Mesh mesh)
    {
        Material material = ResolveMaterial();
        if (material == null)
        {
            return;
        }

        if (material != activeMaterial)
        {
            activeMaterial = material;
            activeColorId = ColorPropertyOf(material);
        }

        int count = Mathf.Max(1, mesh.subMeshCount);
        Material[] slots = overlayRenderer.sharedMaterials;
        if (slots.Length == count && slots.Length > 0 && slots[0] == material)
        {
            return;
        }

        slots = new Material[count];
        for (int i = 0; i < count; i++)
        {
            slots[i] = material;
        }
        overlayRenderer.sharedMaterials = slots;
    }

    /// <summary>The material this body highlights with: its own if it has one, else the built-in fallback.</summary>
    private Material ResolveMaterial()
    {
        if (body == null)
        {
            TryGetComponent(out body);
        }

        Material authored = body != null ? body.highlightMaterial : null;
        return authored != null ? authored : FallbackMaterial();
    }

    /// <summary>Which colour property <paramref name="material"/> answers to, asked once per material rather than per frame.</summary>
    /// <remarks>Setting a property a shader hasn't got is a silent no-op, which would read as "the highlight is broken" with nothing in the console -- so an unrecognised material says so once, here.</remarks>
    private static int ColorPropertyOf(Material material)
    {
        if (material.HasProperty(OutlineColorId)) return OutlineColorId;
        if (material.HasProperty(OutlineColorIdUnprefixed)) return OutlineColorIdUnprefixed;
        if (material.HasProperty(HighlightColorId)) return HighlightColorId;

        Debug.LogWarning($"Highlight material '{material.name}' has no OutlineColor (or _OutlineColor) property, so the cut highlight will draw in whatever colour the material is set to.");
        return OutlineColorId;
    }

    /// <summary>The shared fallback material, built on first use. Null with an error when the shader is missing from the build.</summary>
    private static Material FallbackMaterial()
    {
        if (sharedHighlightMaterial != null)
        {
            return sharedHighlightMaterial;
        }

        Shader shader = Shader.Find(ShaderName);
        if (shader == null)
        {
            Debug.LogError($"Shader '{ShaderName}' not found. Cut region highlighting is off. If this only happens in a build, add the shader to Project Settings > Graphics > Always Included Shaders.");
            return null;
        }

        sharedHighlightMaterial = new Material(shader) { name = "CutRegionHighlight (shared)" };
        return sharedHighlightMaterial;
    }

    /// <summary>Gets the highlighter on a body, adding one if it hasn't got one.</summary>
    public static CutRegionHighlighter For(CuttableObject body)
    {
        if (body == null)
        {
            return null;
        }
        if (!body.TryGetComponent(out CutRegionHighlighter highlighter))
        {
            highlighter = body.gameObject.AddComponent<CutRegionHighlighter>();
        }
        return highlighter;
    }
}
