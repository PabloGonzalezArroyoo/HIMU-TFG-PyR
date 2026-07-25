using UnityEngine;

public class GroundDetectorComponent : MonoBehaviour
{

    private LayerMask groundLayerMask = ~0;
    public bool isGrounded = false;

    public void SetLayer(LayerMask layer)
    {
        groundLayerMask = layer;
    }

    // Comprueba si la capa del objeto (un índice, ej. 8) está incluida
    // en la máscara de bits del LayerMask (ej. 256 si solo incluye la capa 8).
    private bool IsInGroundLayer(GameObject obj)
    {
        return (groundLayerMask.value & (1 << obj.layer)) != 0;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (IsInGroundLayer(other.gameObject)) { isGrounded = true; }
    }

    private void OnTriggerExit(Collider other)
    {
        if (IsInGroundLayer(other.gameObject)) { isGrounded = false; }
    }
}