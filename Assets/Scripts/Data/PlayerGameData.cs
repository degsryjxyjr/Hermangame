using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

public class PlayerGameData : ScriptableObject
{
    public PlayerClassDefinition classDefinition;
    public int level = 1;
    public int experience = 0;
    
    [System.Serializable]
    public class RuntimeStats
    {
        public int currentHealth;
        public int maxHealth;
        public int attack;
        public int defense;
        public int magic;
        
        public void Initialize(PlayerClassDefinition classDef, int level)
        {
            maxHealth = Mathf.FloorToInt(classDef.baseHealth * classDef.healthGrowth.Evaluate(level));
            currentHealth = maxHealth;
            attack = Mathf.FloorToInt(classDef.baseAttack * classDef.attackGrowth.Evaluate(level));
            defense = Mathf.FloorToInt(classDef.baseDefense * classDef.attackGrowth.Evaluate(level));
            magic = Mathf.FloorToInt(classDef.baseMagic * classDef.attackGrowth.Evaluate(level));
        }
    }
    
    public RuntimeStats stats = new();
    public List<AbilityDefinition> unlockedAbilities = new();
}