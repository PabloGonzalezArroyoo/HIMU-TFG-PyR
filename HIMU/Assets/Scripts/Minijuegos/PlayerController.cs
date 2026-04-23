using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    public float moveSpeed = 5f;
    public float jumpForce = 5f;
    public GameObject groundCheck;

    private float horizontal, vertical = 0f;

    private Rigidbody rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void Update()
    {
        Move();
        Jump();
    }

    private void Move()
    {
        Vector3 direction = new Vector3(horizontal, 0f, vertical) * moveSpeed;
        direction.y = rb.linearVelocity.y;
        rb.linearVelocity = direction;
    }

    public void AddDirection(float x, float z)
    {
        horizontal = x;
        vertical = z;
    }

    public void Jump()
    {
        if (IsGrounded())
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
    }

    private bool IsGrounded()
    {
        return Physics.CheckSphere(groundCheck.transform.position, 0.1f);
    }
}