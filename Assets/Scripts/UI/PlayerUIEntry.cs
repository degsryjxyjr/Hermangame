using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerUIEntry : MonoBehaviour
{
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text roleText;
    [SerializeField] private Image readyIndicator;

    public void Initialize(string playerName, string playerRole)
    {
        nameText.text = playerName;
        roleText.text = playerRole;
        SetReadyState(false);
    }

    public void SetReadyState(bool isReady)
    {
        readyIndicator.color = isReady ? Color.green : Color.red;
    }
}