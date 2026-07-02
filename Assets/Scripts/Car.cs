using System.Collections.Generic;
using UnityEngine;

public class Car : BaseCollisionObject
{
    [Header("Physics")]
    [SerializeField] private float mass = 10f;
    [SerializeField] private float inputForce = 150f;
    [SerializeField] private float frictionCoefficient = 0.5f;
    [SerializeField] private float restitution = 0.3f;
    [SerializeField] private MeshRenderer meshRenderer;
    [SerializeField] private MeshFilter meshFilter;

    [Header("Handling")]
    [SerializeField] private float lateralFriction = 12f;

    [Header("Rotation")]
    [SerializeField] private float rotationSpeed = 70f;

    [Header("Jump")]
    [SerializeField] private float jumpImpulse = 5f;

    private const float GRAVITY = 9.8f;

    private Vector3 linearVelocity;

    private float movementInput;
    private float rotationInput;

    private bool isGrounded = true;

    private readonly List<Triangle> triangles = new();
    public override List<Triangle> Triangles => triangles;

    private readonly List<TriangleReference> triangleReferences = new();
    public override List<TriangleReference> TriangleReferences => triangleReferences;

    private AABBVolume collisionVolume;

    public override float Mass => mass;
    public override float Restitution => restitution;

    public Vector3 LinearVelocity
    {
        get => linearVelocity;
        set => linearVelocity = value;
    }

    public AABB Bounds => new AABB(transform.position, meshRenderer.bounds.extents * 2f);

    public override CollisionVolume CollisionVolume
    {
        get
        {
            collisionVolume ??= new AABBVolume(Bounds);
            collisionVolume.Bounds = Bounds;
            return collisionVolume;
        }
    }

    private void Awake()
    {
        SaveTriangles();
        foreach (Triangle triangle in triangles)
            triangleReferences.Add(new TriangleReference(this, triangle, GetTriangleSphere(triangle)));

        SaveState();
        PreviousState = CurrentState;
    }

    private void FixedUpdate()
    {
        SimulateMovement();
    }

    private void SimulateMovement()
    {
        float dt = Time.fixedDeltaTime;

        Vector3 forward = transform.forward;

        // Componentes de la velocidad
        Vector3 forwardVelocity = Vector3.Dot(linearVelocity, forward) * forward;
        Vector3 lateralVelocity = linearVelocity - forwardVelocity;

        // Eliminar progresivamente el deslizamiento lateral
        linearVelocity -= lateralVelocity * lateralFriction * dt;

        float forwardSpeed = Vector3.Dot(linearVelocity, forward);

        float appliedForce = movementInput * inputForce;

        float frictionForce = 0f;
        float normalForce = mass * GRAVITY;

        if (Mathf.Abs(forwardSpeed) > 0.001f)
        {
            frictionForce = -Mathf.Sign(forwardSpeed) * frictionCoefficient * normalForce;
        }
        else if (movementInput != 0f)
        {
            float maxStatic = frictionCoefficient * normalForce;

            if (Mathf.Abs(appliedForce) < maxStatic)
                appliedForce = 0f;
            else
                frictionForce = -Mathf.Sign(appliedForce) * maxStatic;
        }

        float acceleration = (appliedForce + frictionForce) / mass;

        linearVelocity += forward * acceleration * dt;

        //Gravedad
        //if (!isGrounded)
        //    linearVelocity += Vector3.down * GRAVITY * dt;

        transform.position += linearVelocity * dt;

        float steering = Mathf.Clamp01(Mathf.Abs(forwardSpeed) / 5f);

        transform.Rotate(Vector3.up, rotationInput * rotationSpeed * steering * dt);
    }

    public void Jump()
    {
        if (!isGrounded)
            return;

        linearVelocity += Vector3.up * jumpImpulse;
        isGrounded = false;
    }

    public void SetGrounded(bool grounded)
    {
        isGrounded = grounded;

        if (grounded && linearVelocity.y < 0f)
            linearVelocity = new Vector3(linearVelocity.x, 0f, linearVelocity.z);
    }

    public void SetMovementInput(float value)
    {
        movementInput = Mathf.Clamp(value, -1f, 1f);
    }

    public void SetRotationInput(float value)
    {
        rotationInput = Mathf.Clamp(value, -1f, 1f);
    }

    private void SaveTriangles()
    {
        Mesh mesh = meshFilter.mesh;

        Vector3[] vertices = mesh.vertices;
        int[] indices = mesh.triangles;

        triangles.Clear();

        for (int i = 0; i < indices.Length; i += 3)
            triangles.Add(new Triangle(vertices[indices[i]], vertices[indices[i + 1]], vertices[indices[i + 2]]));
    }

    public override Sphere GetTriangleSphere(Triangle triangle)
    {
        Vector3 worldCenter = transform.TransformPoint(triangle.localBoundingSphere.center);

        float scale = Mathf.Max(
            transform.lossyScale.x,
            Mathf.Max(
                transform.lossyScale.y,
                transform.lossyScale.z));

        return new Sphere(worldCenter, triangle.localBoundingSphere.radius * scale);
    }

    public override void UpdateTriangleWorldData()
    {
        foreach (TriangleReference reference in triangleReferences)
            reference.UpdateWorldData();
    }

    public override void UpdateTriangleReferences()
    {
        foreach (TriangleReference reference in triangleReferences)
            reference.UpdateSphere();
    }

    protected override Vector3 GetLinearVelocity()
    {
        return linearVelocity;
    }

    protected override Vector3 GetAngularVelocity()
    {
        float steering = Mathf.Clamp01(Mathf.Abs(Vector3.Dot(linearVelocity, transform.forward)) / 5f);

        return Vector3.up * rotationInput * rotationSpeed * steering;
    }

    protected override void SetLinearVelocity(Vector3 velocity)
    {
        linearVelocity = velocity;
    }

    protected override void SetAngularVelocity(Vector3 velocity)
    {
        //TODO
    }


    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(Bounds.center, Bounds.halfSize * 2f);
    }
}