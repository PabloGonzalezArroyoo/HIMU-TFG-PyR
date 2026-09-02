using System.Drawing;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Color = UnityEngine.Color;

public class MainMenuController : MonoBehaviour
{
    /// <summary>
    /// Reference to the UI element used for fade out
    /// </summary>
    [SerializeField] private RawImage fadeObject;

    #region Methods
    /// <summary>
    /// Method to transition to Connections Scene
    /// </summary>
    public void StartFading()
    {
        AppManager.Instance.StartFading(fadeObject, "ConnectionSelectionScene");
    }

    /// <summary>
    /// Method to close the app
    /// </summary>
    public void ExitApp()
    {
        AppManager.Instance.ExitApp();
    }
    #endregion
}
