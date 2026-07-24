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
        }
        else
        {
            int goalIndex = Random.Range(0, surgeryTableBTransforms.Count);
            agent.destination = surgeryTableBTransforms[goalIndex].position;
        }
        Debug.Log("Doctor starts walk.");
        anim.Play(animName);
    }

    public override State UpdateState()
    {
        //agent.speed = (anim.deltaPosition / Time.deltaTime).magnitude;
        //Debug.Log("agent speed: "+agent.speed); 
        // Vector3 targetPosition = new Vector3(
        // agent.destination.x,
        // bot.transform.position.y, // Keep current height
        // agent.destination.z
        // );

        // Calculate direction
        // Vector3 direction = targetPosition - bot.transform.position;
        // direction.y = 0f;

        // // Rotate if we're actually moving
        // if (direction.sqrMagnitude > 0.001f)
        // {
        //     Quaternion targetRotation = Quaternion.LookRotation(direction);
        //     bot.transform.rotation = Quaternion.Slerp(
        //         bot.transform.rotation,
        //         targetRotation,
        //         rotationSpeed * Time.deltaTime);
        // }

        // Move
        // bot.transform.position = Vector3.MoveTowards(
        //     bot.transform.position,
        //     targetPosition,
        //     moveSpeed * Time.deltaTime);

        if (Vector3.Distance(bot.transform.position, agent.destination) <= stoppingDistance)
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
