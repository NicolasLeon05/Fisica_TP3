using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    private struct CollisionCandidate
    {
        public TriangleReference triangleA;
        public TriangleReference triangleB;
        public float score;

        public CollisionCandidate(TriangleReference triangleA, TriangleReference triangleB, float score)
        {
            this.triangleA = triangleA;
            this.triangleB = triangleB;
            this.score = score;
        }
    }

    private sealed class CandidateBuffer
    {
        private readonly CollisionCandidate[] candidates;

        public int Count { get; private set; }

        public CollisionCandidate this[int index] => candidates[index];

        public CandidateBuffer(int capacity)
        {
            candidates = new CollisionCandidate[capacity];
        }

        public void Clear()
        {
            Count = 0;
        }

        public void AddOrdered(CollisionCandidate candidate)
        {
            int insertIndex = 0;

            while (insertIndex < Count && candidates[insertIndex].score >= candidate.score)
                insertIndex++;

            if (insertIndex >= candidates.Length)
                return;

            int lastIndex = Mathf.Min(Count, candidates.Length - 1);

            for (int i = lastIndex; i > insertIndex; i--)
                candidates[i] = candidates[i - 1];

            candidates[insertIndex] = candidate;

            if (Count < candidates.Length)
                Count++;
        }
    }

    [Header("Objects")]
    [SerializeField] private Car car1;
    [SerializeField] private Car car2;
    [SerializeField] private List<ScenarioPiece> scenarioPieces;

    [Header("Octree")]
    [SerializeField] private float parentSize;
    [SerializeField] private float minSize;
    //private OctreeNode parentNode;
    private List<BaseCollisionObject> objects = new();

    [Header("Constraints")]
    [SerializeField] private int minObjectsToDivide = 2;
    [SerializeField] private int minTrianglesToDivide = 64;
    [SerializeField] private int maxOctreeDepth = 10;
    [SerializeField] private int binarySearchLimit = 6;

    [Header("Static Octree")]
    [SerializeField] private float staticMinNodeSize = 4f;
    [SerializeField] private int staticMaxDepth = 6;
    [SerializeField] private int staticTrianglesPerNode = 64;

    [Header("Dynamic Octree")]
    [SerializeField] private float dynamicMinNodeSize = 4f;
    [SerializeField] private int dynamicMaxDepth = 5;
    [SerializeField] private int dynamicTrianglesPerNode = 256;

    [Header("Collision candidates")]
    [SerializeField] private int maxPreciseCandidatesPerPair = 12;
    private readonly Dictionary<ObjectPairKey, CandidateBuffer> candidateBuffers = new();
    private readonly List<TriangleReference> octreeQueryResults = new(256);
    private readonly List<TriangleReference> staticObjectCandidates = new(1024);
    private readonly List<TriangleReference> supportQueryResults = new(128);
    private readonly List<TriangleReference> dynamicPairQueryResults = new(2048);
    private readonly List<TriangleReference> dynamicPairTrianglesA = new(1024);
    private readonly List<TriangleReference> dynamicPairTrianglesB = new(1024);

    private TriangleOctree staticOctree;
    private TriangleOctree dynamicOctree;
    private readonly List<TriangleReference> dynamicTriangles = new();

    private readonly List<CollisionInfo> collisions = new List<CollisionInfo>();

    private int collisionStep;

    private readonly HashSet<TrianglePairKey> testedTrianglePairs = new();
    private readonly Dictionary<ObjectPairKey, int> contactsPerPair = new();
    [SerializeField] private int maxContactsPerPair = 1;

    private void Awake()
    {
        //parentNode = new OctreeNode(transform.position, parentSize, null);
        objects.Add(car1);
        objects.Add(car2);

        foreach (ScenarioPiece piece in scenarioPieces)
            if (piece != null)
                objects.Add(piece);

        //parentNode.SetPosition(transform.position);

        staticOctree = new TriangleOctree(transform.position, parentSize, staticMinNodeSize, staticMaxDepth, staticTrianglesPerNode);
        dynamicOctree = new TriangleOctree(transform.position, parentSize, dynamicMinNodeSize, dynamicMaxDepth, dynamicTrianglesPerNode);
    }

    private void Start()
    {
        BuildStaticOctree();
        Debug.Log(
        $"ROOT | Min: {staticOctree.Root.Bounds.Min} | " +
        $"Max: {staticOctree.Root.Bounds.Max}");

        int dynamicTriangleCount = car1.Triangles.Count + car2.Triangles.Count;
        dynamicTriangles.Capacity = dynamicTriangleCount;
    }

    private void Update()
    {
        //CAR 1
        //ACCELERATION
        float move1 = 0f;

        if (Input.GetKey(KeyCode.W))
            move1 = 1f;
        else if (Input.GetKey(KeyCode.S))
            move1 = -1f;

        car1.SetMovementInput(move1);

        //ROTATION
        float rotate1 = 0f;

        if (Input.GetKey(KeyCode.A))
            rotate1 = -1f;
        else if (Input.GetKey(KeyCode.D))
            rotate1 = 1f;

        car1.SetRotationInput(rotate1);

        //JUMP
        if (Input.GetKeyDown(KeyCode.Space))
            car1.Jump();

        //CAR 2
        //ACCELERATION
        float move2 = 0f;

        if (Input.GetKey(KeyCode.UpArrow))
            move2 = 1f;
        else if (Input.GetKey(KeyCode.DownArrow))
            move2 = -1f;

        car2.SetMovementInput(move2);

        //ROTATION
        float rotate2 = 0f;

        if (Input.GetKey(KeyCode.LeftArrow))
            rotate2 = -1f;
        else if (Input.GetKey(KeyCode.RightArrow))
            rotate2 = 1f;

        car2.SetRotationInput(rotate2);


        //JUMP
        if (Input.GetKeyDown(KeyCode.RightControl))
            car2.Jump();
    }

    private void FixedUpdate()
    {
        car1.SimulatePhysicsStep();
        car2.SimulatePhysicsStep();

        collisionStep++;

        MaintainGroundSupport(car1);
        MaintainGroundSupport(car2);

        RebuildDynamicOctree(out _, out _);

        DetectCollisions();
        ResolveDetectedCollisions();
    }

    private void DetectCollisions()
    {
        collisions.Clear();
        contactsPerPair.Clear();

        foreach (CandidateBuffer buffer in candidateBuffers.Values)
            buffer.Clear();

        DetectDynamicDynamicCollisions(car1, car2);
        DetectDynamicStaticCollisions(car1);
        DetectDynamicStaticCollisions(car2);

        BuildCollisionsFromCandidates();
    }

    private void DetectDynamicDynamicCollisions(BaseCollisionObject objectA, BaseCollisionObject objectB)
    {
        AABB objectBoundsA = GetObjectBounds(objectA);
        AABB objectBoundsB = GetObjectBounds(objectB);

        if (!Collisions.AABBIntersectsAABB(objectBoundsA, objectBoundsB))
            return;
        /*
         * Consultamos solamente la región espacial
         * compartida por los dos autos.
         */
        AABB intersectionBounds = GetIntersectionBounds(objectBoundsA, objectBoundsB);

        dynamicPairQueryResults.Clear();
        dynamicOctree.Query(intersectionBounds, dynamicPairQueryResults);
        dynamicPairTrianglesA.Clear();
        dynamicPairTrianglesB.Clear();

        for (int i = 0; i < dynamicPairQueryResults.Count; i++)
        {
            TriangleReference triangle = dynamicPairQueryResults[i];

            if (triangle.owner == objectA)
                dynamicPairTrianglesA.Add(triangle);
            else if (triangle.owner == objectB)
                dynamicPairTrianglesB.Add(triangle);
        }

        if (dynamicPairTrianglesA.Count == 0 || dynamicPairTrianglesB.Count == 0)
            return;

        /*
         * Ordenamos ambos conjuntos según el comienzo
         * de su AABB en el eje X.
         */
        dynamicPairTrianglesA.Sort(CompareTriangleByMinX);
        dynamicPairTrianglesB.Sort(CompareTriangleByMinX);

        int firstPossibleB = 0;

        for (int i = 0; i < dynamicPairTrianglesA.Count; i++)
        {
            TriangleReference triangleA = dynamicPairTrianglesA[i];

            /*
             * Descartamos definitivamente todos los
             * triángulos B que quedaron a la izquierda
             * del triángulo A.
             */
            while (firstPossibleB < dynamicPairTrianglesB.Count &&
                dynamicPairTrianglesB[firstPossibleB].bounds.Max.x < triangleA.bounds.Min.x)
            {
                firstPossibleB++;
            }

            for (int j = firstPossibleB; j < dynamicPairTrianglesB.Count; j++)
            {
                TriangleReference triangleB = dynamicPairTrianglesB[j];

                /*
                 * Como B está ordenado por Min.x,
                 * los siguientes tampoco podrán tocar A.
                 */
                if (triangleB.bounds.Min.x > triangleA.bounds.Max.x)
                    break;

                if (!Collisions.AABBIntersectsAABB(triangleA.bounds, triangleB.bounds))
                    continue;

                if (!Collisions.SphereVsSphere(triangleA.sphere, triangleB.sphere))
                    continue;

                AddCollisionCandidate(triangleA, triangleB);
            }
        }
    }

    private void DetectDynamicStaticCollisions(BaseCollisionObject dynamicObject)
    {

        AABB objectBounds = GetObjectBounds(dynamicObject);

        staticObjectCandidates.Clear();
        staticOctree.Query(objectBounds, staticObjectCandidates);

        TriangleReference[] dynamicReferences = dynamicObject.TriangleReferences;

        for (int i = 0; i < dynamicReferences.Length; i++)
        {
            TriangleReference dynamicTriangle = dynamicReferences[i];

            for (int j = 0; j < staticObjectCandidates.Count; j++)
            {
                TriangleReference staticTriangle = staticObjectCandidates[j];

                if (dynamicObject is Car car && car.IsGrounded && staticTriangle.owner == car.GroundPiece)
                    continue;

                if (!Collisions.AABBIntersectsAABB(dynamicTriangle.bounds, staticTriangle.bounds))
                    continue;

                if (!Collisions.SphereVsSphere(dynamicTriangle.sphere, staticTriangle.sphere))
                    continue;

                AddCollisionCandidate(dynamicTriangle, staticTriangle);
            }
        }
    }

    private void BuildStaticOctree()
    {
        List<TriangleReference> staticTriangles = new List<TriangleReference>();

        foreach (ScenarioPiece piece in scenarioPieces)
        {
            if (piece == null)
                continue;

            for (int i = 0; i < piece.Triangles.Count; i++)
            {
                TriangleReference reference = piece.GetTriangleReference(i, 0);
                staticTriangles.Add(reference);
            }
        }

        System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
        staticOctree.Build(staticTriangles);

        stopwatch.Stop();

        Debug.Log(
            $"Octree estático construido | " +
            $"Triángulos originales: " +
            $"{staticTriangles.Count} | " +
            $"Referencias almacenadas: " +
            $"{staticOctree.StoredReferenceCount} | " +
            $"Nodos: {staticOctree.NodeCount} | " +
            $"Tiempo: " +
            $"{stopwatch.Elapsed.TotalMilliseconds:F2} ms");
    }

    private void AddCollisionCandidate(TriangleReference triangleA, TriangleReference triangleB)
    {
        if (triangleA.owner == triangleB.owner)
            return;

        if (triangleA.owner.Mass <= 0f && triangleB.owner.Mass <= 0f)
            return;

        ObjectPairKey objectPair = new ObjectPairKey(triangleA.owner, triangleB.owner);

        if (!candidateBuffers.TryGetValue(objectPair, out CandidateBuffer buffer))
        {
            buffer = new CandidateBuffer(maxPreciseCandidatesPerPair);
            candidateBuffers.Add(objectPair, buffer);
        }

        Vector3 centerDifference = triangleB.sphere.center - triangleA.sphere.center;
        float combinedRadius = triangleA.sphere.radius + triangleB.sphere.radius;
        float score = combinedRadius * combinedRadius - centerDifference.sqrMagnitude;

        buffer.AddOrdered(new CollisionCandidate(triangleA, triangleB, score));
    }

    private void BuildCollisionsFromCandidates()
    {
        foreach (KeyValuePair<ObjectPairKey, CandidateBuffer> entry in candidateBuffers)
        {
            CandidateBuffer buffer = entry.Value;

            if (buffer.Count == 0)
                continue;

            int contactCount = 0;

            for (int i = 0; i < buffer.Count; i++)
            {
                CollisionCandidate candidate = buffer[i];
                CollisionInfo collision = BuildCollisionInfo(candidate.triangleA, candidate.triangleB);

                if (!CheckCollision(collision))
                    continue;

                collisions.Add(collision);
                contactCount++;
                contactsPerPair[entry.Key] = contactCount;

                if (contactCount >= maxContactsPerPair)
                    break;
            }
        }
    }

    private void ResolveDetectedCollisions()
    {
        for (int i = 0; i < collisions.Count; i++)
        {
            CollisionInfo collision = collisions[i];

            collision.previousStateA = collision.objectA.PreviousState;
            collision.currentStateA = collision.objectA.CurrentState;
            collision.previousStateB = collision.objectB.PreviousState;
            collision.currentStateB = collision.objectB.CurrentState;

            ResolveCollision(collision);
        }

        collisions.Clear();
    }

    private void RebuildDynamicOctree(out double sphereUpdateMilliseconds, out double octreeBuildMilliseconds)
    {
        Matrix4x4 car1Matrix = car1.CollisionLocalToWorldMatrix;
        Matrix4x4 car2Matrix = car2.CollisionLocalToWorldMatrix;

        System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();

        car1.UpdateTriangleReferencesParallel(car1Matrix, collisionStep);
        car2.UpdateTriangleReferencesParallel(car2Matrix, collisionStep);

        stopwatch.Stop();
        sphereUpdateMilliseconds = stopwatch.Elapsed.TotalMilliseconds;

        if (collisionStep == 1)
        {
            TriangleReference car1Reference = car1.TriangleReferences[0];
            TriangleReference car2Reference = car2.TriangleReferences[0];

            Debug.Log(
                $"{car1.name} primer triángulo | " +
                $"Min: {car1Reference.bounds.Min} | " +
                $"Max: {car1Reference.bounds.Max}");

            Debug.Log(
                $"{car2.name} primer triángulo | " +
                $"Min: {car2Reference.bounds.Min} | " +
                $"Max: {car2Reference.bounds.Max}");
        }

        dynamicTriangles.Clear();
        dynamicTriangles.AddRange(car1.TriangleReferences);
        dynamicTriangles.AddRange(car2.TriangleReferences);

        stopwatch.Restart();
        dynamicOctree.Refill(dynamicTriangles);
        stopwatch.Stop();

        octreeBuildMilliseconds = stopwatch.Elapsed.TotalMilliseconds;
    }

    private void ResolveCollision(CollisionInfo info)
    {
        float collisionTime = FindCollisionTime(info, binarySearchLimit);

        PhysicsState impactStateA = info.objectA.GetInterpolatedState(info.previousStateA, info.currentStateA, collisionTime);
        PhysicsState impactStateB = info.objectB.GetInterpolatedState(info.previousStateB, info.currentStateB, collisionTime);

        info.objectA.ApplyTemporaryState(impactStateA);
        info.objectB.ApplyTemporaryState(impactStateB);

        info.collisionTime = collisionTime;

        if (!CheckCollision(info))
        {
            float safeTime = Mathf.Max(0f, collisionTime - 0.001f);

            PhysicsState safeStateA = info.objectA.GetInterpolatedState(info.previousStateA, info.currentStateA, safeTime);
            PhysicsState safeStateB = info.objectB.GetInterpolatedState(info.previousStateB, info.currentStateB, safeTime);

            CommitFinalState(info.objectA, safeStateA);
            CommitFinalState(info.objectB, safeStateB);

            return;
        }

        CalculateContactData(info);
        ResolveImpulse(info, ref impactStateA, ref impactStateB);
        ApplyContactSeparation(info, ref impactStateA, ref impactStateB);

        PhysicsState finalStateA = impactStateA;
        PhysicsState finalStateB = impactStateB;

        AdvanceRemainingTime(ref finalStateA, collisionTime);
        AdvanceRemainingTime(ref finalStateB, collisionTime);

        CommitFinalState(info.objectA, finalStateA);
        CommitFinalState(info.objectB, finalStateB);
    }

    private void ApplyContactSeparation(CollisionInfo info, ref PhysicsState stateA, ref PhysicsState stateB)
    {
        const float SLOP = 0.001f;
        const float CORRECTION_PERCENT = 0.8f;

        float penetration = Mathf.Max(info.penetration - SLOP, 0f);

        if (penetration <= 0f)
            return;

        float inverseMassA = info.objectA.Mass > 0f ? 1f / info.objectA.Mass : 0f;
        float inverseMassB = info.objectB.Mass > 0f ? 1f / info.objectB.Mass : 0f;

        float inverseMassSum = inverseMassA + inverseMassB;

        if (inverseMassSum <= Mathf.Epsilon)
            return;

        Vector3 correction = info.contactNormal.normalized * penetration * CORRECTION_PERCENT / inverseMassSum;

        stateA.Position -= correction * inverseMassA;
        stateB.Position += correction * inverseMassB;
    }

    private void ResolveImpulse(CollisionInfo info, ref PhysicsState stateA, ref PhysicsState stateB)
    {
        Vector3 normal = info.contactNormal.normalized;
        Vector3 relativeVelocity = stateB.LinearVelocity - stateA.LinearVelocity;
        float velocityAlongNormal = Vector3.Dot(relativeVelocity, normal);

        if (velocityAlongNormal >= 0f)
            return;

        float inverseMassA = info.objectA.Mass > 0f ? 1f / info.objectA.Mass : 0f;
        float inverseMassB = info.objectB.Mass > 0f ? 1f / info.objectB.Mass : 0f;

        float denominator = inverseMassA + inverseMassB;

        if (denominator <= Mathf.Epsilon)
            return;

        float restitution = Mathf.Min(info.objectA.Restitution, info.objectB.Restitution);
        const float RESTING_VELOCITY_THRESHOLD = 1f;

        if (Mathf.Abs(velocityAlongNormal) < RESTING_VELOCITY_THRESHOLD)
            restitution = 0f;

        float impulseMagnitude = -(1f + restitution) * velocityAlongNormal / denominator;
        Vector3 impulse = impulseMagnitude * normal;

        stateA.LinearVelocity -= impulse * inverseMassA;
        stateB.LinearVelocity += impulse * inverseMassB;
    }

    private void AdvanceRemainingTime(ref PhysicsState state, float collisionTime)
    {
        float remainingTime = (1f - collisionTime) * Time.fixedDeltaTime;

        state.Position += state.LinearVelocity * remainingTime;

        float angularSpeed = state.AngularVelocity.magnitude;

        if (angularSpeed <= Mathf.Epsilon)
            return;

        Vector3 axis = state.AngularVelocity / angularSpeed;

        float angleDegrees = angularSpeed * Mathf.Rad2Deg * remainingTime;
        Quaternion rotationDelta = Quaternion.AngleAxis(angleDegrees, axis);

        state.Rotation = rotationDelta * state.Rotation;
    }

    private float FindCollisionTime(CollisionInfo info, int iterations)
    {
        PhysicsState previousStateA = info.previousStateA;
        PhysicsState previousStateB = info.previousStateB;

        info.objectA.ApplyTemporaryState(previousStateA);
        info.objectB.ApplyTemporaryState(previousStateB);

        // Ya estaban penetrados al comienzo del paso.
        if (CheckCollision(info))
            return 0f;

        PhysicsState currentStateA = info.currentStateA;
        PhysicsState currentStateB = info.currentStateB;

        info.objectA.ApplyTemporaryState(currentStateA);
        info.objectB.ApplyTemporaryState(currentStateB);

        if (!CheckCollision(info))
            return 1f;

        float left = 0f;
        float right = 1f;

        for (int i = 0; i < iterations; i++)
        {
            float mid = (left + right) * 0.5f;

            PhysicsState stateA = info.objectA.GetInterpolatedState(previousStateA, currentStateA, mid);
            PhysicsState stateB = info.objectB.GetInterpolatedState(previousStateB, currentStateB, mid);

            info.objectA.ApplyTemporaryState(stateA);
            info.objectB.ApplyTemporaryState(stateB);

            if (CheckCollision(info))
                right = mid;
            else
                left = mid;
        }

        return right;
    }

    private bool CheckCollision(CollisionInfo info)
    {
        bool objectAIsScenario = info.objectA is ScenarioPiece;

        bool objectBIsScenario = info.objectB is ScenarioPiece;

        // Escenario contra objeto dinámico: el triángulo del escenario siempre funciona como plano.
        if (objectAIsScenario && !objectBIsScenario)
            return CheckTriangleDirection(info.triangleA, info.triangleB, info);

        if (objectBIsScenario && !objectAIsScenario)
            return CheckTriangleDirection(info.triangleB, info.triangleA, info);

        if (CheckTriangleDirection(info.triangleA, info.triangleB, info))
            return true;

        return CheckTriangleDirection(info.triangleB, info.triangleA, info);
    }

    private bool CheckTriangleDirection(TriangleReference planeTriangle, TriangleReference penetratingTriangle, CollisionInfo info)
    {
        Vector3 oppositeVertex;
        Vector3 edge1;
        Vector3 edge2;

        if (!Collisions.VertexPlaneTest(planeTriangle, penetratingTriangle,
            out oppositeVertex, out edge1, out edge2))
        {
            return false;
        }

        Vector3 dir1 = (edge1 - oppositeVertex).normalized;
        Vector3 dir2 = (edge2 - oppositeVertex).normalized;

        float dist1 = Vector3.Distance(oppositeVertex, edge1);
        float dist2 = Vector3.Distance(oppositeVertex, edge2);

        Vector3 hitPoint;

        bool hit =
            Collisions.RayVsTriangle(oppositeVertex, dir1, dist1, planeTriangle, out hitPoint) ||
            Collisions.RayVsTriangle(oppositeVertex, dir2, dist2, planeTriangle, out hitPoint);

        if (!hit)
            return false;

        info.planeTriangle = planeTriangle;
        info.penetratingTriangle = penetratingTriangle;

        info.penetratingVertex = oppositeVertex;
        info.contactPoint = hitPoint;

        return true;
    }

    private void CalculateContactData(CollisionInfo info)
    {
        TriangleReference plane = info.planeTriangle;

        Vector3 p1 = plane.owner.CollisionPointToWorld(plane.triangle.v1);
        Vector3 p2 = plane.owner.CollisionPointToWorld(plane.triangle.v2);
        Vector3 p3 = plane.owner.CollisionPointToWorld(plane.triangle.v3);

        Vector3 normal = Vector3.Cross(p2 - p1, p3 - p1).normalized;

        Vector3 centerDirection = info.objectB.transform.position - info.objectA.transform.position;

        if (Vector3.Dot(normal, centerDirection) < 0f)
            normal = -normal;

        info.contactNormal = normal;
        info.penetration = Mathf.Abs(Vector3.Dot(info.penetratingVertex - info.contactPoint, normal));
    }

    private static int CompareTriangleByMinX(TriangleReference a, TriangleReference b)
    {
        return a.bounds.Min.x.CompareTo(b.bounds.Min.x);
    }

    private static AABB GetIntersectionBounds(AABB a, AABB b)
    {
        Vector3 minimum = Vector3.Max(a.Min, b.Min);
        Vector3 maximum = Vector3.Min(a.Max, b.Max);

        return new AABB((minimum + maximum) * 0.5f, maximum - minimum);
    }

    private AABB GetObjectBounds(BaseCollisionObject collisionObject)
    {
        AABBVolume volume = collisionObject.CollisionVolume as AABBVolume;

        if (volume == null)
        {
            Debug.LogError($"{collisionObject.name} no tiene un AABBVolume.");
            return new AABB(collisionObject.transform.position, Vector3.zero);
        }

        return volume.Bounds;
    }

    private static AABB BuildSupportQueryBounds(Vector3 start, Vector3 end, Vector3 halfSize)
    {
        Vector3 minimum = Vector3.Min(start, end) - halfSize;
        Vector3 maximum = Vector3.Max(start, end) + halfSize;

        return new AABB((minimum + maximum) * 0.5f, maximum - minimum);
    }

    private static Vector3 GetTriangleNormal(TriangleReference triangle, Vector3 towardPoint)
    {
        Vector3 p1 = triangle.owner.CollisionPointToWorld(triangle.triangle.v1);
        Vector3 p2 = triangle.owner.CollisionPointToWorld(triangle.triangle.v2);
        Vector3 p3 = triangle.owner.CollisionPointToWorld(triangle.triangle.v3);

        Vector3 normal = Vector3.Cross(p2 - p1, p3 - p1).normalized;
        Vector3 towardDirection = towardPoint - p1;

        if (Vector3.Dot(normal, towardDirection) < 0f)
            normal = -normal;

        return normal;
    }

    private bool TryFindWheelSupport(Car car, Transform supportPoint, Vector3 carUp,
        out Vector3 hitPoint, out Vector3 hitNormal, out float hitDistance, out ScenarioPiece hitPiece)
    {
        hitPoint = Vector3.zero;
        hitNormal = Vector3.zero;
        hitDistance = float.MaxValue;
        hitPiece = null;

        Vector3 origin = supportPoint.position + carUp * car.SupportProbeStart;
        Vector3 direction = -carUp;
        float maximumDistance = car.SupportProbeStart + car.SupportProbeLength;
        Vector3 end = origin + direction * maximumDistance;

        AABB queryBounds = BuildSupportQueryBounds(origin, end, car.SupportBoxHalfSize);

        supportQueryResults.Clear();
        staticOctree.Query(queryBounds, supportQueryResults);

        bool foundSupport = false;

        for (int i = 0; i < supportQueryResults.Count; i++)
        {
            TriangleReference triangle =
                supportQueryResults[i];

            if (!Collisions.RayVsTriangle(origin, direction, maximumDistance, triangle, out Vector3 currentHitPoint))
                continue;

            float currentDistance = Vector3.Distance(origin, currentHitPoint);

            if (currentDistance >= hitDistance)
                continue;

            Vector3 currentNormal = GetTriangleNormal(triangle, origin);

            if (Vector3.Dot(currentNormal, carUp) <= 0.05f)
                continue;

            hitPoint = currentHitPoint;
            hitNormal = currentNormal;
            hitDistance = currentDistance;
            hitPiece = triangle.owner as ScenarioPiece;

            foundSupport = true;
        }

        return foundSupport;
    }

    private void MaintainGroundSupport(Car car)
    {
        Transform[] supportPoints = car.SupportPoints;

        if (supportPoints == null || supportPoints.Length == 0)
        {
            car.ClearGroundSupport();
            return;
        }

        Vector3 carUp = car.IsGrounded ? car.GroundNormal : car.transform.up;
        Vector3 normalSum = Vector3.zero;
        Vector3 correctionSum = Vector3.zero;

        int supportCount = 0;

        ScenarioPiece closestPiece = null;
        float closestDistance = float.MaxValue;

        for (int i = 0; i < supportPoints.Length; i++)
        {
            Transform supportPoint = supportPoints[i];

            if (supportPoint == null)
                continue;

            if (!TryFindWheelSupport(car, supportPoint, carUp,
                out Vector3 hitPoint, out Vector3 hitNormal, out float hitDistance, out ScenarioPiece hitPiece))
                continue;

            float desiredDistance = car.SupportProbeStart;

            float correction = desiredDistance - hitDistance;
            correction = Mathf.Clamp(correction, -car.SupportProbeLength, car.MaximumSupportCorrection);

            normalSum += hitNormal;
            correctionSum += hitNormal * correction;

            supportCount++;

            if (hitDistance < closestDistance)
            {
                closestDistance = hitDistance;
                closestPiece = hitPiece;
            }
        }

        if (supportCount < car.MinimumSupportPoints)
        {
            car.ClearGroundSupport();
            return;
        }

        Vector3 averageNormal = normalSum.normalized;
        PhysicsState state = car.CurrentState;
        float velocityAwayFromGround = Vector3.Dot(state.LinearVelocity, averageNormal);

        if (velocityAwayFromGround > car.SupportDetachVelocity)
        {
            car.ClearGroundSupport();
            return;
        }

        Vector3 correctionVector = correctionSum / supportCount;
        correctionVector = Vector3.ClampMagnitude(correctionVector, car.MaximumSupportCorrection);

        state.Position += correctionVector;

        float velocityIntoGround = Vector3.Dot(state.LinearVelocity, averageNormal);

        if (velocityIntoGround < 0f)
            state.LinearVelocity -= averageNormal * velocityIntoGround;

        Vector3 currentUp = state.Rotation * Vector3.up;
        //conserva la direccion horizontal general del auto
        Quaternion targetRotation = Quaternion.FromToRotation(currentUp, averageNormal) * state.Rotation;

        float alignmentFactor = 1f - Mathf.Exp(-car.GroundAlignSpeed * Time.fixedDeltaTime);

        state.Rotation = Quaternion.Slerp(state.Rotation, targetRotation, alignmentFactor);
        state.AngularVelocity = Vector3.Project(state.AngularVelocity, averageNormal);

        car.SetSimulationStates(car.PreviousState, state);
        car.SetGroundSupport(closestPiece, averageNormal);
    }

    private CollisionInfo BuildCollisionInfo(TriangleReference triangleA, TriangleReference triangleB)
    {
        CollisionInfo info = new CollisionInfo();

        info.objectA = triangleA.owner;
        info.objectB = triangleB.owner;

        info.triangleA = triangleA;
        info.triangleB = triangleB;

        info.previousStateA = triangleA.owner.PreviousState;
        info.currentStateA = triangleA.owner.CurrentState;

        info.previousStateB = triangleB.owner.PreviousState;
        info.currentStateB = triangleB.owner.CurrentState;

        return info;
    }

    private void CommitFinalState(BaseCollisionObject obj, PhysicsState state)
    {
        if (obj.Mass <= 0f)
            return;

        obj.SetSimulationStates(state, state);
    }


    private void OnDrawGizmos()
    {
        if (staticOctree == null)
            return;

        Gizmos.color = Color.green;
        staticOctree.DrawGizmos();
    }
}