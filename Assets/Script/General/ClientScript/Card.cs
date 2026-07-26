using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;
[Serializable]
public class Card
{
    public TextMeshProUGUI text;
    public List<Image> images = new List<Image>();
    public Card()
    {
        
    }
}