using System.Collections.Generic;
using UnityEngine;

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
    private Transform goal;
    private Transform doctor;
    private void Start()
    {
        doctor = transform.parent.parent;
        if (!doctor)
        {
            Debug.LogError("Doctor is null");
        }
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
    }
    public override void EnterState()
    {
        if (testIsATable)
        {
            int goalIndex = Random.Range(0, surgeryTableATransforms.Count);
            goal = surgeryTableATransforms[goalIndex];
        }
        else
        {
            int goalIndex = Random.Range(0, surgeryTableBTransforms.Count);
            goal = surgeryTableBTransforms[goalIndex];
        }
        Debug.Log("Doctor starts walk.");
    }

    public override State UpdateState()
    {
        Vector3 targetPosition = new Vector3(
        goal.position.x,
        doctor.transform.position.y, // Keep current height
        goal.position.z
        );

        doctor.transform.position = Vector3.MoveTowards(
            doctor.transform.position,
            targetPosition,
            moveSpeed * Time.deltaTime);

        if (Vector3.Distance(doctor.transform.position, targetPosition) <= stoppingDistance)
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
