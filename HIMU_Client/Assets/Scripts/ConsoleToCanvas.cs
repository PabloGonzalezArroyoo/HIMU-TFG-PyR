using UnityEngine;
using TMPro; // Cambia por UnityEngine.UI si usas Text clásico
using System.Collections.Generic;

public class ConsoleToCanvas : MonoBehaviour
{
    [Header("Referencia al texto del Canvas")]
    public TMP_Text consoleText;

    [Header("Configuración")]
    public int maxLines = 20;
    public bool showTimestamp = true;

    private Queue<string> logLines = new Queue<string>();

    // - Ciclo de vida -

    private void OnEnable()
    {
        Application.logMessageReceived += HandleLog;
    }

    private void OnDisable()
    {
        Application.logMessageReceived -= HandleLog;
    }

    // - Manejo de logs -

    private void HandleLog(string message, string stackTrace, LogType type)
    {
        string color = GetColor(type);
        string prefix = GetPrefix(type);
        string time = showTimestamp ? $"[{Time.time:F1}s] " : "";

        string formatted = $"<color={color}>{time}{prefix} {message}</color>";

        logLines.Enqueue(formatted);

        while (logLines.Count > maxLines)
            logLines.Dequeue();

        if (consoleText != null)
            consoleText.text = string.Join("\n", logLines);
    }

    // - Helpers -

    private string GetColor(LogType type)
    {
        return type switch
        {
            LogType.Error => "#FF5555",
            LogType.Exception => "#FF3333",
            LogType.Warning => "#FFCC44",
            _ => "#CCCCCC"
        };
    }

    private string GetPrefix(LogType type)
    {
        return type switch
        {
            LogType.Error => "[ERR]",
            LogType.Exception => "[EXC]",
            LogType.Warning => "[WRN]",
            _ => "[LOG]"
        };
    }
}