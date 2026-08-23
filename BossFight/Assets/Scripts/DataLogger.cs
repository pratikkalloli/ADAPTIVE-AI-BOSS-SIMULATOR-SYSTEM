using UnityEngine;
using System.IO;

public class DataLogger : MonoBehaviour
{
    [Header("Assign Boss Transform Here")]
    public Transform boss;

    private string filePath;

    void Start()
    {
        filePath = Application.persistentDataPath + "/combat_logs.csv";

        if (!File.Exists(filePath))
        {
            File.WriteAllText(
                filePath,
               "Time,Action,PlayerX,PlayerY,BossX,BossY,Distance,MoveX,MoveY,Jump,Block,Sprint,PlayerHP,BossHP\n"
            );
        }

        Debug.Log("CSV File Path: " + filePath);

        if (boss == null)
        {
            Debug.LogWarning("Boss Transform NOT assigned in Inspector!");
        }
        else
        {
            Debug.Log("Boss Found: " + boss.name);
        }
    }

    public void LogPlayerState(
        string action,
        float horizontal,
        float vertical,
        bool jump,
        bool block,
        bool sprint,
        int playerHP,
int bossHP)
    {
        try
        {
            float playerX = transform.position.x;
            float playerY = transform.position.y;

            float bossX = 0f;
            float bossY = 0f;
            float distance = 0f;

            if (boss != null)
            {
                bossX = boss.position.x;
                bossY = boss.position.y;

                distance = Vector3.Distance(
                    transform.position,
                    boss.position
                );
            }

            Debug.Log(
                "Player Pos: " + playerX + "," + playerY +
                " | Boss Pos: " + bossX + "," + bossY +
                " | Distance: " + distance
            );

            string row =
                Time.time + "," +
                action + "," +
                playerX + "," +
                playerY + "," +
                bossX + "," +
                bossY + "," +
                distance + "," +
                horizontal + "," +
                vertical + "," +
                jump + "," +
                block + "," +
                sprint + "," +
                playerHP + "," +
                bossHP + "\n";

            File.AppendAllText(filePath, row);
        }
        catch (System.Exception e)
        {
            Debug.LogError("Logger Error: " + e.Message);
        }
    }
}