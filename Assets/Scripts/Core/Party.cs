using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Represents a group of players moving and progressing together in the game.
/// Manages shared state like level, experience, and map position.
/// </summary>
[System.Serializable] // Serializable if you want to potentially save/load party state easily later
public class Party : IParty
{
    // --- Constants ---
    // Define how much XP is needed for each level. This is a simple example.
    // You might want this configurable per party type or based on a formula/curve later.
    private static readonly long[] XP_REQUIREMENTS = {
        0,       // Level 1
        100,     // Level 2
        200,     // Level 3
        300,    // Level 4
        400,    // Level 5
        500,   // Level 6
        600,   // Level 7
        700,   // Level 8
        800,   // Level 9
        900,   // Level 10
        // ... Add more as needed or use a formula
    };

    // --- IParty Implementation ---

    /// <inheritdoc />
    public int Level { get; private set; } = 1;

    /// <inheritdoc />
    public long TotalExperience { get; private set; } = 0;

    /// <inheritdoc />
    public List<PlayerConnection> Members { get; private set; } = new List<PlayerConnection>();

    /// <inheritdoc />
    public string CurrentMapNodeId { get; set; } = ""; // Default to an empty string or a starting node ID

    /// <inheritdoc />
    public void AddExperience(long xpGained)
    {
        if (xpGained <= 0) return; // Prevent negative or zero XP gain

        TotalExperience += xpGained;
        Debug.Log($"Party gained {xpGained} XP. Total XP: {TotalExperience}");
        CheckLevelUp(); // Check for level up after gaining XP
        SendPartyUpdateToMembers(); // Notify members of XP change
    }

    /// <inheritdoc />
    public void CheckLevelUp()
    {
        int maxLevelIndex = XP_REQUIREMENTS.Length - 1;
        // Check if the party has enough XP for the *next* level, up to the defined max
        while (Level <= maxLevelIndex && TotalExperience >= XP_REQUIREMENTS[Level])
        {
            Level++;
            Debug.Log($"Party leveled up! New Level: {Level}");
            // TODO: Potentially grant stat bonuses based on PlayerClassDefinition growth curves
            // This would involve iterating Members and calling methods on their GameData/Entities
            // For now, just log. Implement stat bonus logic here later.
            foreach (var member in Members)
            {
                if (member != null && GameServer.Instance != null)
                {
                    // call levelUp function for each player and pass the new level
                    member.LevelUp(Level);
                }
            }


            // Send a specific level up notification
            BroadcastLevelUp();
        }
    }

    /// <inheritdoc />
    public void AddMember(PlayerConnection player)
    {
        if (player != null && !Members.Contains(player))
        {
            Members.Add(player);
            player.CurrentParty = this; // Link the player back to this party
            Debug.Log($"Player {player.LobbyData?.Name ?? player.NetworkId} added to party.");
            // Consider sending an update if party composition changes need immediate client notification
            // SendPartyUpdateToMembers();
        }
    }

    /// <inheritdoc />
    public void RemoveMember(PlayerConnection player)
    {
        if (player != null && Members.Contains(player))
        {
            Members.Remove(player);
            player.CurrentParty = null; // Unlink the player from this party
            Debug.Log($"Player {player.LobbyData?.Name ?? player.NetworkId} removed from party.");
            // Consider sending an update
            // SendPartyUpdateToMembers();
        }
    }

    /// <inheritdoc />
    public List<PlayerConnection> GetActiveMembers()
    {
        // Example: Filter out disconnected players or dead players if relevant
        // For now, assuming all members in the list are considered active for party purposes
        // unless explicitly marked otherwise (e.g., by a 'IsAlive' flag on PlayerConnection or GameData)
        // You might refine this logic based on your specific needs in combat/map.
        return Members.Where(m => m != null /* && m.IsAlive() && m.IsConnected() */ ).ToList();
    }


    /// <inheritdoc />
    public void SendPartyUpdateToMembers()
    {
        var partyUpdateData = new Dictionary<string, object>
        {
            ["type"] = "party_update",
            ["party"] = new Dictionary<string, object>
            {
                ["level"] = this.Level,
                ["totalExperience"] = this.TotalExperience,
                // Add required XP for next level if needed
                // ["requiredExperience"] = GetRequiredXpForNextLevel(),
                ["members"] = this.Members.Select(m => new Dictionary<string, object>
                {
                    ["id"] = m.NetworkId,
                    ["name"] = m.LobbyData?.Name ?? "Unknown"
                    // Add other relevant member info if needed by client
                }).ToList()
            }
        };

        foreach (var member in Members)
        {
            if (member != null && GameServer.Instance != null) // Check for nulls
            {
                //Also sending a statsUpdate as "party_update" not implemented on client side
                member.SendStatsUpdateToClient();
                GameServer.Instance.SendToPlayer(member.NetworkId, partyUpdateData);
            }
        }
    }
    
    /// <summary>
    /// Distributes a list of loot items to the active members of the party.
    /// Ensures that every active member receives at least one item by cycling through the provided list if necessary.
    /// Any excess items beyond the number of members are distributed randomly among the active members.
    /// Each member receives a personalized notification listing the specific items they received.
    /// </summary>
    /// <param name="items">The list of ItemDefinitions to distribute.</param>
    public void GrantLoot(List<ItemDefinition> items)
    {
        if (items == null || items.Count == 0)
        {
            Debug.Log("Party.GrantLoot: No items to distribute.");
            return;
        }

        // Get active members to ensure loot goes to players who can receive it
        List<PlayerConnection> activeMembers = this.GetActiveMembers();

        if (activeMembers == null || activeMembers.Count == 0)
        {
            Debug.LogWarning("Party.GrantLoot: No active members to distribute loot to.");
            return;
        }

        Debug.Log($"Party.GrantLoot: Distributing {items.Count} items to {activeMembers.Count} active members (ensuring everyone gets at least one if possible).");

        // --- Distribute Items Ensuring Everyone Gets At Least One (if items list is not empty) ---
        System.Random rng = new System.Random(); // Use System.Random for consistent randomness

        // Prepare a list to hold messages for each member about items they received
        Dictionary<PlayerConnection, List<ItemDefinition>> memberLoot = new Dictionary<PlayerConnection, List<ItemDefinition>>();

        // 1. Ensure each active member gets at least one item (cycle through items if necessary)
        if (items != null && items.Count > 0) // Double-check items list is valid
        {
            for (int i = 0; i < activeMembers.Count; i++)
            {
                PlayerConnection member = activeMembers[i];
                // Cycle through the items list using modulo
                ItemDefinition itemToGive = items[i % items.Count];

                if (itemToGive != null && member != null)
                {
                    // Add the item to the member's inventory
                    if (InventoryService.Instance != null)
                    {
                        bool itemAdded = InventoryService.Instance.AddItem(member.NetworkId, itemToGive, 1);

                        if (itemAdded)
                        {
                            Debug.Log($"Party.GrantLoot: Ensured item '{itemToGive.displayName}' given to player '{member.LobbyData?.Name ?? member.NetworkId}' (Item #{i + 1}/{activeMembers.Count}).");

                            // Track the item for the member's loot notification
                            if (!memberLoot.ContainsKey(member))
                            {
                                memberLoot[member] = new List<ItemDefinition>();
                            }
                            memberLoot[member].Add(itemToGive);
                        }
                        else
                        {
                            Debug.LogWarning($"Party.GrantLoot: Failed to add ensured item '{itemToGive.displayName}' to player '{member.LobbyData?.Name ?? member.NetworkId}' inventory.");
                        }
                    }
                    else
                    {
                        Debug.LogError("Party.GrantLoot: InventoryService.Instance is null during ensure-everyone-gets-one phase.");
                    }
                }
            }
        }

        // 2. Distribute any remaining items randomly (only if there were more items than members)
        if (items.Count > activeMembers.Count)
        {
            int extraItemsToDistribute = items.Count - activeMembers.Count;
            Debug.Log($"Party.GrantLoot: Distributing {extraItemsToDistribute} extra items randomly.");

            for (int i = activeMembers.Count; i < items.Count; i++)
            {
                ItemDefinition extraItem = items[i];
                if (extraItem == null) continue; // Safety check

                // Select a random active member to receive this extra item
                int randomIndex = rng.Next(activeMembers.Count);
                PlayerConnection recipient = activeMembers[randomIndex];

                // Add the item to the recipient's inventory
                if (InventoryService.Instance != null && recipient != null)
                {
                    bool itemAdded = InventoryService.Instance.AddItem(recipient.NetworkId, extraItem, 1);

                    if (itemAdded)
                    {
                        Debug.Log($"Party.GrantLoot: Extra item '{extraItem.displayName}' randomly granted to player '{recipient.LobbyData?.Name ?? recipient.NetworkId}'.");

                        // Track the item for the recipient's loot notification
                        if (!memberLoot.ContainsKey(recipient))
                        {
                            memberLoot[recipient] = new List<ItemDefinition>();
                        }
                        memberLoot[recipient].Add(extraItem);
                    }
                    else
                    {
                        Debug.LogWarning($"Party.GrantLoot: Failed to add extra item '{extraItem.displayName}' to player '{recipient.LobbyData?.Name ?? recipient.NetworkId}' inventory.");
                    }
                }
                else
                {
                    Debug.LogError("Party.GrantLoot: InventoryService.Instance or recipient is null during extra item distribution phase.");
                }
            }
        }


        // --- Notify Members of Their Loot ---
        // Send a personalized loot message to each member who received items
        foreach (var memberLootEntry in memberLoot)
        {
            PlayerConnection member = memberLootEntry.Key;
            List<ItemDefinition> receivedItems = memberLootEntry.Value;

            if (member != null && GameServer.Instance != null && receivedItems != null && receivedItems.Count > 0)
            {
                var memberLootData = new Dictionary<string, object>
                {
                    ["type"] = "loot_received",
                    ["items"] = receivedItems.Select(item => new Dictionary<string, object>
                    {
                        ["id"] = item.name, // Or a unique ID if you have one
                        ["name"] = item.displayName,
                        ["iconPath"] = item.icon != null ? $"images/icons/{item.icon.name}.png" : "images/icons/default-item.png"
                        // Add other item properties relevant to the client
                    }).ToList()
                };

                GameServer.Instance.SendToPlayer(member.NetworkId, memberLootData);
                Debug.Log($"Party.GrantLoot: Sent loot notification to player '{member.LobbyData?.Name ?? member.NetworkId}' for {receivedItems.Count} item(s).");
            }
        }

        Debug.Log($"Party.GrantLoot: Finished distributing items (everyone got at least one if items existed).");
    }


    // --- Constructor ---

    /// <summary>
    /// Initializes a new instance of the Party class with an initial list of members.
    /// </summary>
    /// <param name="initialMembers">The list of PlayerConnections to add to the party initially.</param>
    public Party(List<PlayerConnection> initialMembers)
    {
        if (initialMembers != null)
        {
            foreach (var member in initialMembers)
            {
                AddMember(member); // Use AddMember to ensure proper linking
            }
        }
        // Initialize other party state if needed (e.g., starting map node)
        // CurrentMapNodeId = "start_node"; // Example
    }

    // --- Helper Methods ---

    // Example helper to get XP needed for the *next* level
    
    public long GetRequiredXpForNextLevel()
    {
        if (Level < XP_REQUIREMENTS.Length)
        {
            return XP_REQUIREMENTS[Level];
        }
        // If beyond defined levels, you could use a formula or return a large number/long.MaxValue
        return long.MaxValue;
    }
    

    // Example helper to broadcast level up event
    private void BroadcastLevelUp()
    {
         var levelUpData = new Dictionary<string, object>
         {
             ["type"] = "level_up",
             ["partyLevel"] = this.Level,
             // TODO: Include stat increases if calculated
             // ["statIncreases"] = new Dictionary<string, int> { {"health", 10}, {"attack", 2} } // Example
         };

         foreach (var member in Members)
         {
             if (member != null && GameServer.Instance != null)
             {
                 GameServer.Instance.SendToPlayer(member.NetworkId, levelUpData);
             }
         }
    }
}