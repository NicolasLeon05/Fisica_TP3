using System.Collections.Generic;
using UnityEngine;

public class PhysicsWorld : MonoBehaviour
{
    [Header("Dynamic objects")]
    [SerializeField] private Car[] cars;
    [SerializeField] private Ball ball;

    [Header("Scenario")]
    [SerializeField] private List<ScenarioPiece> scenarioPieces;

    [Header("Octree root")]
    [SerializeField] private float parentSize = 150f;

    [Header("Static octree")]
    [SerializeField] private float staticMinNodeSize = 4f;

    [SerializeField] private int staticMaxDepth = 6;
    [SerializeField] private int staticTrianglesPerNode = 64;

    [Header("Dynamic octree")]
    [SerializeField] private float dynamicMinNodeSize = 8f;
    [SerializeField] private int dynamicMaxDepth = 3;
    [SerializeField] private int dynamicTrianglesPerNode = 512;

    [Header("Collision detection")]
    [SerializeField] private int maxPreciseCandidatesPerPair = 12;
    [SerializeField] private int maxContactsPerPair = 1;
    [SerializeField] private int binarySearchLimit = 6;

    [Header("Continuous collision")]
    [SerializeField] private int temporalSearchSteps = 16;

    private readonly List<IDynamicCollisionBody> dynamicBodies = new();

    private readonly List<BaseCollisionObject> dynamicObjects = new();

    private readonly List<TriangleReference> dynamicTriangles = new();

    private readonly List<CollisionInfo> collisions = new();

    private TriangleOctree staticOctree;
    private TriangleOctree dynamicOctree;

    private GroundSupportSolver groundSupportSolver;

    private CollisionDetector collisionDetector;

    private CollisionResolver collisionResolver;

    private int collisionStep;

    private void Awake()
    {
        staticOctree = new TriangleOctree(transform.position, parentSize, staticMinNodeSize, staticMaxDepth, staticTrianglesPerNode);
        dynamicOctree = new TriangleOctree(transform.position, parentSize, dynamicMinNodeSize, dynamicMaxDepth, dynamicTrianglesPerNode);

        if (cars != null)
            for (int i = 0; i < cars.Length; i++)
                AddDynamicBody(cars[i]);


        if (ball != null)
            AddDynamicBody(ball);

        groundSupportSolver = new GroundSupportSolver(staticOctree);

        collisionDetector = new CollisionDetector(staticOctree, dynamicOctree, maxPreciseCandidatesPerPair, maxContactsPerPair, temporalSearchSteps, binarySearchLimit);
        collisionResolver = new CollisionResolver();
    }

    private void Start()
    {
        BuildStaticOctree();
    }

    private void FixedUpdate()
    {
        for (int i = 0; i < dynamicBodies.Count; i++)
            dynamicBodies[i].SimulatePhysicsStep();

        collisionStep++;

        if (cars != null)
        {
            for (int i = 0; i < cars.Length; i++)
            {
                Car car = cars[i];
                if (car != null)
                    groundSupportSolver.Maintain(car);
            }
        }

        RebuildDynamicOctree();

        collisionDetector.Detect(dynamicObjects, collisions);
        collisionResolver.ResolveAll(collisions);
    }

    private void AddDynamicBody(IDynamicCollisionBody body)
    {
        if (body == null)
            return;

        dynamicBodies.Add(body);
        dynamicObjects.Add(body.CollisionObject);
    }

    private void BuildStaticOctree()
    {
        List<TriangleReference> staticTriangles = new();

        if (scenarioPieces == null)
        {
            staticOctree.Build(staticTriangles);
            return;
        }

        foreach (ScenarioPiece piece in scenarioPieces)
        {
            if (piece == null || piece.TriangleReferences == null)
                continue;

            staticTriangles.AddRange(piece.TriangleReferences);
        }

        staticOctree.Build(staticTriangles);
    }

    private void RebuildDynamicOctree()
    {
        dynamicTriangles.Clear();

        for (int i = 0; i < dynamicBodies.Count; i++)
        {
            IDynamicCollisionBody body = dynamicBodies[i];
            body.UpdateTriangleReferencesParallel(collisionStep);
            dynamicTriangles.AddRange(body.CollisionObject.TriangleReferences);
        }

        dynamicOctree.Refill(dynamicTriangles);
    }
}