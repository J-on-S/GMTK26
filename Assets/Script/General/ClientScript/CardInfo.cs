using UnityEngine;
using UnityEngine.UI;
public class CardInfo : MonoBehaviour
{
    [SerializeField] private Transform bodyPartInfos;
    [SerializeField] private GameObject bodyPartInfoPrefab;
    [SerializeField] private Image profileImg;
    
    public void SetCardProfile(Sprite profileImg)
    {
        this.profileImg.sprite = profileImg; 
    }
    public void SetCardInfo(string amount, Sprite bodyPartIcon)
    {
        GameObject newBodyPartInfo = Instantiate(bodyPartInfoPrefab, bodyPartInfos.position, Quaternion.identity, bodyPartInfos);
        newBodyPartInfo.GetComponent<BodyPartTxt>().SetBodyPartInfo(amount, bodyPartIcon);
    }
}
