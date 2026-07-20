using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;
public class ConversationSystem : MonoBehaviour
{
    public static ConversationSystem Instance {get; private set;}
    [SerializeField] private TextMeshProUGUI text;
    private float currentTypingSpeed = 0.05f;
    [SerializeField] private float waitSecondAfterTyping = 3f;

    
    private Conversation currentConversation;
    private Dialogue_line currentDialogue_line;
    //private Coroutine applyEffectsCoroutine;
    private Coroutine checkForNextDialogueCoroutine;

    private void Start()
    {
        
    }
    private void Awake()
    {
        if(Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(this);
        }
        if (!text)
        {
            text = GetComponent<TextMeshProUGUI>();
        }
    }
    private bool isCurrentDialogueLineFinished = false;
    public IEnumerator TypeText()
    {
        NPC currentNPC = currentConversation.GetCurrentNPC();
        currentDialogue_line = currentConversation.GetCurrentDialogueLine();
        //string currentDialogue_text = 
        text.text = "";

        text.maxVisibleCharacters = 0;
        text.text = currentDialogue_line.GetDialogue_text();
        text.ForceMeshUpdate();
        int totalCharacters = text.textInfo.characterCount;

        isCurrentDialogueLineFinished = false;
        for (int i = 0; i <= totalCharacters; i++)
        {
            
            text.maxVisibleCharacters = i;
            yield return new WaitForSeconds(currentTypingSpeed);
        }
        isCurrentDialogueLineFinished = true;
        bool result = currentConversation.NextDialogueLine();
        if (!result)
        {
            FinishText();
        }
    }
    private void FinishText()
    {
        Debug.Log("Should have stop finishTxt");
        StopCoroutine(checkForNextDialogueCoroutine);
        //StopCoroutine(applyEffectsCoroutine);
        StartCoroutine(FinishTextCoroutine());
    }
    private IEnumerator FinishTextCoroutine()
    {
        yield return new WaitForSeconds(waitSecondAfterTyping);
        text.text = "";
        text.maxVisibleCharacters = 0;
        Debug.Log("Stop");
        //currentDayCanvas.SetActive(false);
        //Do Something
    }
    private void WriteText()
    {
        StartCoroutine(TypeText());
    }
    

    public void StartConversation(Conversation conversation)
    {
        currentConversation = conversation;
        currentConversation.Reset();
        currentTypingSpeed = currentConversation.GetCurrentNPC().GetTypingSpeed();
        WriteText();
        checkForNextDialogueCoroutine = StartCoroutine(CheckForNextDialogue());
        //applyEffectsCoroutine = StartCoroutine(CheckApplyTextEffects());
    }
    // private IEnumerator CheckApplyTextEffects()
    // {
    //     while (true)
    //     {
    //         if (text.maxVisibleCharacters > 0 && currentDialogue_line != null)
    //         {
    //             ApplyTextEffects();
    //         }

    //         yield return WaitForEndOfFrame();//null;
    //     }
    // }
    private void LateUpdate()
    {
        if (currentDialogue_line == null)
            return;

        if (text.maxVisibleCharacters == 0)
            return;

        ApplyTextEffects();
    }
    private void AnimateCharacter(int charIndex)
    {
        TMP_TextInfo textInfo = text.textInfo;

        // Don't animate characters that haven't been typed yet.
        if (charIndex >= text.maxVisibleCharacters)
            return;

        TMP_CharacterInfo charInfo = textInfo.characterInfo[charIndex];

        if (!charInfo.isVisible)
            return;

        TMP_MeshInfo meshInfo = textInfo.meshInfo[charInfo.materialReferenceIndex];

        float yOffset = Mathf.Sin(Time.time * 5f + charIndex * 0.4f) * 8f;

        for (int i = 0; i < 4; i++)
        {
            int vertexIndex = charInfo.vertexIndex + i;
            meshInfo.vertices[vertexIndex] += Vector3.up * yOffset;
        }
    }
    private void ShakeCharacter(int charIndex)
    {
        TMP_TextInfo textInfo = text.textInfo;

        if (charIndex >= text.maxVisibleCharacters)
            return;

        TMP_CharacterInfo charInfo = textInfo.characterInfo[charIndex];

        if (!charInfo.isVisible)
            return;

        TMP_MeshInfo meshInfo = textInfo.meshInfo[charInfo.materialReferenceIndex];

        float x = (Mathf.PerlinNoise(charIndex, Time.time * 15f) - 0.5f) * 6f;
        float y = (Mathf.PerlinNoise(charIndex + 100, Time.time * 15f) - 0.5f) * 6f;

        Vector3 offset = new Vector3(x, y, 0);

        for (int i = 0; i < 4; i++)
        {
            int vertexIndex = charInfo.vertexIndex + i;
            meshInfo.vertices[vertexIndex] += offset;
        }
    }

    private void ColorCharacter(int charIndex, Color color)
    {
        TMP_TextInfo textInfo = text.textInfo;

        if (charIndex >= text.maxVisibleCharacters)
            return;

        TMP_CharacterInfo charInfo = textInfo.characterInfo[charIndex];

        if (!charInfo.isVisible)
            return;

        TMP_MeshInfo meshInfo = textInfo.meshInfo[charInfo.materialReferenceIndex];

        Color32[] colors = meshInfo.colors32;

        for (int i = 0; i < 4; i++)
        {
            colors[charInfo.vertexIndex + i] = color;
        }
    }

    private void ApplyTextEffects()
    {
        text.ForceMeshUpdate();
        List<Character> characters = currentDialogue_line.GetDialogue_characters();

        for (int i=0; i<characters.Count; i++)
        {
            Character currentChar = characters[i];
            SpecialWordEffectType currentCharSpecialEffect = currentChar.GetCharSpecialEffectType();
            if (currentChar.GetCharSpecialEffectType().HasFlag(SpecialWordEffectType.Wave))
                AnimateCharacter(i);

            if (currentChar.GetCharSpecialEffectType().HasFlag(SpecialWordEffectType.Shaking))
                ShakeCharacter(i);

            // if (word.effects.HasFlag(SpecialWordEffectType.Big))
            //     ScaleCharacters(word.startIndex, word.length);
            ColorCharacter(i, currentChar.GetCharColor());
        }
        text.UpdateVertexData(
    TMP_VertexDataUpdateFlags.Vertices |
    TMP_VertexDataUpdateFlags.Colors32);
        //UpdateMeshes();
    }
    
    private IEnumerator CheckForNextDialogue()
    {
        while (!currentConversation.isConversationFinished())
        {
            if (Input.GetKeyDown(KeyCode.E))
            {
                if (!isCurrentDialogueLineFinished)
                {
                    SkipText();
                }
                else
                {
                    WriteText();
                }
                // Handle input
            }
            yield return null; // Wait until the next frame
        }
    }

    private void Update()
    {
        
    }
    public void SkipText()
    {
        currentTypingSpeed = 0.01f;
        
        //StopAllCoroutines();
        //text.text = textToShow;
    }
}
