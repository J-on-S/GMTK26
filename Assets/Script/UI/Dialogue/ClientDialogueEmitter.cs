using UnityEngine;

/// <summary>
/// Lives on a client prefab and announces that client's generated task when
/// ClientTaskHolder receives it.
/// </summary>
[RequireComponent(typeof(ClientTaskHolder))]
public class ClientDialogueEmitter : MonoBehaviour
{
    [SerializeField] private ClientDialogueEventChannel dialogueChannel;
    [SerializeField] private string displayName;
    [SerializeField] private bool announceOnlyOnce = true;

    private ClientTaskHolder taskHolder;
    private bool hasAnnounced;

    private void Awake()
    {
        taskHolder = GetComponent<ClientTaskHolder>();
    }

    private void OnEnable()
    {
        taskHolder.TaskAssigned += HandleTaskAssigned;
    }

    private void Start()
    {
        // Fallback for a client that was assigned while inactive.
        if (taskHolder.HasTask && !hasAnnounced)
            Announce(taskHolder.AssignedTask);
    }

    private void OnDisable()
    {
        taskHolder.TaskAssigned -= HandleTaskAssigned;
    }

    private void HandleTaskAssigned(ClientTask task)
    {
        Announce(task);
    }

    private void Announce(ClientTask task)
    {
        if (task == null || (announceOnlyOnce && hasAnnounced))
            return;

        hasAnnounced = true;
        string speakerName = string.IsNullOrWhiteSpace(displayName)
            ? gameObject.name.Replace("(Clone)", string.Empty).Trim()
            : displayName;
        ClientDialogueRequest request = new(
            gameObject,
            speakerName,
            task.GetDialogue());

        if (dialogueChannel == null)
        {
            Debug.LogWarning(
                $"{name} has no ClientDialogueEventChannel. " +
                $"Dialogue: {request.Text}",
                this);
            return;
        }

        dialogueChannel.Raise(request);
    }
}
