using System.Collections.Generic;
using UnityEngine; // Might be needed if you use Vector2Int for positions

/// <summary>
/// Interface defining the contract for a party of players.
/// Allows for potential future expansion with AI parties or multiple player parties.
/// </summary>
public interface IParty
{
    /// <summary>
    /// Gets the current shared level of the party.
    /// </summary>
    int Level { get; }

    /// <summary>
    /// Gets the total accumulated experience points of the party.
    /// </summary>
    long TotalExperience { get; }

    /// <summary>
    /// Gets the list of PlayerConnection members in the party.
    /// </summary>
    List<PlayerConnection> Members { get; }

    /// <summary>
    /// Gets or sets the unique identifier for the node the party is currently occupying on the map.
    /// </summary>
    string CurrentMapNodeId { get; set; } // Or Vector2Int CurrentMapPosition { get; set; }

    /// <summary>
    /// Adds experience points to the party's total.
    /// Triggers level up checks.
    /// </summary>
    /// <param name="xpGained">The amount of XP to add.</param>
    void AddExperience(long xpGained);

    /// <summary>
    /// Checks if the accumulated XP is enough for the next level and performs the level up if so.
    /// Can be called recursively if multiple levels are gained at once.
    /// </summary>
    void CheckLevelUp();

    /// <summary>
    /// Adds a player to the party's member list.
    /// </summary>
    /// <param name="player">The PlayerConnection to add.</param>
    void AddMember(PlayerConnection player);

    /// <summary>
    /// Removes a player from the party's member list.
    /// </summary>
    /// <param name="player">The PlayerConnection to remove.</param>
    void RemoveMember(PlayerConnection player);

    /// <summary>
    /// Gets a list of members who are considered active (e.g., alive, not disconnected).
    /// Useful for combat and other gameplay checks.
    /// </summary>
    /// <returns>A list of active PlayerConnections.</returns>
    List<PlayerConnection> GetActiveMembers();

    /// <summary>
    /// Sends a message containing the current party state (level, XP, members) to all connected members.
    /// </summary>
    void SendPartyUpdateToMembers();

    /// <summary>
    /// Distributes loot to the party members.
    /// Implementation details (instant share, shared pool, etc.) can vary.
    /// </summary>
    /// <param name="items">The list of items to grant as loot.</param>
    void GrantLoot(List<ItemDefinition> items);
}