using UnityEngine;

public class GenerateClientInfoUI : MonoBehaviour
{
    [SerializeField] private GameObject clientPage1;
    [SerializeField] private GameObject clientPage2;
    [SerializeField] private GameObject cardPrefab;
    [SerializeField] private Sprite testSprite;
    private bool hasInitiate = false;
    public void GenerateCards()
    {
        if(hasInitiate) return;
        hasInitiate = true;
        int nbCard = 0;
        GameObject currentPage = clientPage1;

        for(int i=0; i<RandomizedClientList.Instance.GeneratedTaskList.Count; i++)
        {
            ClientTaskQueueEntry client = RandomizedClientList.Instance.GeneratedTaskList[i];
            ClientTask clientTask = client.Task;
            
            GameObject newCardObj = Instantiate(cardPrefab, currentPage.transform.position, Quaternion.identity, currentPage.transform);
            CardInfo cardInfo = newCardObj.GetComponent<CardInfo>();
            cardInfo.SetCardProfile(testSprite);

            foreach(BodyPartRequest bodyPartRequest in clientTask.Requests)
            {
                cardInfo.SetCardInfo(bodyPartRequest.Amount.ToString(), bodyPartRequest.BodyPart.bodyPartImg);
            }
            nbCard++;
            if (nbCard >= 6)
            {
                currentPage = clientPage2;
            }
        }
    }
}
