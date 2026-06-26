using System.Diagnostics;
using UnityEngine;

public class ServerLauncher : MonoBehaviour
{
    [SerializeField] string batRelativePath = "start-server.bat";

    public void LaunchServer()
    {
        string batPath = System.IO.Path.Combine(Application.dataPath, "..", batRelativePath);

        ProcessStartInfo psi = new ProcessStartInfo
        {
            FileName = batPath,
            WorkingDirectory = System.IO.Path.GetDirectoryName(batPath),
            UseShellExecute = true, // necesario para que abra las ventanas cmd visibles
            CreateNoWindow = false
        };

        try
        {
            Process.Start(psi);
            UnityEngine.Debug.Log("[ServerLauncher] Script lanzado correctamente.");
        }
        catch (System.Exception ex)
        {
            UnityEngine.Debug.LogError($"[ServerLauncher] Error al lanzar el bat: {ex.Message}");
        }
    }
}