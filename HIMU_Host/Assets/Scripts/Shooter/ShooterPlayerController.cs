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

    #region Variables

    [Header("Movimiento")]
    [SerializeField] private float moveSpeed = 2f;

    [Header("Rotación")]
    [SerializeField] private float rotationSpeed = 100f; // grados por segundo

    /// <summary>
    /// When true the local keyboard is merged into the remote state. Intended for editor
    /// testing without a connected device; should be disabled for the defence build.
    /// </summary>
    [Header("Debug")]
    [SerializeField] private bool keyboardFallback = true;

    private Rigidbody rb;
    private ShootingComponent shtComp;

    /// <summary>
    /// Virtual device driving this player. Null while the player has no control scene bound.
    /// </summary>
    private RemoteControlRig controlSource;

    /// <summary>
    /// State sampled in Update and consumed in FixedUpdate.
    /// </summary>
    private ShootingState state;

    #endregion

    /// <summary>
    /// Injects the virtual device that drives this player.
    /// </summary>
    /// <param name="source">Control source, or null to detach the player from remote input.</param>
    public void SetControlSource(RemoteControlRig source)
    {
        controlSource = source;
    }

    /// <summary>
    /// Samples the control state for this frame. Kept in Update (not FixedUpdate) so that no
    /// edge-triggered action can be missed when the physics step and the render step diverge.
    /// </summary>
    private void ReadInput()
    {
        state = default;

        if (controlSource != null && controlSource != null)
            state = controlSource.GetShootingState();

        if (keyboardFallback) MergeKeyboard(ref state);

        // The weapon enforces its own cooldown, so holding the button yields automatic fire,
        // matching the previous keyboard behaviour.
        if (state.shootHeld && shtComp != null) shtComp.Shoot();
    }

    /// <summary>
    /// Adds the local keyboard on top of the remote state, for editor testing.
    /// </summary>
    /// <param name="target">State to merge the keyboard input into.</param>
    private void MergeKeyboard(ref ShootingState target)
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard.wKey.isPressed) target.move.y += 1f;
        if (keyboard.sKey.isPressed) target.move.y -= 1f;
        if (keyboard.dKey.isPressed) target.move.x += 1f;
        if (keyboard.aKey.isPressed) target.move.x -= 1f;

        if (keyboard.rightArrowKey.isPressed) target.rotation += 1f;
        if (keyboard.leftArrowKey.isPressed) target.rotation -= 1f;

        if (keyboard.spaceKey.isPressed) target.shootHeld = true;
        if (keyboard.spaceKey.wasPressedThisFrame) target.shootPressed = true;

        target.move = Vector2.ClampMagnitude(target.move, 1f);
        target.rotation = Mathf.Clamp(target.rotation, -1f, 1f);
    }

    private void HandleMovement()
    {
        // Movimiento relativo a la orientación del personaje (queda siempre en el plano X/Z)
        Vector3 direction = transform.right * state.move.x + transform.forward * state.move.y;
        direction = Vector3.ClampMagnitude(direction, 1f);

        Vector3 newPosition = rb.position + direction * moveSpeed * Time.fixedDeltaTime;
        rb.MovePosition(newPosition);
    }

    private void HandleRotation()
    {
        float angle = state.rotation * rotationSpeed * Time.fixedDeltaTime;
        Quaternion deltaRotation = Quaternion.Euler(0f, angle, 0f);

        rb.MoveRotation(rb.rotation * deltaRotation);
    }

    #region Monobehaviour

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        shtComp = GetComponent<ShootingComponent>();
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

    #endregion
}