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
    public static CarController Instance { get; private set; }

    [SerializeField]
    public float maxSpeed = 20f;

    [SerializeField]
    public float timeToMaxSpeed = 2.5f;

    [SerializeField]
    public float timeToStop = 2f;

    [SerializeField]
    public float turnSpeed = 90f;

    [SerializeField]
    public float minSpeedToTurn = 0.5f;

    [SerializeField]
    [Range(0f, 1f)]
    public float lateralGrip = 0.9f;

    [SerializeField]
    public LayerMask groundLayer = ~0;

    private Rigidbody rb;
    [SerializeField]
    private GroundDetectorComponent groundDetector;

    private float accelerationForceMagnitude;
    private float brakeDecelerationMagnitude;

    public bool isAccelerating;
    public bool turnLeft = false;
    public bool turnRight = false;
    private float turnInput;

    #region Physics
    /// <summary>
    /// Calcula la fuerza de aceleración y la deceleración de frenado
    /// </summary>
    private void RecalculateForces()
    {
        float mass = (rb != null) ? rb.mass : 1f;
        accelerationForceMagnitude = (maxSpeed / Mathf.Max(timeToMaxSpeed, 0.01f)) * mass;
        brakeDecelerationMagnitude = maxSpeed / Mathf.Max(timeToStop, 0.01f);
    }

    /// <summary>
    /// Simula el agarre de las ruedas para evitar derrape
    /// </summary>
    private void ApplyLateralGrip()
    {
        if (!groundDetector.isGrounded) return;

        Vector3 lateralVelocity = Vector3.Dot(rb.linearVelocity, transform.right) * transform.right;
        rb.linearVelocity -= lateralVelocity * lateralGrip;
    }

    private void HandleAcceleration()
    {
        // Proyectamos el vector forward del coche sobre el plano horizontal para evitar volar
        Vector3 flatForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;

        float currentForwardSpeed = Vector3.Dot(rb.linearVelocity, flatForward);

        if (isAccelerating && groundDetector.isGrounded)
        {
            if (currentForwardSpeed < maxSpeed)
            {
                rb.AddForce(flatForward * accelerationForceMagnitude, ForceMode.Force);
            }
        }
        else if (!isAccelerating)
        {
            // Frenado progresivo: aplica una fuerza contraria a la velocidad actual
            if (rb.linearVelocity.sqrMagnitude > 0.0025f)
            {
                Vector3 brakeDirection = -rb.linearVelocity.normalized;
                float brakeForceMag = brakeDecelerationMagnitude * rb.mass;

                rb.AddForce(brakeDirection * brakeForceMag, ForceMode.Force);

                // Si frenar invierte el movimiento del coche (marcha atras), directamente lo detenemos
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

    public void StopCar()
    {
        isAccelerating = false;
        rb.linearVelocity = Vector3.zero;
    }
    #endregion

    #region Monobehaviour
    void Awake()
    {
        if (Instance != null)
            DestroyImmediate(this);
        Instance = this;

        rb = GetComponent<Rigidbody>();
        groundDetector.SetLayer(groundLayer);
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
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

    void Update()
    {
        turnInput = 0f;
        if (turnLeft) turnInput -= 1f;
        if (turnRight) turnInput += 1f;
    }

    void FixedUpdate()
    {
        if (RacingGameManager.Instance.gameStarted && !RacingGameManager.Instance.isPaused)
        {
            ApplyLateralGrip();
            HandleAcceleration();
            HandleSteering();
        }
    }
    #endregion
}