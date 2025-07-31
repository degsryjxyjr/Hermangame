using UnityEngine;

public class MapPlaceHolderSwitchScene : MonoBehaviour
{
    // Drag this method into each button via the Inspector
    public void GoToCombat() => GameStateManager.Instance.ChangeState(GameStateManager.GameState.Combat);
    public void GoToShop() => GameStateManager.Instance.ChangeState(GameStateManager.GameState.Shop);
    public void GoToLobby() => GameStateManager.Instance.ChangeState(GameStateManager.GameState.Lobby);
    public void GoToLoot() => GameStateManager.Instance.ChangeState(GameStateManager.GameState.Loot);
    public void GoToMap() => GameStateManager.Instance.ChangeState(GameStateManager.GameState.Map);

    /* debug start button */
    public void DebugStartGame()
    {
        // initialise every connected player’s data, then jump to Map
        PlayerManager.Instance.InitializeGameData(PlayerManager.Instance.GetAllPlayers());
        GameStateManager.Instance.ChangeState(GameStateManager.GameState.Map);

    }
}
