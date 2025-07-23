using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
[CreateAssetMenu(menuName = "Game/Player/Class", fileName = "NewClass")]
public class PlayerClassDefinition : ScriptableObject
{
    [Header("Basic Info")]
    public string className;
    public Sprite classIcon;
    [TextArea] public string description;

    [Header("Base Stats")]
    public int baseHealth;
    public int baseAttack;
    public int baseDefense;
    public int baseMagic;
    public float attackRange = 1f;

    [Header("Visuals")]
    public GameObject characterPrefab;
    public AnimatorOverrideController animatorController;

    [Header("Abilities")]
    public List<AbilityDefinition> startingAbilities;
    public List<ItemDefinition> startingEquipment;

    [Header("Progression")]
    public AnimationCurve healthGrowth;
    public AnimationCurve attackGrowth;
    // Add other growth curves as needed
}