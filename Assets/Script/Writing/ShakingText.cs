using UnityEngine;
using TMPro;

public class ShakingText : MonoBehaviour
{
    [Header("Wiggle Settings")]
    [SerializeField] private float wiggleSpeed = 10f;
    [SerializeField] private float wiggleStrength = 5f;

    private TMP_Text textComponent;
    private TMP_TextInfo textInfo;

    void Awake()
    {
        textComponent = GetComponent<TMP_Text>();
    }

    void Update()
    {
        // Force the text to update its internal mesh data structure
        textComponent.ForceMeshUpdate();
        textInfo = textComponent.textInfo;

        // Loop through every individual character in the text string
        for (int i = 0; i < textInfo.characterCount; i++)
        {
            TMP_CharacterInfo charInfo = textInfo.characterInfo[i];

            // Skip characters that don't have a visible mesh (like spaces)
            if (!charInfo.isVisible) 
                continue;

            // Get the index of the material used by this character
            int materialIndex = charInfo.materialReferenceIndex;

            // Get the index of the first vertex for this character
            int vertexIndex = charInfo.vertexIndex;

            // Grab the array containing all vertex positions for this submesh
            Vector3[] vertices = textInfo.meshInfo[materialIndex].vertices;

            // Generate an offset using independent Perlin noise for random, smooth shaking
            // Multiplying 'i' prevents all letters from shaking in identical unison
            float offsetX = (Mathf.PerlinNoise(Time.time * wiggleSpeed, i * 100f) - 0.5f) * wiggleStrength;
            float offsetY = (Mathf.PerlinNoise(i * 100f, Time.time * wiggleSpeed) - 0.5f) * wiggleStrength;
            Vector3 offset = new Vector3(offsetX, offsetY, 0);

            // Apply the unique offset to all 4 corners of the character's quad mesh
            vertices[vertexIndex + 0] += offset; // Bottom Left
            vertices[vertexIndex + 1] += offset; // Top Left
            vertices[vertexIndex + 2] += offset; // Top Right
            vertices[vertexIndex + 3] += offset; // Bottom Right
        }

        // Push the modified vertex data back into the text meshes to render the frame
        for (int i = 0; i < textInfo.meshInfo.Length; i++)
        {
            textInfo.meshInfo[i].mesh.vertices = textInfo.meshInfo[i].vertices;
            textComponent.UpdateGeometry(textInfo.meshInfo[i].mesh, i);
        }
    }
}