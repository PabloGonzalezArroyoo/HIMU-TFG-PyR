using UnityEngine;
using UnityEngine.InputSystem; // Requiere el paquete "Input System" instalado

/// <summary>
/// Controlador de coche basado en físicas (Rigidbody + AddForce).
/// - SPACE: acelera el coche (con inercia: tarda "timeToMaxSpeed" en llegar a velocidad máxima).
/// - Flechas IZQ/DER: orientan el coche (solo tiene efecto si el coche se está moviendo).
/// - Al soltar SPACE, el coche frena progresivamente hasta perder toda la inercia en "timeToStop".
/// </summary>
public class CarController : MonoBehaviour
{
    [Header("Movimiento / Aceleración")]
    [Tooltip("Velocidad máxima que puede alcanzar el coche (m/s)")]
    public float maxSpeed = 20f;

    [Tooltip("Tiempo (segundos) que tarda el coche en pasar de 0 a la velocidad máxima manteniendo SPACE pulsado")]
    public float timeToMaxSpeed = 2.5f;

    [Tooltip("Tiempo (segundos) que tarda el coche en perder toda su inercia al soltar SPACE")]
    public float timeToStop = 2f;

    [Header("Dirección")]
    [Tooltip("Velocidad de giro del coche en grados/segundo")]
    public float turnSpeed = 90f;

    [Tooltip("Velocidad mínima que debe tener el coche para poder girar")]
    public float minSpeedToTurn = 0.5f;

    [Header("Agarre (evita derrapes tipo hielo)")]
    [Tooltip("0 = sin agarre lateral (patina como en hielo), 1 = agarre total (elimina todo el deslizamiento lateral)")]
    [Range(0f, 1f)]
    public float lateralGrip = 0.9f;

    [Header("Suelo")]
    [Tooltip("Capas que cuentan como 'suelo' para poder acelerar/frenar")]
    public LayerMask groundLayer = ~0;

    private Rigidbody rb;
    [SerializeField]
    private GroundDetectorComponent groundDetector;

    private float accelerationForceMagnitude;
    private float brakeDecelerationMagnitude;

    private bool isAccelerating;
    private float turnInput;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        groundDetector.SetLayer(groundLayer);

        // Evita que el coche vuelque; solo se mueve y gira en el plano horizontal.
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        // Baja el centro de masa para dar más estabilidad al coche.
        rb.centerOfMass = new Vector3(0f, -0.5f, 0f);
    }

    void Start()
    {
        RecalculateForces();
    }

    void OnValidate()
    {
        // Recalcula las fuerzas si se cambian los valores desde el Inspector.
        RecalculateForces();
    }

    /// <summary>
    /// Calcula la fuerza de aceleración y la deceleración de frenado
    /// a partir de los tiempos configurados, usando F = m * a, con a = v / t.
    /// </summary>
    private void RecalculateForces()
    {
        float mass = (rb != null) ? rb.mass : 1f;
        accelerationForceMagnitude = (maxSpeed / Mathf.Max(timeToMaxSpeed, 0.01f)) * mass;
        brakeDecelerationMagnitude = maxSpeed / Mathf.Max(timeToStop, 0.01f);
    }

    void Update()
    {
        // Lectura de input usando el nuevo Input System (paquete "Input System")
        var keyboard = Keyboard.current;
        if (keyboard == null) return; // Por si no hay teclado disponible (ej. build en otra plataforma)

        isAccelerating = keyboard.spaceKey.isPressed;

        turnInput = 0f;
        if (keyboard.leftArrowKey.isPressed) turnInput -= 1f;
        if (keyboard.rightArrowKey.isPressed) turnInput += 1f;
    }

    void FixedUpdate()
    {
        if (CarGameManager.Instance.gameStarted && !CarGameManager.Instance.isPaused)
        {
            ApplyLateralGrip();
            HandleAcceleration();
            HandleSteering();
        }
    }

    /// <summary>
    /// Simula el agarre de los neumáticos: elimina (total o parcialmente) la componente
    /// de la velocidad que es perpendicular al coche, para que no derrape en curvas
    /// como si estuviera sobre hielo.
    /// </summary>
    private void ApplyLateralGrip()
    {
        if (!groundDetector.isGrounded) return; // En el aire no hay neumáticos agarrando nada

        Vector3 lateralVelocity = Vector3.Dot(rb.linearVelocity, transform.right) * transform.right;
        rb.linearVelocity -= lateralVelocity * lateralGrip;
    }

    private void HandleAcceleration()
    {
        // Proyectamos el forward del coche sobre el plano horizontal.
        // Así, si el coche queda inclinado (ej. tras un choque), la fuerza de propulsión
        // sigue siendo horizontal y nunca "empuja hacia arriba".
        Vector3 flatForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;

        // rb.linearVelocity es el nuevo nombre de rb.velocity en Unity 6
        float currentForwardSpeed = Vector3.Dot(rb.linearVelocity, flatForward);

        if (isAccelerating && groundDetector.isGrounded)
        {
            // Aplica fuerza hacia delante mientras no se haya alcanzado la velocidad máxima.
            if (currentForwardSpeed < maxSpeed)
            {
                rb.AddForce(flatForward * accelerationForceMagnitude, ForceMode.Force);
            }
        }
        else if (!isAccelerating)
        {
            // Frenado progresivo: aplica una fuerza contraria a la velocidad actual
            // hasta que el coche pierde toda su inercia (velocidad = 0).
            if (rb.linearVelocity.sqrMagnitude > 0.0025f)
            {
                Vector3 brakeDirection = -rb.linearVelocity.normalized;
                float brakeForceMag = brakeDecelerationMagnitude * rb.mass;

                rb.AddForce(brakeDirection * brakeForceMag, ForceMode.Force);

                // Evita overshoot: si el frenado va a invertir el sentido del movimiento
                // en este mismo paso de física, directamente detenemos el coche.
                Vector3 estimatedVelocity = rb.linearVelocity + (brakeDirection * brakeForceMag / rb.mass) * Time.fixedDeltaTime;
                if (Vector3.Dot(estimatedVelocity, rb.linearVelocity) < 0f)
                {
                    rb.linearVelocity = Vector3.zero;
                }
            }
            else
            {
                rb.linearVelocity = Vector3.zero;
            }
        }

        // Clamp de seguridad para no superar nunca la velocidad máxima.
        if (rb.linearVelocity.magnitude > maxSpeed)
        {
            rb.linearVelocity = rb.linearVelocity.normalized * maxSpeed;
        }
    }

    private void HandleSteering()
    {
        float currentSpeed = rb.linearVelocity.magnitude;

        // El coche solo gira si tiene algo de velocidad (como un coche real).
        if (Mathf.Abs(turnInput) > 0.01f && currentSpeed > minSpeedToTurn)
        {
            float turnAmount = turnInput * turnSpeed * Time.fixedDeltaTime;
            Quaternion turnRotation = Quaternion.Euler(0f, turnAmount, 0f);
            rb.MoveRotation(rb.rotation * turnRotation);
        }
    }
}