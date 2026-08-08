using System.Drawing;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Color = UnityEngine.Color;

public class MainMenuController : MonoBehaviour
{
    [SerializeField]
    private RawImage fadeObject;

    public void StartFading()
    {
        AppManager.Instance.StartFading(fadeObject, "ConnectionsScene");
    }

    public void ExitApp()
    {
        AppManager.Instance.ExitApp();
    }
}
