using System;
using System.Globalization;
using System.IO;
using UnityEngine;

public class DataLogger : MonoBehaviour
{
    private const string SchemaVersion = "2";

    private static readonly string[] Columns =
    {
        "schema_version", "episode_id", "event_index", "sim_time", "event_type",
        "actor", "attack_sequence_id", "swing_id", "action_name", "accepted",
        "rejection_reason", "target", "player_x", "player_y", "player_z",
        "boss_x", "boss_y", "boss_z", "distance", "move_x", "move_y",
        "jump", "block", "sprint", "player_hp_before", "player_hp_after",
        "boss_hp_before", "boss_hp_after", "effective_damage", "reward",
        "terminated", "outcome", "reference_status"
    };

    [Header("Assign Boss Transform Here")]
    public Transform boss;

    public static DataLogger Instance { get; private set; }
    public string EpisodeId { get; private set; }
    public string FilePath { get { return filePath; } }

    private string filePath;
    private Transform playerTransform;
    private CombatManager combatManager;
    private int eventIndex;
    private bool episodeStarted;
    private bool episodeEnded;
    private bool missingBossWarningLogged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Multiple DataLogger components are active; the first logger remains authoritative.");
            return;
        }

        Instance = this;
        playerTransform = transform;
        combatManager = FindObjectOfType<CombatManager>();

        if (boss == null)
        {
            GameObject bossObject = GameObject.Find("NightmareDragon");
            if (bossObject != null)
            {
                boss = bossObject.transform;
            }
        }

        InitializeEpisodeFile();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void InitializeEpisodeFile()
    {
        try
        {
            Directory.CreateDirectory(Application.persistentDataPath);

            // A unique, schema-versioned file per fight keeps legacy and malformed CSVs untouched.
            for (int attempt = 0; attempt < 3; attempt++)
            {
                EpisodeId = Guid.NewGuid().ToString("N");
                filePath = Path.Combine(
                    Application.persistentDataPath,
                    "combat_logs_v2_" + EpisodeId + ".csv");

                try
                {
                    using (FileStream stream = new FileStream(filePath, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
                    using (StreamWriter writer = new StreamWriter(stream))
                    {
                        writer.WriteLine(string.Join(",", Columns));
                    }
                    Debug.Log("Combat telemetry CSV: " + filePath);
                    return;
                }
                catch (IOException)
                {
                    if (!File.Exists(filePath))
                    {
                        throw;
                    }
                }
            }

            throw new IOException("Could not reserve a unique telemetry filename after three attempts.");
        }
        catch (Exception exception)
        {
            filePath = null;
            Debug.LogError("Telemetry file initialization failed: " + exception.Message);
        }
    }

    public void BeginEpisode(int playerHP, int bossHP)
    {
        if (episodeStarted)
        {
            return;
        }

        episodeStarted = true;
        WriteEvent(
            "EpisodeStart", "system", -1, -1, "fight", null, null, null,
            null, null, null, null, null, playerHP, playerHP, bossHP, bossHP,
            null, null, false, null);
    }

    public void BeginNextEpisode(int playerHP, int bossHP)
    {
        if (!episodeEnded)
        {
            Debug.LogWarning("A new telemetry episode was requested before the previous episode ended.");
            return;
        }

        episodeStarted = false;
        episodeEnded = false;
        eventIndex = 0;
        missingBossWarningLogged = false;
        filePath = null;
        InitializeEpisodeFile();
        BeginEpisode(playerHP, bossHP);
    }

    public void LogEpisodeEnd(string outcome, int playerHP, int bossHP)
    {
        if (episodeEnded)
        {
            return;
        }

        if (!episodeStarted)
        {
            BeginEpisode(playerHP, bossHP);
        }

        WriteEvent(
            "EpisodeEnd", "system", -1, -1, "fight", null, null, null,
            null, null, null, null, null, playerHP, playerHP, bossHP, bossHP,
            0, null, true, outcome);
        episodeEnded = true;
    }

    public void LogPlayerAttackAttempt(int sequenceId, int playerHP, int bossHP)
    {
        LogPlayerAttackEvent("PlayerAttackAttempt", sequenceId, null, null, null, playerHP, bossHP);
    }

    public void LogPlayerAttackOutcome(
        int sequenceId,
        bool accepted,
        string rejectionReason,
        string target,
        int playerHP,
        int bossHP)
    {
        LogPlayerAttackEvent(
            accepted ? "PlayerAttackAccepted" : "PlayerAttackRejected",
            sequenceId,
            accepted,
            rejectionReason,
            target,
            playerHP,
            bossHP);
    }

    private void LogPlayerAttackEvent(
        string eventType,
        int sequenceId,
        bool? accepted,
        string rejectionReason,
        string target,
        int playerHP,
        int bossHP)
    {
        WriteEvent(
            eventType, "player", sequenceId, -1, "Attack", accepted,
            rejectionReason, target,
            Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"),
            Input.GetKey(KeyCode.Space), Input.GetMouseButton(1),
            Input.GetKey(KeyCode.LeftShift),
            playerHP >= 0 ? (int?)playerHP : null,
            playerHP >= 0 ? (int?)playerHP : null,
            bossHP >= 0 ? (int?)bossHP : null,
            bossHP >= 0 ? (int?)bossHP : null,
            null, null, false, null);
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
        bool isAttack = action == "Attack";
        WriteEvent(
            isAttack ? "PlayerAttackAccepted" : "PlayerAction",
            "player", -1, -1, action,
            isAttack ? (bool?)true : null, null, null,
            horizontal, vertical, jump, block, sprint,
            playerHP, playerHP, bossHP, bossHP, null, null, false, null);
    }

    public void LogBossAction(
        string eventType,
        int swingId,
        string actionName,
        bool? accepted = null,
        string rejectionReason = null,
        string target = null)
    {
        CombatManager manager = GetCombatManager();
        int? playerHP = manager != null && manager.getHealth() >= 0 ? (int?)manager.getHealth() : null;
        int? bossHP = manager != null && manager.getDragonHP() >= 0 ? (int?)manager.getDragonHP() : null;

        WriteEvent(
            eventType, "boss", -1, swingId, actionName, accepted,
            rejectionReason, target, null, null, null, null, null,
            playerHP, playerHP, bossHP, bossHP, null, null, false, null);
    }

    public void LogDamageEvent(
        string eventType,
        string actor,
        int attackSequenceId,
        int swingId,
        string actionName,
        string target,
        int playerHPBefore,
        int playerHPAfter,
        int bossHPBefore,
        int bossHPAfter,
        string rejectionReason = null)
    {
        int effectiveDamage = actor == "player"
            ? Mathf.Max(0, bossHPBefore - bossHPAfter)
            : Mathf.Max(0, playerHPBefore - playerHPAfter);

        WriteEvent(
            eventType, actor, attackSequenceId, swingId, actionName,
            eventType.EndsWith("Rejected", StringComparison.Ordinal) ? (bool?)false : true,
            eventType.EndsWith("Rejected", StringComparison.Ordinal) ? rejectionReason : null,
            target, null, null, null, null, null,
            playerHPBefore, playerHPAfter, bossHPBefore, bossHPAfter,
            effectiveDamage, null, false, null);
    }

    private void WriteEvent(
        string eventType,
        string actor,
        int attackSequenceId,
        int swingId,
        string actionName,
        bool? accepted,
        string rejectionReason,
        string target,
        float? moveX,
        float? moveY,
        bool? jump,
        bool? block,
        bool? sprint,
        int? playerHPBefore,
        int? playerHPAfter,
        int? bossHPBefore,
        int? bossHPAfter,
        int? effectiveDamage,
        float? reward,
        bool terminated,
        string outcome)
    {
        if (episodeEnded)
        {
            return;
        }

        if (string.IsNullOrEmpty(filePath))
        {
            return;
        }

        string playerReferenceStatus;
        string bossReferenceStatus;
        string[] playerPosition = GetPosition(playerTransform, "player", out playerReferenceStatus);
        string[] bossPosition = GetPosition(boss, "boss", out bossReferenceStatus);
        string referenceStatus = playerReferenceStatus == "ok" && bossReferenceStatus == "ok"
            ? "ok"
            : playerReferenceStatus + "|" + bossReferenceStatus;
        string distance = "na";
        if (playerTransform != null && boss != null && IsFinite(playerTransform.position) && IsFinite(boss.position))
        {
            float value = Vector3.Distance(playerTransform.position, boss.position);
            distance = IsFinite(value) ? Number(value) : "na";
        }

        string[] fields =
        {
            SchemaVersion,
            EpisodeId,
            eventIndex.ToString(CultureInfo.InvariantCulture),
            Number(Time.time),
            ValueOrNA(eventType),
            ValueOrNA(actor),
            IntegerOrNA(attackSequenceId),
            IntegerOrNA(swingId),
            ValueOrNA(actionName),
            BoolOrNA(accepted),
            ValueOrNA(rejectionReason),
            ValueOrNA(target),
            playerPosition[0], playerPosition[1], playerPosition[2],
            bossPosition[0], bossPosition[1], bossPosition[2],
            distance,
            NullableNumber(moveX), NullableNumber(moveY),
            BoolOrNA(jump), BoolOrNA(block), BoolOrNA(sprint),
            NullableInteger(playerHPBefore), NullableInteger(playerHPAfter),
            NullableInteger(bossHPBefore), NullableInteger(bossHPAfter),
            NullableInteger(effectiveDamage), NullableNumber(reward),
            terminated ? "true" : "false", ValueOrNA(outcome), referenceStatus
        };

        if (fields.Length != Columns.Length)
        {
            Debug.LogError("Telemetry schema invariant failed: expected " + Columns.Length + " fields, got " + fields.Length + ".");
            return;
        }

        string[] escapedFields = new string[fields.Length];
        for (int i = 0; i < fields.Length; i++)
        {
            escapedFields[i] = CsvEscape(fields[i]);
        }

        try
        {
            File.AppendAllText(filePath, string.Join(",", escapedFields) + "\n");
            eventIndex++;
        }
        catch (Exception exception)
        {
            Debug.LogError("Telemetry write failed: " + exception.Message);
        }
    }

    private string[] GetPosition(Transform target, string label, out string referenceStatus)
    {
        Vector3 position;
        if (target == null)
        {
            referenceStatus = label + "_transform_missing";
            if (label == "boss" && !missingBossWarningLogged)
            {
                missingBossWarningLogged = true;
                Debug.LogWarning("Telemetry cannot record boss position: boss Transform is missing.");
            }
            return new[] { "na", "na", "na" };
        }

        position = target.position;
        if (!IsFinite(position))
        {
            referenceStatus = label + "_position_invalid";
            return new[] { "na", "na", "na" };
        }

        referenceStatus = "ok";
        return new[] { Number(position.x), Number(position.y), Number(position.z) };
    }

    private CombatManager GetCombatManager()
    {
        if (combatManager == null)
        {
            combatManager = FindObjectOfType<CombatManager>();
        }
        return combatManager;
    }

    private static bool IsFinite(Vector3 value)
    {
        return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private static string Number(float value)
    {
        return IsFinite(value) ? value.ToString("R", CultureInfo.InvariantCulture) : "na";
    }

    private static string NullableNumber(float? value)
    {
        return value.HasValue ? Number(value.Value) : "na";
    }

    private static string NullableInteger(int? value)
    {
        return value.HasValue ? value.Value.ToString(CultureInfo.InvariantCulture) : "na";
    }

    private static string IntegerOrNA(int value)
    {
        return value >= 0 ? value.ToString(CultureInfo.InvariantCulture) : "na";
    }

    private static string BoolOrNA(bool? value)
    {
        return value.HasValue ? value.Value.ToString().ToLowerInvariant() : "na";
    }

    private static string ValueOrNA(string value)
    {
        return string.IsNullOrEmpty(value) ? "na" : value;
    }

    private static string CsvEscape(string value)
    {
        if (value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) < 0)
        {
            return value;
        }
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
