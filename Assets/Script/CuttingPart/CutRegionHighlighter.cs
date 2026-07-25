using UnityEngine;

/// <summary>Tints the piece a cut would remove, and only that piece.</summary>
/// <remarks>
/// Draws the actual severed mesh -- the one <see cref="CuttingManager.SeveredPreviewMesh"/> gets by
/// running the real slice -- as an overlay over the body. So the highlight is the piece, exactly:
/// bounded by the cut's finite window and by mesh connectivity, not by an infinite plane.
/// <para>
/// An overlay rather than a material swap on the body: the body keeps whatever lit shader it
/// already uses, and turning the highlight on or off cannot disturb its normal appearance. Colour
/// goes through a <see cref="MaterialPropertyBlock"/>, so every body in the scene shares one
/// material and no instances leak.
/// </para>
/// </remarks>
public class CutRegionHighlighter : MonoBehaviour
{
    /// <summary>Shader path; the material is built from it on first use.</summary>
    private const string ShaderName = "Cutting/CutRegionHighlight";

    private static readonly int ColorId = Shader.PropertyToID("_HighlightColor");

    /// <summary>One material for every highlighter in the scene; all variation rides on property blocks.</summary>
    private static Material sharedHighlightMaterial;

    /// <summary>Child renderer that draws the tint. Created on demand.</summary>
    private MeshRenderer overlayRenderer;
    private MeshFilter overlayFilter;
    private MaterialPropertyBlock block;

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

        if (overlayFilter.sharedMesh != severedMesh)
        {
            overlayFilter.sharedMesh = severedMesh;
            SyncMaterialSlots(severedMesh);
        }

        block ??= new MaterialPropertyBlock();
        overlayRenderer.GetPropertyBlock(block);
        block.SetColor(ColorId, color);
        overlayRenderer.SetPropertyBlock(block);

        overlayRenderer.enabled = true;
    }

    /// <summary>Turns the tint off. Safe to call when it is already off.</summary>
    public void Hide()
    {
        if (overlayRenderer != null)
        {
            overlayRenderer.enabled = false;
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

        Material material = HighlightMaterial();
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

    /// <summary>Gives the renderer one material per submesh.</summary>
    /// <remarks>A severed piece carries the body's skin submeshes plus a cap; a renderer with fewer materials than submeshes silently drops the extra ones, so half the highlight would go missing.</remarks>
    private void SyncMaterialSlots(Mesh mesh)
    {
        Material material = HighlightMaterial();
        if (material == null)
        {
            return;
        }

        int count = Mathf.Max(1, mesh.subMeshCount);
        if (overlayRenderer.sharedMaterials.Length == count)
        {
            return;
        }

        var slots = new Material[count];
        for (int i = 0; i < count; i++)
        {
            slots[i] = material;
        }
        overlayRenderer.sharedMaterials = slots;
    }

    /// <summary>The shared highlight material, built on first use. Null with an error when the shader is missing from the build.</summary>
    private static Material HighlightMaterial()
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
