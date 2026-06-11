using UnityEngine;

public class Car : MonoBehaviour
{
    [Header("Physics")]
    [SerializeField] private float mass = 10f;
    [SerializeField] private float inputForce = 150f;
    [SerializeField] private float frictionCoefficient = 0.5f;
    [SerializeField] private float restitution = 0.3f;

    [Header("Rotation")]
    [SerializeField] private float rotationSpeed = 120f;

    [Header("Jump")]
    [SerializeField] private float jumpImpulse = 5f;

    private const float GRAVITY = 9.8f;

    private float forwardSpeed;
    private float verticalSpeed;

    private float movementInput;
    private float rotationInput;

    private bool isGrounded = true;

    public float Mass => mass;
    public float Restitution => restitution;

    public float ForwardSpeed
    {
        get => forwardSpeed;
        set => forwardSpeed = value;
    }

    public float VerticalSpeed
    {
        get => verticalSpeed;
        set => verticalSpeed = value;
    }

    private void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;

        float appliedForce = movementInput * inputForce;
        float frictionForce = 0f;
        float normalForce = mass * GRAVITY;

        if (Mathf.Abs(forwardSpeed) > 0.001f)
        {
            frictionForce = -Mathf.Sign(forwardSpeed) * frictionCoefficient * normalForce;
        }
        else if (movementInput != 0)
        {
            float maxStaticFriction = frictionCoefficient * normalForce;

            if (Mathf.Abs(appliedForce) < maxStaticFriction)
            {
                forwardSpeed = 0f;
                return;
            }

            frictionForce = -Mathf.Sign(appliedForce) * maxStaticFriction;
        }

        float totalForce = appliedForce + frictionForce;
        float acceleration = totalForce / mass;


        forwardSpeed += acceleration * dt;

        if (movementInput == 0 && Mathf.Abs(forwardSpeed) < 0.01f)
            forwardSpeed = 0f;

        /*
        if (!isGrounded)
        {
            verticalSpeed -= GRAVITY * dt;
        }
        */

        Vector3 horizontalMovement = transform.forward * forwardSpeed * dt;
        Vector3 verticalMovement = Vector3.up * verticalSpeed * dt;
        transform.position += horizontalMovement + verticalMovement;


        float steeringFactor = Mathf.Clamp01(Mathf.Abs(forwardSpeed) / 5f);
        transform.Rotate(Vector3.up, rotationInput * rotationSpeed * steeringFactor * dt);
    }

    public void Jump()
    {
        if (!isGrounded)
            return;

        verticalSpeed = jumpImpulse;
        isGrounded = false;
    }

    public void SetGrounded(bool grounded)
    {
        isGrounded = grounded;
    }


    public void SetMovementInput(float value)
    {
        movementInput = Mathf.Clamp(value, -1f, 1f);
    }

    public void SetRotationInput(float value)
    {
        rotationInput = Mathf.Clamp(value, -1f, 1f);
    }
}