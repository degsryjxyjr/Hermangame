using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ShopItemUIEntry : MonoBehaviour
{
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text costText;
    [SerializeField] private Image image;
    
    [SerializeField] private ShopService _shop;

    public void Initialize(ItemDefinition itemDef, ShopService shop, int modifiedPrice = -1)
    {
        _shop = shop;

        nameText.text = itemDef.displayName;
        image.sprite = itemDef.icon;

        // if no modified cost is passed(cost=0) use the basePrice from itemDef
        if (modifiedPrice == -1)
        {
            costText.text += itemDef.basePrice.ToString();
        }
        else
        {
            costText.text += modifiedPrice.ToString();
        }
    }
}
