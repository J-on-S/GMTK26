using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class WalkState : State
{
    [SerializeField] private List<StateWeight> states = new List<StateWeight>();
    [SerializeField] private float moveSpeed;
    [SerializeField] private float stoppingDistance = 0.1f;

    [Header("Doctor request source")]
    [Tooltip(
        "Provides the client and operation chair the doctor is currently " +
        "processing. Found automatically when left empty.")]
    [SerializeField] private ToolRequestManager toolRequestManager;

    [Header("Bed walking positions")]
    [SerializeField] private Transform surgeryTableATransform;
    [SerializeField] private Transform surgeryTableBTransform;
    private readonly List<Transform> surgeryTableATransforms = new();
    private readonly List<Transform> surgeryTableBTransforms = new();

    [Header("Bed facing targets")]
    [SerializeField] private float rotationSpeed = 5f;
    private Transform currentBed;
    private OperationChair targetChair;
    [SerializeField] private Transform bedA;
    [SerializeField] private Transform bedB;

    private NavMeshAgent agent;
    private bool hasDestination;
    private bool loggedMissingTarget;

    protected override void Awake()
    {
        base.Awake();

        CacheWaypoints(
            surgeryTableATransform,
            surgeryTableATransforms);
        CacheWaypoints(
            surgeryTableBTransform,
            surgeryTableBTransforms);

        agent = bot.GetComponent<NavMeshAgent>();
        if (agent == null)
        {
            Debug.LogError(
                "WalkState requires a NavMeshAgent on the doctor.",
                this);
            return;
        }

        agent.speed = moveSpeed;
        agent.stoppingDistance = stoppingDistance;

        if (toolRequestManager == null)
            toolRequestManager =
                FindFirstObjectByType<ToolRequestManager>();
    }

    public override void EnterState()
    {
        currentBed = null;
        targetChair = null;
        hasDestination = false;
        loggedMissingTarget = false;

        if (agent == null)
            return;

        agent.speed = moveSpeed;
        agent.stoppingDistance = stoppingDistance;
        agent.updateRotation = true;
        if (agent.isOnNavMesh)
            agent.isStopped = false;

        TrySetFocusedBedDestination();

        Debug.Log("Doctor starts walk.");
        anim.Play(animName);
    }

    public override State UpdateState()
    {
        if (agent == null)
            return this;

        OperationChair focusedChair =
            toolRequestManager != null
                ? toolRequestManager.FocusedChair
                : null;

        if (hasDestination &&
            (currentBed == null || focusedChair != targetChair))
        {
            hasDestination = false;
            currentBed = null;
            targetChair = null;

            if (agent.isOnNavMesh)
            {
                agent.isStopped = false;
                agent.ResetPath();
            }
        }

        // ToolRequestManager may choose its first client after this state
        // begins, so wait until a focused chair becomes available.
        if (!hasDestination)
        {
            TrySetFocusedBedDestination();
            return this;
        }

        if (agent.pathPending ||
            agent.remainingDistance > stoppingDistance)
        {
            return this;
        }

        agent.isStopped = true;
        agent.updateRotation = false;

        Vector3 direction =
            currentBed.position - bot.transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.0001f)
            return stateManager.RandomState(states);

        Quaternion targetRotation =
            Quaternion.LookRotation(direction);

        bot.transform.rotation = Quaternion.RotateTowards(
            bot.transform.rotation,
            targetRotation,
            rotationSpeed * Time.deltaTime);

        float angle = Quaternion.Angle(
            bot.transform.rotation,
            targetRotation);

        if (angle < 1f)
            return stateManager.RandomState(states);

        return this;
    }

    public override void ExitState()
    {
        Debug.Log("Doctor found a task.");
    }

    private bool TrySetFocusedBedDestination()
    {
        if (toolRequestManager == null)
        {
            toolRequestManager =
                FindFirstObjectByType<ToolRequestManager>();
        }

        OperationChair focusedChair =
            toolRequestManager != null
                ? toolRequestManager.FocusedChair
                : null;

        if (focusedChair == null)
        {
            LogMissingTargetOnce(
                "WalkState is waiting for ToolRequestManager to focus a client.");
            return false;
        }

        List<Transform> walkingPositions;
        Transform fallbackFacingTarget;

        if (MatchesChair(focusedChair, bedA))
        {
            walkingPositions = surgeryTableATransforms;
            fallbackFacingTarget = bedA;
        }
        else if (MatchesChair(focusedChair, bedB))
        {
            walkingPositions = surgeryTableBTransforms;
            fallbackFacingTarget = bedB;
        }
        else
        {
            LogMissingTargetOnce(
                $"{focusedChair.name} does not match WalkState's Bed A or Bed B.");
            return false;
        }

        if (walkingPositions.Count == 0)
        {
            LogMissingTargetOnce(
                $"{focusedChair.name} has no doctor walking positions.");
            return false;
        }

        currentBed = focusedChair.CurrentClient != null
            ? focusedChair.CurrentClient.transform
            : fallbackFacingTarget;
        targetChair = focusedChair;

        if (currentBed == null)
        {
            LogMissingTargetOnce(
                $"{focusedChair.name} has no client or facing target.");
            return false;
        }

        Transform destination =
            walkingPositions[Random.Range(0, walkingPositions.Count)];

        if (!agent.isOnNavMesh ||
            !agent.SetDestination(destination.position))
        {
            targetChair = null;
            LogMissingTargetOnce(
                $"Doctor could not calculate a path to {focusedChair.name}.");
            return false;
        }

        agent.isStopped = false;
        loggedMissingTarget = false;
        hasDestination = true;
        Debug.Log(
            $"Doctor is walking to {focusedChair.name} for " +
            $"{focusedChair.CurrentClient?.name ?? "its focused client"}.",
            this);
        return true;
    }

    private static bool MatchesChair(
        OperationChair chair,
        Transform configuredBed)
    {
        if (chair == null || configuredBed == null)
            return false;

        Transform chairTransform = chair.transform;
        return configuredBed == chairTransform ||
               configuredBed.IsChildOf(chairTransform) ||
               chairTransform.IsChildOf(configuredBed);
    }

    private static void CacheWaypoints(
        Transform parent,
        List<Transform> destination)
    {
        destination.Clear();
        if (parent == null)
            return;

        foreach (Transform child in parent)
            destination.Add(child);
    }

    private void LogMissingTargetOnce(string message)
    {
        if (loggedMissingTarget)
            return;

        loggedMissingTarget = true;
        Debug.LogWarning(message, this);
    }
}
