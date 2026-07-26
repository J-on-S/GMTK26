using System.Text;
using UnityEngine;

/// <summary>
/// Inspector-driven test harness for spawning a client, inspecting its
/// generated request, fulfilling it, and removing the completed client.
/// </summary>
public class ClientTaskDebugTester : MonoBehaviour
{
    [Header("Required references")]
    [SerializeField] private ClientTaskList taskList;
    [SerializeField] private OperationChair operationChair;

    [Header("Runtime debug data")]
    [SerializeField] private GameObject currentClient;
    [SerializeField, TextArea(4, 12)] private string generatedTaskData;

    public GameObject CurrentClient => currentClient;
    public string GeneratedTaskData => generatedTaskData;
    private RandomizedClientList ClientList => RandomizedClientList.Instance;

    [ContextMenu("1. Pregenerate Client Task List")]
    public void PregenerateClientTaskList()
    {
        if (!RequirePlayMode())
            return;

        if (ClientList == null || taskList == null)
        {
            Debug.LogError(
                "Add a RandomizedClientList singleton and assign Task List before testing.",
                this);
            return;
        }

        ClientList.PregenerateClientTasks(taskList);
        LogPregeneratedTaskList();
    }

    [ContextMenu("2. Log Pregenerated Task List")]
    public void LogPregeneratedTaskList()
    {
        if (ClientList == null)
            return;

        StringBuilder data = new();
        data.AppendLine($"Pre-generated entries: {ClientList.TaskListCount}");

        int number = 1;
        foreach (ClientTaskQueueEntry entry in ClientList.GeneratedTaskList)
        {
            data.AppendLine(
                $"{number}. {entry.ClientPrefab.name}: " +
                $"{entry.Task.GetDialogue()} | Spawned: {entry.IsSpawned}");
            number++;
        }

        generatedTaskData = data.ToString();
        Debug.Log(generatedTaskData, this);
    }

    [ContextMenu("3. Spawn Next Pregenerated Client")]
    public void SpawnNextPregeneratedClient()
    {
        if (!RequirePlayMode())
            return;

        if (ClientList == null || operationChair == null)
        {
            Debug.LogError(
                "Add a RandomizedClientList singleton and assign Operation Chair before testing.",
                this);
            return;
        }

        if (currentClient != null)
        {
            Debug.LogWarning(
                "Complete or remove the current debug client before spawning another.",
                currentClient);
            return;
        }

        currentClient = ClientList.SpawnNextClient(operationChair);
        if (currentClient == null)
        {
            Debug.LogWarning("No client could be spawned.", this);
            return;
        }

        ClientTaskHolder holder = currentClient.GetComponent<ClientTaskHolder>();
        if (holder == null || holder.AssignedTask == null)
        {
            Debug.LogError(
                "The spawned client needs ClientTaskHolder and an assigned task.",
                currentClient);
            ClientList.RemoveActiveClient(currentClient);
            currentClient = null;
            return;
        }

        holder.TaskCompletedWithOwner += HandleTaskCompleted;
        RefreshAndLogTaskData(holder);
    }

    [ContextMenu("4. Log Current Client Task")]
    public void LogCurrentClientTask()
    {
        ClientTaskHolder holder = GetCurrentHolder();
        if (holder != null)
            RefreshAndLogTaskData(holder);
    }

    [ContextMenu("5. Complete Current Client Task")]
    public void CompleteCurrentClientTask()
    {
        if (!RequirePlayMode())
            return;

        ClientTaskHolder holder = GetCurrentHolder();
        if (holder == null || holder.AssignedTask == null)
            return;

        ClientTask task = holder.AssignedTask;

        // Give every still-required body part through the normal client API.
        foreach (BodyPartRequest request in task.Requests)
        {
            while (task.GetRemainingAmount(request.BodyPart) > 0)
                holder.GiveBodyPart(request.BodyPart);
        }
    }

    [ContextMenu("Remove Current Client Without Completing")]
    public void RemoveCurrentClient()
    {
        if (!RequirePlayMode())
            return;

        if (currentClient == null)
            return;

        ClientTaskHolder holder = currentClient.GetComponent<ClientTaskHolder>();
        if (holder != null)
            holder.TaskCompletedWithOwner -= HandleTaskCompleted;

        ClientList.RemoveActiveClient(currentClient);
        currentClient = null;
        generatedTaskData = "No active debug client.";
    }

    private ClientTaskHolder GetCurrentHolder()
    {
        if (currentClient == null)
        {
            Debug.LogWarning("Spawn a debug client first.", this);
            return null;
        }

        ClientTaskHolder holder = currentClient.GetComponent<ClientTaskHolder>();
        if (holder == null)
            Debug.LogError("Current client has no ClientTaskHolder.", currentClient);

        return holder;
    }

    private bool RequirePlayMode()
    {
        if (Application.isPlaying)
            return true;

        Debug.LogWarning("Enter Play Mode before running the client task test.", this);
        return false;
    }

    private void RefreshAndLogTaskData(ClientTaskHolder holder)
    {
        ClientTask task = holder.AssignedTask;
        StringBuilder data = new();
        data.AppendLine($"Client: {holder.gameObject.name}");
        data.AppendLine($"Dialogue: {task.GetDialogue()}");
        data.AppendLine($"Total requested parts: {task.TotalParts}");
        data.AppendLine("Requirements:");

        foreach (BodyPartRequest request in task.Requests)
        {
            data.AppendLine(
                $"- {request.BodyPart}: {request.Amount} requested, " +
                $"{task.GetRemainingAmount(request.BodyPart)} remaining");
        }

        data.AppendLine($"Complete: {task.IsComplete}");
        data.AppendLine($"Active clients: {ClientList.ActiveClientCount}");

        generatedTaskData = data.ToString();
        Debug.Log(generatedTaskData, holder);
    }

    private void HandleTaskCompleted(
        ClientTaskHolder holder,
        ClientTask completedTask)
    {
        generatedTaskData =
            $"Completed: {completedTask.GetDialogue()}\n" +
            "Client was removed from the generated task list and active list.";

        Debug.Log(generatedTaskData, holder);
        holder.TaskCompletedWithOwner -= HandleTaskCompleted;
        currentClient = null;
    }
}
