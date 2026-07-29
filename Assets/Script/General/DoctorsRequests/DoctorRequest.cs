using System.Collections.Generic;
using UnityEngine;

public class DoctorRequest
{
    public List<Request> currentRequests = new List<Request>();   // this contains the active doctor's requests
    public GameObject targetClient;
    public OperationChair targetChair;
    public void AddRequest(Request newRequest) => currentRequests.Add(newRequest);
    public void ClearRequests() => currentRequests.Clear();
    public int Count => currentRequests.Count;
    public Request ChosenRequest()
    {
        int randomRequestIndex = Random.Range(0, Count);
        Request currentRequest = currentRequests[randomRequestIndex];
        currentRequests.Remove(currentRequest);
        return currentRequest;
    }
    public DoctorRequest(GameObject targetClient, OperationChair targetChair)
    {
        currentRequests = new List<Request>();
        this.targetClient = targetClient;
        this.targetChair = targetChair;
    }
}