using System.Collections.Generic;
using UnityEngine;
using System.Threading.Tasks;

public class Car : BaseCollisionObject
{
    [Header("Physics")]
    [SerializeField] private float mass = 10f;
    [SerializeField] private float inputForce = 150f;
    [SerializeField] private float frictionCoefficient = 0.5f;
    [SerializeField] private float restitution = 0.3f;
    [SerializeField] private float lateralFrictionCoefficient = 2.5f;
    [SerializeField] private float angularDamping = 3f;
    [SerializeField] private MeshRenderer meshRenderer;
    [SerializeField] private MeshFilter meshFilter;

    [Header("Rotation")]
    [SerializeField] private float rotationSpeed = 70f;
    [SerializeField] private float angularAcceleration = 360f;

    [Header("Jump")]
    [SerializeField] private float jumpImpulse = 5f;

    [Header("Ground Support")]
    [SerializeField] private Transform[] supportPoints;

    [SerializeField] private Vector3 supportBoxHalfSize = new Vector3(0.25f, 0.1f, 0.35f);

    [SerializeField] private float supportProbeStart = 0.15f;
    [SerializeField] private float supportProbeLength = 0.6f;
    [SerializeField] private int minimumSupportPoints = 2;

    [SerializeField] private float groundAlignSpeed = 12f;
    [SerializeField] private float maximumSupportCorrection = 0.3f;
    [SerializeField] private float supportDetachVelocity = 0.75f;

    private bool isGrounded;
    private Vector3 groundNormal = Vector3.up;
    private ScenarioPiece groundPiece;

    public Transform[] SupportPoints => supportPoints;
    public Vector3 SupportBoxHalfSize => supportBoxHalfSize;
    public float SupportProbeStart => supportProbeStart;
    public float SupportProbeLength => supportProbeLength;
    public int MinimumSupportPoints => minimumSupportPoints;
    public float GroundAlignSpeed => groundAlignSpeed;
    public float MaximumSupportCorrection => maximumSupportCorrection;
    public float SupportDetachVelocity => supportDetachVelocity;

    public bool IsGrounded => isGrounded;
    public Vector3 GroundNormal => groundNormal;
    public ScenarioPiece GroundPiece => groundPiece;

    private const float GRAVITY = 9.8f;

    private Vector3 linearVelocity;
    private Vector3 angularVelocity;
    private Vector3 inverseInertiaTensor;

    private float movementInput;
    private float rotationInput;

    private List<Triangle> triangles = new List<Triangle>();
    private TriangleReference[] triangleReferences;
    public override TriangleReference[] TriangleReferences
    {
        get
        {
            return triangleReferences;
        }
    }
    public override float Mass => mass;
    public override float Restitution => restitution;
    public AABB Bounds => new AABB(meshRenderer.bounds.center, meshRenderer.bounds.extents * 2);

    private BVHNode bvhRoot;
    public override BVHNode BVHRoot => bvhRoot;
    public override Vector3 LocalCenterOfMass => meshFilter.sharedMesh.bounds.center;

    public float ForwardSpeed
    {
        get => Vector3.Dot(linearVelocity, transform.forward);
        set
        {
            Vector3 forward = transform.forward;
            float currentForwardSpeed = Vector3.Dot(linearVelocity, forward);

            linearVelocity += forward * (value - currentForwardSpeed);
        }
    }

    public override Transform CollisionMeshTransform
    {
        get
        {
            return meshFilter.transform;
        }
    }

    public float VerticalSpeed
    {
        get => linearVelocity.y;
        set => linearVelocity.y = value;
    }

    private AABBVolume collisionVolume;

    public override AABB LocalBounds
    {
        get
        {
            Bounds meshBounds = meshFilter.sharedMesh.bounds;

            return new AABB(meshBounds.center, meshBounds.size);
        }
    }

    public override CollisionVolume CollisionVolume
    {
        get
        {
            AABB sweptBounds = GetSweptBounds();

            collisionVolume ??= new AABBVolume(sweptBounds);
            collisionVolume.Bounds = sweptBounds;

            return collisionVolume;
        }
    }

    public override Vector3 CenterOfMass
    {
        get
        {
            Vector3 localCenter = meshFilter.sharedMesh.bounds.center;
            return transform.TransformPoint(localCenter);
        }
    }

    public override List<Triangle> Triangles => triangles;

    private void Awake()
    {
        SaveTriangles();
        CreateTriangleReferences();
        CalculateInverseInertiaTensor();

        bvhRoot = BVHBuilder.Build(triangles);

        SaveState();
        PreviousState = CurrentState;

        Debug.Log($"{name}: {Triangles.Count} triángulos");
        Debug.Log($"{name}: {meshFilter.sharedMesh.vertexCount} meshFilter.sharedMesh.vertexCount");
        Debug.Log($"{name}: {meshFilter.sharedMesh.triangles.Length} meshFilter.sharedMesh.triangles.Length");
    }


    public void SimulatePhysicsStep()
    {
        SimulateMovement();
        SaveState();
    }

    private void SimulateMovement()
    {
        float dt = Time.fixedDeltaTime;

        Vector3 forward = transform.forward;
        float forwardSpeed = Vector3.Dot(linearVelocity, forward);

        float appliedForce = movementInput * inputForce;
        float normalForce = mass * GRAVITY;
        float frictionForce = 0f;

        if (Mathf.Abs(forwardSpeed) > 0.001f)
        {
            frictionForce = -Mathf.Sign(forwardSpeed) * frictionCoefficient * normalForce;

            // Evitar que la friccion invierta la velocidad.
            float maximumStoppingForce = Mathf.Abs(forwardSpeed) * mass / dt;

            frictionForce = Mathf.Clamp(frictionForce, -maximumStoppingForce, maximumStoppingForce);
        }
        else if (movementInput != 0f)
        {
            float maximumStaticFriction = frictionCoefficient * normalForce;

            if (Mathf.Abs(appliedForce) < maximumStaticFriction)
            {
                linearVelocity -= forward * forwardSpeed;
                appliedForce = 0f;
            }
            else
            {
                frictionForce = -Mathf.Sign(appliedForce) * maximumStaticFriction;
            }
        }

        float forwardAcceleration = (appliedForce + frictionForce) / mass;
        linearVelocity += forward * forwardAcceleration * dt;

        ApplyLateralFriction(dt);

        linearVelocity += Vector3.down * GRAVITY * dt;
        transform.position += linearVelocity * dt;

        float updatedForwardSpeed = Vector3.Dot(linearVelocity, transform.forward);

        float steering = Mathf.Clamp01(Mathf.Abs(updatedForwardSpeed) / 5f);
        Vector3 steeringAxis = transform.up;

        float targetAngularSpeed = rotationInput * rotationSpeed * Mathf.Deg2Rad * steering;
        float currentSteeringSpeed = Vector3.Dot(angularVelocity, steeringAxis);

        float maximumAngularChange = angularAcceleration * Mathf.Deg2Rad * dt;
        float angularChange = Mathf.Clamp(targetAngularSpeed - currentSteeringSpeed, -maximumAngularChange, maximumAngularChange);

        angularVelocity += steeringAxis * angularChange;

        if (Mathf.Abs(rotationInput) < 0.001f)
            ApplyAngularDamping(dt);

        IntegrateRotation(dt);
    }

    private void ApplyLateralFriction(float dt)
    {
        if (!isGrounded)
            return;

        Vector3 right = transform.right;
        float lateralSpeed = Vector3.Dot(linearVelocity, right);

        if (Mathf.Abs(lateralSpeed) <= 0.001f)
        {
            linearVelocity -= right * lateralSpeed;
            return;
        }

        float maximumLateralAcceleration = lateralFrictionCoefficient * GRAVITY;
        float requiredAcceleration = -lateralSpeed / dt;
        float lateralAcceleration = Mathf.Clamp(requiredAcceleration, -maximumLateralAcceleration, maximumLateralAcceleration);

        linearVelocity += right * lateralAcceleration * dt;
    }

    private void ApplyAngularDamping(float dt)
    {
        if (!isGrounded)
            return;

        float dampingFactor = Mathf.Max(0f, 1f - angularDamping * dt);
        angularVelocity *= dampingFactor;

        if (angularVelocity.sqrMagnitude < 0.0001f)
            angularVelocity = Vector3.zero;
    }

    public void Jump()
    {
        if (!isGrounded)
            return;

        linearVelocity.y = jumpImpulse;
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

    private void SaveTriangles()
    {
        Mesh mesh = meshFilter.mesh;

        Vector3[] vertices = mesh.vertices;
        int[] trianglesArray = mesh.triangles;

        triangles.Clear();

        for (int i = 0; i < trianglesArray.Length; i += 3)
        {
            Vector3 v1 = vertices[trianglesArray[i]];
            Vector3 v2 = vertices[trianglesArray[i + 1]];
            Vector3 v3 = vertices[trianglesArray[i + 2]];

            triangles.Add(new Triangle(v1, v2, v3));
        }
    }

    private void IntegrateRotation(float dt)
    {
        float angularSpeed = angularVelocity.magnitude;

        if (angularSpeed <= Mathf.Epsilon)
            return;

        Vector3 axis = angularVelocity / angularSpeed;

        float angleDegrees = angularSpeed * Mathf.Rad2Deg * dt;
        Quaternion rotationDelta = Quaternion.AngleAxis(angleDegrees, axis);

        transform.rotation = rotationDelta * transform.rotation;
    }

    private void CalculateInverseInertiaTensor()
    {
        Vector3 meshSize = meshFilter.sharedMesh.bounds.size;
        Vector3 scale = new Vector3(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y), Mathf.Abs(transform.lossyScale.z));
        Vector3 size = Vector3.Scale(meshSize, scale);

        float width = size.x;
        float height = size.y;
        float depth = size.z;

        float inertiaX = mass * (height * height + depth * depth) / 12f;
        float inertiaY = mass * (width * width + depth * depth) / 12f;
        float inertiaZ = mass * (width * width + height * height) / 12f;

        inverseInertiaTensor = new Vector3(
            inertiaX > Mathf.Epsilon ? 1f / inertiaX : 0f,
            inertiaY > Mathf.Epsilon ? 1f / inertiaY : 0f,
            inertiaZ > Mathf.Epsilon ? 1f / inertiaZ : 0f);
    }

    public void UpdateTriangleReferencesParallel(Matrix4x4 localToWorldMatrix, int collisionStep)
    {
        Parallel.For(0, triangleReferences.Length, i =>
            {
                TriangleReference reference = triangleReferences[i];

                Triangle triangle = reference.triangle;

                Vector3 p1 = localToWorldMatrix.MultiplyPoint3x4(triangle.v1);
                Vector3 p2 = localToWorldMatrix.MultiplyPoint3x4(triangle.v2);
                Vector3 p3 = localToWorldMatrix.MultiplyPoint3x4(triangle.v3);
                Vector3 sphereCenter = (p1 + p2 + p3) / 3f;

                float radiusSquared = Mathf.Max((p1 - sphereCenter).sqrMagnitude, Mathf.Max((p2 - sphereCenter).sqrMagnitude, (p3 - sphereCenter).sqrMagnitude));
                reference.sphere = new Sphere(sphereCenter, Mathf.Sqrt(radiusSquared));

                Vector3 minimum = Vector3.Min(p1, Vector3.Min(p2, p3));
                Vector3 maximum = Vector3.Max(p1, Vector3.Max(p2, p3));

                reference.bounds = new AABB((minimum + maximum) * 0.5f, maximum - minimum);
                reference.lastUpdatedStep = collisionStep;
            });
    }

    public void BeginContactDetection()
    {
        isGrounded = false;
    }

    public void SetGroundContact(Vector3 normal)
    {
        isGrounded = true;
        groundNormal = normal.normalized;
    }

    public void SetGroundSupport(ScenarioPiece piece, Vector3 normal)
    {
        isGrounded = true;
        groundPiece = piece;
        groundNormal = normal.normalized;
    }

    public void ClearGroundSupport()
    {
        isGrounded = false;
        groundPiece = null;
    }

    public override Vector3 ApplyInverseInertiaTensor(Vector3 worldVector)
    {
        Vector3 localVector = transform.InverseTransformDirection(worldVector);

        Vector3 localResult = new Vector3(
            localVector.x * inverseInertiaTensor.x,
            localVector.y * inverseInertiaTensor.y,
            localVector.z * inverseInertiaTensor.z);

        return transform.TransformDirection(localResult);
    }

    public override Sphere GetTriangleSphere(Triangle triangle)
    {
        Vector3 p1 = transform.TransformPoint(triangle.v1);
        Vector3 p2 = transform.TransformPoint(triangle.v2);
        Vector3 p3 = transform.TransformPoint(triangle.v3);

        return Collisions.GetMinimumTriangleSphere(p1, p2, p3);
    }


    protected override Vector3 GetLinearVelocity()
    {
        return linearVelocity;
    }

    protected override void SetLinearVelocity(Vector3 velocity)
    {
        linearVelocity = velocity;
    }

    protected override Vector3 GetAngularVelocity()
    {
        return angularVelocity;
    }

    protected override void SetAngularVelocity(Vector3 velocity)
    {
        angularVelocity = velocity;
    }

    private void CreateTriangleReferences()
    {
        triangleReferences = new TriangleReference[triangles.Count];

        for (int i = 0; i < triangles.Count; i++)
        {
            triangleReferences[i] = new TriangleReference(this, triangles[i], i);
        }
    }


    public override TriangleReference GetTriangleReference(int triangleIndex, int collisionStep)
    {
        TriangleReference reference = triangleReferences[triangleIndex];

        if (reference.lastUpdatedStep == collisionStep)
            return reference;

        reference.sphere = GetTriangleSphere(reference.triangle);
        reference.lastUpdatedStep = collisionStep;
        return reference;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(Bounds.center, Bounds.halfSize * 2);
    }
}