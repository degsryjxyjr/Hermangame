// MapNode.cs
using UnityEngine;
using UnityEngine.UI;

public class MapNode : MonoBehaviour
{
    public enum NodeType { Combat, Elite, Rest, Shop, Treasure, Boss }
    public NodeType nodeType;
    public bool isAvailable = false;
    public bool isCompleted = false;
    
    [Header("Visuals")]
    public Image icon;
    public Sprite[] typeIcons; // Assign in inspector
    public Color availableColor;
    public Color completedColor;
    public Color unavailableColor;

    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
        UpdateVisuals();
    }

    public void UpdateVisuals()
    {
        if (isCompleted)
        {
            icon.color = completedColor;
            button.interactable = false;
        }
        else if (isAvailable)
        {
            icon.color = availableColor;
            button.interactable = true;
        }
        else
        {
            icon.color = unavailableColor;
            button.interactable = false;
        }
        
        // Set icon based on type (assign sprites in inspector)
        icon.sprite = typeIcons[(int)nodeType];
    }

    public void OnNodeClicked()
    {
        if (isAvailable && !isCompleted)
        {
            MapManager.Instance.SelectNode(this);
        }
    }
}