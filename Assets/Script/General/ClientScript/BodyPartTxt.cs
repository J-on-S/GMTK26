using TMPro;
using UnityEngine;
using UnityEngine.UI;
public class BodyPartTxt : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI bodyPartAmount;
    [SerializeField] private Image bodyPartIcon;
    
    public void SetBodyPartInfo(string amount, Sprite bodyPartIconImg)
    {
        bodyPartAmount.text = "+"+amount;
        bodyPartIcon.sprite = bodyPartIconImg;
    }
}
