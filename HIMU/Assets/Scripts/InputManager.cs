using UnityEngine;

public class InputManager : MonoBehaviour
{
    // Singleton
    public static InputManager Instance
    {
        get
        {
            return instance;
        }
    }
    private static InputManager instance = null;


    public void OnInputReceived(string device, string message)
    {
        UnityEngine.Debug.Log($"[Host] Mensaje de {device}: {message}");
        // → Aquí puedes parsear el mensaje y actualizar el estado del juego
    }
}
