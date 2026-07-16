using System.Collections.Generic;
using UnityEngine;

public class ScenarioPiece : BaseCollisionObject
{
    [Header("Physics")]
    [SerializeField] private float frictionCoefficient = 0.8f;
    [SerializeField] private float restitution = 0.2f;

    [Header("Mesh")]
    [SerializeField] private MeshFilter meshFilter;
    [SerializeField] private MeshRenderer meshRenderer;

    private readonly List<Triangle> triangles = new();
    private TriangleReference[] triangleReferences;
    private BVHNode bvhRoot;
    private AABBVolume collisionVolume;

    public override float Mass => 0f;
    public override float Restitution => restitution;

    public float FrictionCoefficient => frictionCoefficient;

    public override BVHNode BVHRoot => bvhRoot;
    public override List<Triangle> Triangles => triangles;

    public override Vector3 CenterOfMass => transform.position;

    public AABB Bounds => new AABB(meshRenderer.bounds.center, meshRenderer.bounds.size);

    public override Vector3 LocalCenterOfMass => meshFilter.sharedMesh.bounds.center;

    public override AABB LocalBounds
    {
        get
        {
            Bounds meshBounds = meshFilter.sharedMesh.bounds;
            return new AABB(meshBounds.center, meshBounds.size);
        }
    }

    public override Transform CollisionMeshTransform
    {
        get
        {
            return meshFilter.transform;
        }
    }
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
        CreateTriangleReferences();

        bvhRoot = BVHBuilder.Build(triangles);

        SaveState();
        PreviousState = CurrentState;

        Debug.Log($"{name}: {Triangles.Count} triángulos");
        Debug.Log($"{name}: {meshFilter.sharedMesh.vertexCount} meshFilter.sharedMesh.vertexCount");
        Debug.Log($"{name}: {meshFilter.sharedMesh.triangles.Length} meshFilter.sharedMesh.triangles.Length");
    }

    private void SaveTriangles()
    {
        Mesh mesh = meshFilter.sharedMesh;

        Vector3[] vertices = mesh.vertices;
        int[] indices = mesh.triangles;

        triangles.Clear();

        for (int i = 0; i < indices.Length; i += 3)
        {
            Vector3 v1 = vertices[indices[i]];
            Vector3 v2 = vertices[indices[i + 1]];
            Vector3 v3 = vertices[indices[i + 2]];

            triangles.Add(new Triangle(v1, v2, v3));
        }
    }

    private void CreateTriangleReferences()
    {
        triangleReferences = new TriangleReference[triangles.Count];

        for (int i = 0; i < triangles.Count; i++)
        {
            TriangleReference reference = new TriangleReference(this, triangles[i], i);

            reference.sphere = GetTriangleSphere(triangles[i]);
            reference.bounds = GetTriangleWorldBounds(triangles[i]);
            triangleReferences[i] = reference;
        }
    }

    private AABB GetTriangleWorldBounds(Triangle triangle)
    {
        Vector3 p1 = transform.TransformPoint(triangle.v1);
        Vector3 p2 = transform.TransformPoint(triangle.v2);
        Vector3 p3 = transform.TransformPoint(triangle.v3);

        Vector3 minimum = Vector3.Min(p1, Vector3.Min(p2, p3));
        Vector3 maximum = Vector3.Max(p1, Vector3.Max(p2, p3));

        Vector3 center = (minimum + maximum) * 0.5f;
        Vector3 size = maximum - minimum;

        return new AABB(center, size);
    }

    public override Sphere GetTriangleSphere(Triangle triangle)
    {
        Vector3 p1 = transform.TransformPoint(triangle.v1);
        Vector3 p2 = transform.TransformPoint(triangle.v2);
        Vector3 p3 = transform.TransformPoint(triangle.v3);

        return Collisions.GetMinimumTriangleSphere(p1, p2, p3);
    }

    public override TriangleReference GetTriangleReference(int triangleIndex, int collisionStep)
    {
        return triangleReferences[triangleIndex];
    }

    public override Vector3 ApplyInverseInertiaTensor(Vector3 worldVector)
    {
        return Vector3.zero;
    }

    protected override Vector3 GetLinearVelocity()
    {
        return Vector3.zero;
    }

    protected override Vector3 GetAngularVelocity()
    {
        return Vector3.zero;
    }

    protected override void SetLinearVelocity(Vector3 velocity)
    {
    }

    protected override void SetAngularVelocity(Vector3 velocity)
    {
    }
}