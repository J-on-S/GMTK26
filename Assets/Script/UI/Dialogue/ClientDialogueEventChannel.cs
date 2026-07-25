using System;
using UnityEngine;

[Serializable]
public class ClientDialogueRequest
{
    [SerializeField] private GameObject speaker;
    [SerializeField] private string speakerName;
    [SerializeField, TextArea] private string text;

    public GameObject Speaker => speaker;
    public string SpeakerName => speakerName;
    public string Text => text;

    public ClientDialogueRequest(
        GameObject speaker,
        string speakerName,
        string text)
    {
        this.speaker = speaker;
        this.speakerName = speakerName;
        this.text = text;
    }
}

/// <summary>
/// Decoupled channel between clients requesting dialogue and UI displaying it.
/// </summary>
[CreateAssetMenu(
    fileName = "ClientDialogueEventChannel",
    menuName = "GMTK26/UI/Client Dialogue Event Channel")]
public class ClientDialogueEventChannel : ScriptableObject
{
    public event Action<ClientDialogueRequest> DialogueRequested;

    public void Raise(ClientDialogueRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Text))
            return;

        DialogueRequested?.Invoke(request);
    }
}
