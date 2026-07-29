using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GenerateClientInfoUI : MonoBehaviour
{
    [SerializeField] private List<GameObject> cardsParent = new List<GameObject>();
    [SerializeField] private List<Card> cards = new List<Card>();
    [SerializeField] private BodyParts bodyParts;
    private bool hasInitiate = false;
    public void Start()
    {
        foreach (GameObject cardParent in cardsParent)
        {
            Card newCard = new Card();
            for (int i = 0; i < cardParent.transform.childCount; i++)
            {
                Transform child = cardParent.transform.GetChild(i);
                if(i == 0)
                {
                    newCard.text = child.GetComponent<TextMeshProUGUI>();
                    child.GetComponent<TextMeshProUGUI>().text = "";
                }
                else
                {
                    child.GetComponent<Image>().enabled = false;
                    newCard.images.Add(child.GetComponent<Image>());
                }
            }
            cards.Add(newCard);
        }
    }
    public void GenerateUI()
    {
        if(hasInitiate) return;
        hasInitiate = true;
        for(int i=0; i<RandomizedClientList.Instance.GeneratedTaskList.Count; i++)
        {
            ClientTaskQueueEntry client = RandomizedClientList.Instance.GeneratedTaskList[i];
            ClientTask clientTask = client.Task;
            string text = "";
            Card card = cards[i];
            int indexImg = 0;
            
            foreach(BodyPartRequest bodyPartRequest in clientTask.Requests)
            {
                text+="+"+bodyPartRequest.Amount+"\n";
                BodyPart bodyPart = bodyPartRequest.BodyPart;
        
                card.images[indexImg].sprite = bodyPart.bodyPartImg;
                card.images[indexImg].enabled = true;
                indexImg++;
            }
            
            card.text.text = text;
        }
        
    }
}
