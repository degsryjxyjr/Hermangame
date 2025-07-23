using UnityEngine;

// Base item definition
public abstract class Item : ScriptableObject
{
    public int ItemId;
    public string DisplayName;
    public Sprite Icon;
}

