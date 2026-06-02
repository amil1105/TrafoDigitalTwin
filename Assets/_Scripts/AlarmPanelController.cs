using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class AlarmPanelController : MonoBehaviour
{
    public enum AlarmSeverity
    {
        Info,
        Warning,
        Critical
    }

    [Header("UI References")]
    public TMP_Text alarmListText;
    public TMP_Text alarmCountText;

    [Header("Settings")]
    public int maxAlarmRows = 8;

    private readonly List<string> alarms = new List<string>();

    public void AddAlarm(string message, AlarmSeverity severity = AlarmSeverity.Warning)
    {
        string color = severity == AlarmSeverity.Critical ? "#FF6961" :
                       severity == AlarmSeverity.Warning ? "#FFD166" : "#8EF58C";
        string stamp = System.DateTime.Now.ToString("HH:mm:ss");
        alarms.Insert(0, $"<color={color}>[{stamp}] {message}</color>");

        while (alarms.Count > maxAlarmRows)
            alarms.RemoveAt(alarms.Count - 1);

        RefreshUI();
    }

    public void ClearAlarms()
    {
        alarms.Clear();
        RefreshUI();
    }

    public void RemoveAlarmsContaining(string text)
    {
        if (string.IsNullOrEmpty(text))
            return;

        alarms.RemoveAll(alarm => alarm.Contains(text));
        RefreshUI();
    }

    public IReadOnlyList<string> GetAlarms()
    {
        return alarms;
    }

    public void RefreshUI()
    {
        if (alarmListText != null)
        {
            alarmListText.text = alarms.Count == 0
                ? "<color=#728087>No active alarms</color>"
                : string.Join("\n", alarms);
        }

        if (alarmCountText != null)
            alarmCountText.text = alarms.Count.ToString();
    }

    void Start()
    {
        RefreshUI();
    }
}
