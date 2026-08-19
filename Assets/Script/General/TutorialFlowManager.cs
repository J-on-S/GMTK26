using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
[Serializable]
public class CutScene
{
    [SerializeField] private string name;
    [SerializeField] private ConversationNode conversationNode;
    [SerializeField] private PlayableDirector playableDirector;
    public string Name => name;
    public bool DialogueIsFinished => conversationNode.hasFinished;
    public void SetDialogueFinished() => conversationNode.hasFinished = true;
    public void Play()
    {
        playableDirector.Play();
        if (conversationNode!=null)
        {
            ConversationSystem.Instance.StartConversation(conversationNode.conversation);
            conversationNode.onConversationFinished?.Invoke();
        }
        
    }
    public void Stop()
    {
        playableDirector.Stop();
    }
}
public class TutorialFlowManager : MonoBehaviour
{
    [SerializeField] private List<CutScene> cutscenes; 
    [ReadOnly, SerializeField]  private CutScene currentCutScene;
    public static TutorialFlowManager Instance {get; private set;}
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }
    public void Start()
    {
        StartCoroutine(StartTutorial());
    }
    public IEnumerator PlayCutScene(string name)
    {
        foreach(CutScene cutScene in cutscenes)
        {
            if (cutScene.Name == name)
            {
                cutScene.Play();
                currentCutScene = cutScene;
                break;
            }
        }

        if (currentCutScene == null)
        {
            Debug.LogWarning($"Cutscene '{name}' not found.");
            yield break;
        }

        // Wait while dialogue is happening
        while (!currentCutScene.DialogueIsFinished)
        {
            yield return new WaitForEndOfFrame();
        }

        // Dialogue finished
        currentCutScene.Stop();
        currentCutScene = null;
    }
    public void FinishCurrentConversation()
    {
        currentCutScene.SetDialogueFinished();
    }
    // [SerializeField] private DialogueManager dialogue;
    // [SerializeField] private TaskManager tasks;
    // [SerializeField] private CutsceneManager cutscenes;
    // [SerializeField] private SceneLoader sceneLoader;

    public IEnumerator StartTutorial()
    {
        yield return PlayCutScene("doctor meeting");
        
        yield return PlayCutScene("customer meeting");
        // tasks.StartTask("FindKey");

        // yield return new WaitUntil(
        //     () => tasks.IsComplete("FindKey")
        // );

        // yield return dialogue.Play("found_key");

        // yield return sceneLoader.LoadScene("Bedroom");
    }
}
