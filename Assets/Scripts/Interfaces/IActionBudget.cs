// File: Scripts/Gameplay/Combat/IActionBudget.cs
using System.Collections.Generic;

/// <summary>
/// Interface for entities that have a dynamic action budget (e.g., Main Actions, Bonus Actions).
/// </summary>
public interface IActionBudget
{
    /// <summary>
    /// Gets the number of main actions available this turn.
    /// </summary>
    int MainActions { get; }

    /// <summary>
    /// Gets the number of bonus actions available this turn.
    /// </summary>
    int BonusActions { get; }

    /// <summary>
    /// Gets the number of main actions remaining that can be used this turn.
    /// </summary>
    int MainActionsRemaining { get; }

    /// <summary>
    /// Gets the number of bonus actions remaining that can be used this turn.
    /// </summary>
    int BonusActionsRemaining { get; }

    /// <summary>
    /// Attempts to consume a main action.
    /// </summary>
    /// <returns>True if the action was successfully consumed, false otherwise.</returns>
    bool ConsumeMainAction();

    /// <summary>
    /// Attempts to consume a bonus action.
    /// </summary>
    /// <returns>True if the action was successfully consumed, false otherwise.</returns>
    bool ConsumeBonusAction();

    /// <summary>
    /// Modifies the current turn's action budget.
    /// Positive values add actions, negative values remove them (minimum 0).
    /// </summary>
    /// <param name="mainChange">Change in main actions.</param>
    /// <param name="bonusChange">Change in bonus actions.</param>
    void ModifyCurrentActionBudget(int mainChange, int bonusChange);

    /// <summary>
    /// Resets the action budget for the start of a new turn, typically based on the entity's base stats.
    /// </summary>
    void ResetActionBudgetForNewTurn();
}