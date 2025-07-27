// File: Scripts/Gameplay/Combat/IActionBudget.cs
/// <summary>
/// Represents an entity's action economy system, handling variable-cost actions per turn.
/// Replaces legacy main/bonus action system with unified action points.
/// </summary>
public interface IActionBudget
{
    /// <summary>
    /// The base number of actions this entity receives at the start of each turn.
    /// </summary>
    /// <example>
    /// A standard character might have 3 TotalActions by default.
    /// </example>
    int TotalActions { get; }

    /// <summary>
    /// The number of actions currently available to spend this turn.
    /// </summary>
    /// <remarks>
    /// This value is typically reduced by ConsumeAction() and modified by ModifyCurrentActionBudget().
    /// </remarks>
    int ActionsRemaining { get; }

    /// <summary>
    /// Attempts to consume actions from the current budget.
    /// </summary>
    /// <param name="cost">The action point cost to deduct (defaults to 1 for standard actions)</param>
    /// <returns>
    /// True if the entity had sufficient actions and they were consumed.
    /// False if the cost couldn't be paid (no action taken).
    /// </returns>
    /// <example>
    /// // Standard attack (1 action)
    /// if (character.ConsumeAction()) Attack();
    /// 
    /// // Powerful ability (2 actions)
    /// if (character.ConsumeAction(2)) UltimateAbility();
    /// </example>
    bool ConsumeAction(int cost = 1);

    /// <summary>
    /// Directly modifies the current action budget (positive or negative values).
    /// </summary>
    /// <param name="change">
    /// Delta to apply to ActionsRemaining.
    /// Positive values add actions, negative values remove.
    /// </param>
    /// <remarks>
    /// Clamps to minimum 0. Does not affect TotalActions.
    /// Useful for temporary buffs/debuffs.
    /// </remarks>
    void ModifyCurrentActionBudget(int change);

    /// <summary>
    /// Resets the action budget at the start of a new turn.
    /// </summary>
    /// <remarks>
    /// Typically sets ActionsRemaining = TotalActions.
    /// May include additional reset logic in implementations.
    /// </remarks>
    void ResetActionBudgetForNewTurn();
}