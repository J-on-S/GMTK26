using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Listens to client dialogue requests and displays them sequentially.
/// Requests from two clients spawning together are queued.
/// </summary>
public class ClientDialogueUIReceiver : MonoBehaviour
{
    [Header("Channel")]
    [SerializeField] private ClientDialogueEventChannel dialogueChannel;

    [Header("UI")]
    [SerializeField] private GameObject dialogueRoot;
    [SerializeField] private TMP_Text speakerNameText;
    [SerializeField] private TMP_Text dialogueText;
    [SerializeField, Min(0.1f)] private float displayDuration = 4f;

    private readonly Queue<ClientDialogueRequest> pendingRequests = new();
    private Coroutine displayRoutine;

    private void OnEnable()
    {
        if (dialogueChannel != null)
            dialogueChannel.DialogueRequested += EnqueueDialogue;
    }

    private void Start()
    {
        if (dialogueRoot != null)
            dialogueRoot.SetActive(false);
    }

    private void OnDisable()
    {
        if (dialogueChannel != null)
            dialogueChannel.DialogueRequested -= EnqueueDialogue;

        if (displayRoutine != null)
            StopCoroutine(displayRoutine);

        displayRoutine = null;
        pendingRequests.Clear();
    }

    private void EnqueueDialogue(ClientDialogueRequest request)
    {
        pendingRequests.Enqueue(request);

        if (displayRoutine == null)
            displayRoutine = StartCoroutine(DisplayQueuedDialogue());
    }

    private IEnumerator DisplayQueuedDialogue()
    {
        while (pendingRequests.Count > 0)
        {
            ClientDialogueRequest request = pendingRequests.Dequeue();

            if (speakerNameText != null)
                speakerNameText.text = request.SpeakerName;

            if (dialogueText != null)
                dialogueText.text = request.Text;

            if (dialogueRoot != null)
                dialogueRoot.SetActive(true);

            Debug.Log(
                $"{request.SpeakerName}: {request.Text}",
                request.Speaker);

            yield return new WaitForSeconds(displayDuration);
        }

        if (dialogueRoot != null)
            dialogueRoot.SetActive(false);

        displayRoutine = null;
    }
}
