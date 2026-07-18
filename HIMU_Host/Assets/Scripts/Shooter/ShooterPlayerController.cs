using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Controlador básico de jugador en 3D usando físicas (Rigidbody) y el nuevo Input System.
/// - Movimiento por los ejes X/Z con W A S D.
/// - Rotación en el eje Y usando solo las flechas Izquierda/Derecha.
/// Requiere: el paquete "Input System" instalado (Window > Package Manager).
/// Coloca este script sobre el GameObject del jugador (requiere componente Rigidbody).
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class ShooterPlayerController : MonoBehaviour
{
    [Header("Movimiento")]
    [SerializeField] private float moveSpeed = 2f;

    [Header("Rotación")]
    [SerializeField] private float rotationSpeed = 100f; // grados por segundo

    private Rigidbody rb;
    private float moveX;
    private float moveZ;
    private float rotationInput;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void Update()
    {
        // El input se lee en Update para no perder pulsaciones de tecla
        ReadInput();
    }

    private void FixedUpdate()
    {
        // El movimiento físico siempre se aplica en FixedUpdate
        HandleMovement();
        HandleRotation();
    }

    private void ReadInput()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return; // no hay teclado detectado

        moveX = 0f;
        moveZ = 0f;

        if (keyboard.wKey.isPressed) moveZ += 1f;
        if (keyboard.sKey.isPressed) moveZ -= 1f;
        if (keyboard.dKey.isPressed) moveX += 1f;
        if (keyboard.aKey.isPressed) moveX -= 1f;

        rotationInput = 0f;

        // Solo flechas izquierda/derecha, nada de A/D para evitar conflicto con el movimiento
        if (keyboard.rightArrowKey.isPressed) rotationInput += 1f;
        if (keyboard.leftArrowKey.isPressed) rotationInput -= 1f;
    }

    private void HandleMovement()
    {
        // Movimiento relativo a la orientación del personaje (queda siempre en el plano X/Z)
        Vector3 direction = transform.right * moveX + transform.forward * moveZ;
        direction = Vector3.ClampMagnitude(direction, 1f);

        Vector3 newPosition = rb.position + direction * moveSpeed * Time.fixedDeltaTime;
        rb.MovePosition(newPosition);
    }

    private void HandleRotation()
    {
        float angle = rotationInput * rotationSpeed * Time.fixedDeltaTime;
        Quaternion deltaRotation = Quaternion.Euler(0f, angle, 0f);

        rb.MoveRotation(rb.rotation * deltaRotation);
    }
}