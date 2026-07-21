using UnityEngine;
using TMPro;
public class WobblyText : MonoBehaviour
{
    public TMP_Text textComponent;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        textComponent.ForceMeshUpdate();
        var textInfo = textComponent.textInfo;

        for(int i=0; i<textInfo.characterCount; ++i)
        {
            var charInfo = textInfo.characterInfo[i];
            if (!charInfo.isVisible)
            {
                continue;
            }
            var meshInfo = textInfo.meshInfo[charInfo.materialReferenceIndex];
            for(int j = 0; j < 4; ++j)
            {
                var index = charInfo.vertexIndex + j;
                var orig = meshInfo.vertices[index];
                meshInfo.vertices[index] = orig + new Vector3(0, Mathf.Sin(Time.time*2f + orig.x*0.01f) * 10f, 0);
                
                //meshInfo.colors32[index] = Color.red;
            }
        }
        for(int i=0; i<textInfo.meshInfo.Length; i++)
        {
            var meshInfo = textInfo.meshInfo[i];
            meshInfo.mesh.vertices = meshInfo.vertices;
            //meshInfo.mesh.colors32 = meshInfo.colors32;
            textComponent.UpdateGeometry(meshInfo.mesh, i);
        }
    }
}
