using UnityEngine;
using System.IO;

[System.Serializable]
public class PlayerProfile
{
    public string PlayerType;
    public string MovementStyle;
    public string BossStrategy;
}

public class BossProfileReader : MonoBehaviour
{
    public static string CurrentPlayerType = "Balanced";

    public string playerType;

    void Start()
    {
        string path =
            Application.dataPath +
            "/../AITraining/player_profile.json";

        if (File.Exists(path))
        {
            string json = File.ReadAllText(path);

            PlayerProfile profile =
                JsonUtility.FromJson<PlayerProfile>(json);

            playerType = profile.PlayerType;

            CurrentPlayerType = playerType;

            Debug.Log("Detected Player Type: " + playerType);

            ApplyBossStrategy();
        }
        else
        {
            Debug.LogWarning("player_profile.json not found!");
        }
    }

    void ApplyBossStrategy()
    {
        if (playerType == "Aggressive")
        {
            Debug.Log("Boss switched to Counter Mode");
        }
        else if (playerType == "Defensive")
        {
            Debug.Log("Boss switched to Guard Break Mode");
        }
        else
        {
            Debug.Log("Boss switched to Balanced Mode");
        }
    }
}