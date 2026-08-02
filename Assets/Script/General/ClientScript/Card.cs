using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
[Serializable]
public class Card
{
    public Sprite profileImg;
    public string amount;
    public Sprite bodyPartIcon;
    public Card(CardInfo cardInfo)
    {
        cardInfo.SetCardProfile(profileImg);
        cardInfo.SetCardInfo(amount.ToString(), bodyPartIcon);
    }
    public Card(int amount, Sprite bodyPartIcon)
    {
        this.amount = amount.ToString();
        this.bodyPartIcon = bodyPartIcon;
    }
}