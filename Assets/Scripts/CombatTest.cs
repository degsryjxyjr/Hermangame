
// CombatTest.cs (attach to CombatManager)
using UnityEngine;

public class CombatTest : MonoBehaviour
{
    public PlayerCharacter warrior;
    public Enemy rat;
    
    void Start()
    {
        // Auto-attack every 2 seconds
        InvokeRepeating(nameof(SimulateCombat), 2f, 2f);
    }

    void SimulateCombat()
    {
        if(warrior != null && rat != null)
        {
            warrior.Attack(rat);
            rat.Attack(warrior);
        }
    }
}
