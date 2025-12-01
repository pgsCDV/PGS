using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class NewMove : MonoBehaviour
{
    public static NewMove instance;

    public float walkSpeed = 6f;
    public float jumpForce = 6f;
    public float airControl = 0.5f;
    public Transform groundCheck;
    public float groundDistance = 0.2f;
    public LayerMask groundMask;
    public float groundDrag = 5f;
    public float airDrag = 0f;

    public float gravity = -9.81f;
    public float jumpHeight = 2f;

    public bool useAccelerometer = false;
    public bool invertX = false;
    public bool invertY = false;

    private Rigidbody rb;
    private bool isGrounded;
    private float horizontal;
    private float vertical;
    private bool jumpRequested;

    void Awake()
    {
        if (instance == null)
            instance = this;
        else Destroy(this);

        rb = GetComponent<Rigidbody>();
        rb.constraints = RigidbodyConstraints.FreezeRotation;
    }

    void Update()
    {
        if (useAccelerometer)
        {
            horizontal = Input.acceleration.x;
            vertical = Input.acceleration.y;

            if (invertX) horizontal = -horizontal;
            if (invertY) vertical = -vertical;
        }
        else
        {
            horizontal = Input.GetAxisRaw("Horizontal");
            vertical = Input.GetAxisRaw("Vertical");
        }

        if (Input.GetButtonDown("Jump"))
            jumpRequested = true;
    }

    void FixedUpdate()
    {
        isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask);
        rb.linearDamping = isGrounded ? groundDrag : airDrag;

        Vector3 inputDir = new Vector3(horizontal, 0f, vertical).normalized;
        Vector3 moveDir = transform.TransformDirection(inputDir);

        Vector3 currentVel = rb.linearVelocity;
        Vector3 targetVel = moveDir * walkSpeed;
        Vector3 velChange = targetVel - new Vector3(currentVel.x, 0f, currentVel.z);
        float control = isGrounded ? 1f : airControl;
        rb.AddForce(velChange * control, ForceMode.VelocityChange);

        if (jumpRequested && isGrounded)
        {
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        }

        jumpRequested = false;
    }

    void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(groundCheck.position, groundDistance);
        }
    }
}