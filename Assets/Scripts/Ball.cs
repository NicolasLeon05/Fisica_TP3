using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class Ball : BaseCollisionObject, IDynamicCollisionBody
{
    private const float GRAVITY = 9.8f;

    [Header("Physics")]
    [SerializeField] private float mass = 5f;
    [SerializeField] private float restitution = 0.8f;
    [SerializeField] private float frictionCoefficient = 0.4f;

    [Header("Mesh")]
    [SerializeField] private MeshFilter meshFilter;

    private readonly List<Triangle> triangles = new();

    private TriangleReference[] triangleReferences;

    private Vector3 linearVelocity;


    public BaseCollisionObject CollisionObject => this;

    public override float FrictionCoefficient => frictionCoefficient;

    public override float Mass => mass;

    public override float Restitution => restitution;

    public override List<Triangle> Triangles => triangles;

    public override TriangleReference[] TriangleReferences => triangleReferences;

    public override Transform CollisionMeshTransform => meshFilter.transform;

    public override AABB LocalBounds
    {
        get
        {
            Bounds meshBounds = meshFilter.sharedMesh.bounds;
            return new AABB(meshBounds.center, meshBounds.size);
        }
    }

    private void Awake()
    {
        SaveTriangles();
        CreateTriangleReferences();

        SaveState();
        PreviousState = CurrentState;

        UpdateTriangleReferencesParallel(0);
    }

    public void SimulatePhysicsStep()
    {
        float dt = Time.fixedDeltaTime;
        linearVelocity += Vector3.down * GRAVITY * dt;
        transform.position += linearVelocity * dt;

        SaveState();
    }

    private void SaveTriangles()
    {
        Mesh mesh = meshFilter.sharedMesh;

        Vector3[] vertices = mesh.vertices;

        int[] indices = mesh.triangles;

        triangles.Clear();

        for (int i = 0; i < indices.Length; i += 3)
            triangles.Add(new Triangle(vertices[indices[i]], vertices[indices[i + 1]], vertices[indices[i + 2]]));
    }

    private void CreateTriangleReferences()
    {
        triangleReferences = new TriangleReference[triangles.Count];

        for (int i = 0; i < triangles.Count; i++)
            triangleReferences[i] = new TriangleReference(this, triangles[i], i);
    }

    public void UpdateTriangleReferencesParallel(int collisionStep)
    {
        Matrix4x4 matrix = CollisionLocalToWorldMatrix;

        Parallel.For(0, triangleReferences.Length, i =>
        {
            TriangleReference reference = triangleReferences[i];
            Triangle triangle = reference.triangle;

            Vector3 p1 = matrix.MultiplyPoint3x4(triangle.v1);
            Vector3 p2 = matrix.MultiplyPoint3x4(triangle.v2);
            Vector3 p3 = matrix.MultiplyPoint3x4(triangle.v3);

            Vector3 sphereCenter = (p1 + p2 + p3) / 3f;
            float radiusSquared = Mathf.Max((p1 - sphereCenter).sqrMagnitude, Mathf.Max((p2 - sphereCenter).sqrMagnitude, (p3 - sphereCenter).sqrMagnitude));

            Sphere newCurrentSphere = new Sphere(sphereCenter, Mathf.Sqrt(radiusSquared));

            Vector3 minimum = Vector3.Min(p1, Vector3.Min(p2, p3));
            Vector3 maximum = Vector3.Max(p1, Vector3.Max(p2, p3));
            AABB newCurrentBounds = new AABB((minimum + maximum) * 0.5f, maximum - minimum);

            bool hasPreviousState = reference.lastUpdatedStep >= 0 && reference.currentBounds != null;

            if (hasPreviousState)
            {
                reference.bounds = Collisions.MergeAABB(reference.currentBounds, newCurrentBounds);
                reference.sphere = Collisions.MergeSpheres(reference.currentSphere, newCurrentSphere);
            }
            else
            {
                reference.bounds = newCurrentBounds;
                reference.sphere = newCurrentSphere;
            }

            reference.currentBounds = newCurrentBounds;
            reference.currentSphere = newCurrentSphere;
            reference.lastUpdatedStep = collisionStep;
        });
    }

    public override TriangleReference GetTriangleReference(int triangleIndex, int collisionStep)
    {
        return triangleReferences[triangleIndex];
    }

    public override Sphere GetTriangleSphere(Triangle triangle)
    {
        Vector3 p1 = CollisionPointToWorld(triangle.v1);
        Vector3 p2 = CollisionPointToWorld(triangle.v2);
        Vector3 p3 = CollisionPointToWorld(triangle.v3);

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
        return Vector3.zero;
    }

    protected override void SetAngularVelocity(Vector3 velocity)
    {
    }
}