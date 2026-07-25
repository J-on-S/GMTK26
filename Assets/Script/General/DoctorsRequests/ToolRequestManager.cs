using UnityEngine;
using System.Collections.Generic;
// using UnityEditor;

public class ToolRequestManager : MonoBehaviour
{
    [System.Serializable]
    public struct ToolRequest
    {
        public string itemName;
        public ItemType itemType;
        public float timeLimit;
    }

    // request stuff
    public List<ToolRequest> availableRequests = new List<ToolRequest>();   // this contains the active doctor's requests
    public List<ToolRequest> allTools = new List<ToolRequest>();            // this contains the list of ALL TOOLS that will be used to randomly select items for the doctor's requests
    public float timeBetweenRequests = 5f; // oooldown before next order from doctor
    public float numberOfRequests = 5;  // number of total requests that the doctor will ask for

    // states
    private enum State{Idle, ActiveRequest, Cooldown}
    private State currentState = State.Idle;

    private ToolRequest currentRequest;
    //private string currentRequiredTool;
    private float remainingTime;
    private float remainingCooldown;
    private SpawnBodyPartCustomer spawnBodyPartCustomer;

    private void Start()
    {
        // start a request immediately
        StartCooldown();
        FinishDoctorRequestList(); // add random assortment of tools to doctor request list -- this will probably need to be called after the client commands are in
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

    // PHILIPPE CALLS THIS FUNCTION
    // this can be called in the client request code (philippe's part) to add the body parts to the list
    public void AddToDoctorRequestList(string bodyPartName)
    {
        availableRequests.Add(new ToolRequest
        {
            itemName = bodyPartName,
            itemType = ItemType.BodyPart,
            timeLimit = UnityEngine.Random.Range(6f, 9f)    // random time limit between 6-9 seconds
        });
    }

    // PHILIPPE CALLS THIS FUNCTION PROBABLY ALSO -- after all body parts have been added to the list
    public void FinishDoctorRequestList()
    {
        if (availableRequests.Count >= numberOfRequests) return; // request list already full no need to add anything else
        
        // fill up the available requests list with random tools from the list of all tools
        while (availableRequests.Count < numberOfRequests)
        {
            int choice = Random.Range(0, allTools.Count);       // random index of tool
            availableRequests.Add(allTools[choice]);
        }

    }

    // countdown and then failure of request if not fulfilled within time
    private void HandleActiveRequest()
    {
        remainingTime -= Time.deltaTime;
        if (remainingTime <= 0)
            {
                FailRequest();
            }   
    }

    // cooldown between requests
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
        if (availableRequests.Count == 0) return;   // this is the scenario when the doctor requests are done, something needs to happen here

        // get a random item from the list
        int index = Random.Range(0, availableRequests.Count);
        currentRequest = availableRequests[index];
        remainingTime = availableRequests[index].timeLimit;
        currentState = State.ActiveRequest;

        string itemCategory = currentRequest.itemType.ToString();
        Debug.Log($"Hey, hand me a {itemCategory}: [{currentRequest.itemName}] within {remainingTime:F1} seconds!");
        //Debug.Log($"Hey, hand me a: {currentRequiredTool} within {remainingTime:F1} seconds!");

        // remove request from the list immediately, doctor will not ask again regardless of if request is fulfilled or not
        availableRequests.Remove(currentRequest);
        Debug.Log($"This is how many items are in the list {availableRequests.Count}");

    }

    // check if player submitted the tool correctly, returns true if correctly submitted
    public bool PlayerSubmittedTool(string submittedName, ItemType submittedType)
    {
        if (currentState != State.ActiveRequest) return false;

        if (submittedName == currentRequest.itemName && submittedType == currentRequest.itemType)
        {
            Debug.Log("Dude thanks for giving me that.");
            StartCooldown();
            //For now: Add the body on it
            if (submittedType == ItemType.BodyPart)
            {
                spawnBodyPartCustomer?.AddBodyPart(submittedName);
            }
            return true;    // success

            // some sort of score stuff
        }
        else
        {
            // maybe add penalty to score here for not fulfilling request?
            // PUT SUBTRACTING A HEART HERE
            Debug.Log($"Nah man wrong tool. I needed {currentRequest.itemType} named {currentRequest.itemName}, but you gave me {submittedType} named {submittedName}.");
            return false; // not success
        }
    }

    // fail message and restart cooldown
    private void FailRequest()
    {
        Debug.Log("Time is up! You failed the request.");
        //TODO: LOOSE LIFE
        StartCooldown();
    }

    // deals with cooldown state and timer
    private void StartCooldown()
    {
        remainingCooldown = timeBetweenRequests;
        currentState = State.Cooldown;
        Debug.Log($"Waiting for the next request. Cooldown active for {timeBetweenRequests} seconds");
    }

    public float timeRemaining()
    {
        return remainingTime;
    }
}
