using UnityEngine;
using System.Collections.Generic;

public class ToolRequestManager : MonoBehaviour
{

    public enum ItemType {Tool, BodyPart}

    [System.Serializable]
    public struct ToolRequest
    {
        public string itemName;
        public ItemType itemType;
        public float timeLimit;
    }

    // request stuff
    public List<ToolRequest> availableRequests = new List<ToolRequest>();
    public float timeBetweenRequests = 5f; // oooldown before next order from doctor

    // states
    private enum State{Idle, ActiveRequest, Cooldown}
    private State currentState = State.Idle;

    private ToolRequest currentRequest;
    //private string currentRequiredTool;
    private float remainingTime;
    private float remainingCooldown;

    private void Start()
    {
        // start a request immediately
        StartCooldown();
    }

    // Update is called once per frame

    void Update()
    {
        switch (currentState) 
        {
            case State.ActiveRequest:
                HandleActiveRequest();
                break;

            case State.Cooldown:
                HandleCooldown();
                break;

            case State.Idle:
                // just idle
                break;
        }


    }

    private void HandleActiveRequest()
    {
        remainingTime -= Time.deltaTime;
        if (remainingTime <= 0)
            {
                FailRequest();
            }
        
    }

    private void HandleCooldown()
    {
        remainingCooldown -= Time.deltaTime;
        if (remainingCooldown <= 0)
        {
            StartNewRandomRequest();
        }
    }

    // randomize the requests
    public void StartNewRandomRequest()
    {
        if (availableRequests.Count == 0) return;   // maybe change to a set number of requests? list should always have all the tools

        // get a random item from the list
        int index = Random.Range(0, availableRequests.Count);
        currentRequest = availableRequests[index];
        remainingTime = availableRequests[index].timeLimit;
        currentState = State.ActiveRequest;

        string itemCategory = currentRequest.itemType.ToString();
        Debug.Log($"Hey, hand me a {itemCategory}: [{currentRequest.itemName}] within {remainingTime:F1} seconds!");
        //Debug.Log($"Hey, hand me a: {currentRequiredTool} within {remainingTime:F1} seconds!");

    }

    // check if player submitted the tool correctly
    public void PlayerSubmittedTool(string submittedName, ItemType submittedType)
    {
        if (currentState != State.ActiveRequest) return;

        if (submittedName == currentRequest.itemName && submittedType == currentRequest.itemType)
        {
            Debug.Log("Dude thanks for giving me that.");
            StartCooldown();

            // some sort of score stuff
        }
        else
        {
            // maybe add penalty here for not fulfilling request?
            Debug.Log($"Nah man wrong tool. I needed {currentRequest.itemType} named {currentRequest.itemName}, but you gave me {submittedType} named {submittedName}.");
        }
    }

    private void FailRequest()
    {
        Debug.Log("Time is up! You failed the request.");
        StartCooldown();
    }

    private void StartCooldown()
    {
        remainingCooldown = timeBetweenRequests;
        currentState = State.Cooldown;
        Debug.Log($"Waiting for the next request. Cooldown active for {timeBetweenRequests} seconds");
    }
}
