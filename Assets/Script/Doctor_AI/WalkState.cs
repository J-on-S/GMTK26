using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class WalkState : State
{
    [SerializeField] private List<StateWeight> states = new List<StateWeight>();
    [SerializeField] private float moveSpeed;
    [SerializeField] private float stoppingDistance = 0.1f;
    [SerializeField] private Transform surgeryTableATransform;
    [SerializeField] private Transform surgeryTableBTransform;
    private List<Transform> surgeryTableATransforms;
    private List<Transform> surgeryTableBTransforms;
    [SerializeField] private bool testIsATable = false;
    [SerializeField] private float rotationSpeed = 5f;
    private Transform currentBed;
    [SerializeField] private Transform bedA;
    [SerializeField] private Transform bedB;
    private NavMeshAgent agent;
    protected override void Awake()
    {
        base.Awake();
        surgeryTableATransforms = new List<Transform>();

        foreach (Transform child in surgeryTableATransform)
        {
            surgeryTableATransforms.Add(child);
        }

        surgeryTableBTransforms = new List<Transform>();

        foreach (Transform child in surgeryTableBTransform)
        {
            surgeryTableBTransforms.Add(child);
        }
        agent = bot.GetComponent<NavMeshAgent>();
    }
    public override void EnterState()
    {
        if (testIsATable)
        {
            int goalIndex = Random.Range(0, surgeryTableATransforms.Count);
            agent.destination = surgeryTableATransforms[goalIndex].position;
            currentBed = bedA;
        }
        else
        {
            int goalIndex = Random.Range(0, surgeryTableBTransforms.Count);
            agent.destination = surgeryTableBTransforms[goalIndex].position;
            currentBed = bedB;
        }
        Debug.Log("Doctor starts walk.");
        agent.updateRotation = true;
        anim.Play(animName);
    }

    public override State UpdateState()
    {
        // Still walking
    if (agent.remainingDistance > stoppingDistance)
    {
        return this;
    }

    // Stop NavMeshAgent from rotating
    agent.updateRotation = false;

    Vector3 direction = currentBed.position - bot.transform.position;
    direction.y = 0f;

    Quaternion targetRotation = Quaternion.LookRotation(direction);

    bot.transform.rotation = Quaternion.RotateTowards(
        bot.transform.rotation,
        targetRotation,
        rotationSpeed * Time.deltaTime);

    // Check if we're facing the bed
    float angle = Quaternion.Angle(bot.transform.rotation, targetRotation);

    if (angle < 1f) // 1 degree tolerance
    {
        return stateManager.RandomState(states);
    }

    return this;
    }

    public override void ExitState()
    {
        Debug.Log("Doctor found a task.");
    }
}
