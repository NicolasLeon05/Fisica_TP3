using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{

    [Header("Objects")]
    [SerializeField] private Car car1;
    [SerializeField] private Car car2;
    [SerializeField] private List<ScenarioPiece> scenarioPieces;

    [Header("Octree")]
    [SerializeField] private float parentSize;
    [SerializeField] private float minSize;
    private OctreeNode parentNode;
    private List<BaseCollisionObject> objects = new();

    [Header("Constraints")]
    [SerializeField] private int minObjectsToDivide = 2;
    [SerializeField] private int minTrianglesToDivide = 64;
    [SerializeField] private int maxOctreeDepth = 10;
    [SerializeField] private int binarySearchLimit = 12;

    [Header("Static Octree")]
    [SerializeField] private float staticMinNodeSize = 4f;
    [SerializeField] private int staticMaxDepth = 6;
    [SerializeField] private int staticTrianglesPerNode = 64;

    [Header("Dynamic Octree")]
    [SerializeField] private float dynamicMinNodeSize = 4f;
    [SerializeField] private int dynamicMaxDepth = 5;
    [SerializeField] private int dynamicTrianglesPerNode = 256;

    private TriangleOctree staticOctree;
    private TriangleOctree dynamicOctree;
    private readonly List<TriangleReference> dynamicTriangles = new();

    private List<OctreeNode> octreeNodes = new List<OctreeNode>();
    private readonly List<CollisionInfo> collisions = new List<CollisionInfo>();

    //////////////////////////////////////////////////// DEBUG ///////////////////////////////////////////////////////////////////////////
    private long sphereTestCount;
    private int sphereHitCount;
    private long spherePairAttemptCount;
    private long duplicatePairCount;
    private long countTrianglesTicks;
    private long collectTrianglesTicks;
    private long sphereCandidatesTicks;
    private int collisionStep;
    private int scenarioContactCount;
    private long preciseCollisionTicks;
    private long preciseCollisionTestCount;

    private double TicksToMilliseconds(long ticks)
    {
        return ticks * 1000.0 /
            System.Diagnostics.Stopwatch.Frequency;
    }
    //////////////////////////////////////////////////////////////////////////////////////////////////////////

    private readonly HashSet<TrianglePairKey> testedTrianglePairs = new();
    private readonly Dictionary<ObjectPairKey, int> contactsPerPair = new();
    [SerializeField] private int maxContactsPerPair = 1;

    private void Awake()
    {
        parentNode = new OctreeNode(transform.position, parentSize, null);
        objects.Add(car1);
        objects.Add(car2);

        foreach (ScenarioPiece piece in scenarioPieces)
            if (piece != null)
                objects.Add(piece);

        parentNode.SetPosition(transform.position);

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

        RebuildDynamicOctree(out double sphereUpdateMilliseconds, out double octreeBuildMilliseconds);

        Debug.Log(
        $"Octree dinámico | " +
        $"Triángulos: {dynamicTriangles.Count} | " +
        $"Referencias totales: {dynamicOctree.StoredReferenceCount} | " +
        $"Referencias en root: {dynamicOctree.RootReferenceCount} | " +
        $"Nodos: {dynamicOctree.NodeCount} | " +
        $"Actualizar: {sphereUpdateMilliseconds:F2} ms | " +
        $"Construir: {octreeBuildMilliseconds:F2} ms | " +
        $"Fuera: {dynamicOctree.RejectedOutsideRoot} | " +
        $"Inválidos: {dynamicOctree.RejectedInvalidBounds}");

        //car1.SimulatePhysicsStep();
        //car2.SimulatePhysicsStep();
        //
        //car1.BeginContactDetection();
        //car2.BeginContactDetection();
        //
        //collisionStep++;
        //
        //collisions.Clear();
        //testedTrianglePairs.Clear();
        //contactsPerPair.Clear();
        //
        //sphereTestCount = 0;
        //sphereHitCount = 0;
        //spherePairAttemptCount = 0;
        //duplicatePairCount = 0;
        //countTrianglesTicks = 0;
        //collectTrianglesTicks = 0;
        //sphereCandidatesTicks = 0;
        //scenarioContactCount = 0;
        //preciseCollisionTicks = 0;
        //preciseCollisionTestCount = 0;
        //
        //parentNode.Clear();
        //octreeNodes.Clear();
        //
        //var stopwatch =
        //    System.Diagnostics.Stopwatch.StartNew();
        //UpdateOctree(parentNode);
        //stopwatch.Stop();
        //
        ////if (Time.frameCount % 60 == 0)
        //{
        //    Debug.Log(
        //    $"Total: {stopwatch.Elapsed.TotalMilliseconds:F2} ms | " +
        //    $"Conteo BVH: {TicksToMilliseconds(countTrianglesTicks):F2} ms | " +
        //    $"Recolectar: {TicksToMilliseconds(collectTrianglesTicks):F2} ms | " +
        //    $"Esferas: {TicksToMilliseconds(sphereCandidatesTicks):F2} ms | " +
        //    $"Nodos: {octreeNodes.Count} | " +
        //    $"Tests: {sphereTestCount} | " +
        //    $"Hits: {sphereHitCount} | " +
        //    $"Contactos escenario: {scenarioContactCount}" +
        //    $"Precisas: {preciseCollisionTestCount} | " +
        //    $"Tiempo preciso: {TicksToMilliseconds(preciseCollisionTicks):F2} ms | ");
        //}
        //
        //foreach (CollisionInfo collision in collisions)
        //    ResolveCollision(collision);
        //
        //collisions.Clear();
        //ClearNodeTriangles();
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

    private void AddDynamicObjectTriangles(BaseCollisionObject collisionObject)
    {
        for (int i = 0; i < collisionObject.Triangles.Count; i++)
        {
            TriangleReference reference = collisionObject.GetTriangleReference(i, collisionStep);
            dynamicTriangles.Add(reference);
        }
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
            TriangleReference car1Reference =                car1.TriangleReferences[0];
            TriangleReference car2Reference =                car2.TriangleReferences[0];

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
        RegisterGroundContact(info);

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

        Vector3 centerOfMassA = info.objectA.CenterOfMass;
        Vector3 centerOfMassB = info.objectB.CenterOfMass;

        Vector3 contactArmA = info.contactPoint - centerOfMassA;
        Vector3 contactArmB = info.contactPoint - centerOfMassB;

        Vector3 contactVelocityA = stateA.LinearVelocity + Vector3.Cross(stateA.AngularVelocity, contactArmA);
        Vector3 contactVelocityB = stateB.LinearVelocity + Vector3.Cross(stateB.AngularVelocity, contactArmB);
        Vector3 relativeVelocity = contactVelocityB - contactVelocityA;
        float velocityAlongNormal = Vector3.Dot(relativeVelocity, normal);

        // Los objetos ya se están separando.
        if (velocityAlongNormal >= 0f)
            return;

        float inverseMassA = info.objectA.Mass > 0f ? 1f / info.objectA.Mass : 0f;
        float inverseMassB = info.objectB.Mass > 0f ? 1f / info.objectB.Mass : 0f;

        Vector3 angularTermA = info.objectA.ApplyInverseInertiaTensor(Vector3.Cross(contactArmA, normal));
        Vector3 angularTermB = info.objectB.ApplyInverseInertiaTensor(Vector3.Cross(contactArmB, normal));

        float angularDenominatorA = Vector3.Dot(normal, Vector3.Cross(angularTermA, contactArmA));
        float angularDenominatorB = Vector3.Dot(normal, Vector3.Cross(angularTermB, contactArmB));
        float denominator = inverseMassA + inverseMassB + angularDenominatorA + angularDenominatorB;

        if (denominator <= Mathf.Epsilon)
            return;

        float restitution = Mathf.Min(info.objectA.Restitution, info.objectB.Restitution);
        const float RESTING_VELOCITY_THRESHOLD = 1f;
        if (Mathf.Abs(velocityAlongNormal) < RESTING_VELOCITY_THRESHOLD)
            restitution = 0f;

        float impulseMagnitude = -(1f + restitution) * velocityAlongNormal / denominator;
        Vector3 impulse = impulseMagnitude * normal;

        // Velocidad lineal.
        stateA.LinearVelocity -= impulse * inverseMassA;
        stateB.LinearVelocity += impulse * inverseMassB;

        // Velocidad angular.
        Vector3 angularImpulseA = Vector3.Cross(contactArmA, impulse);
        Vector3 angularImpulseB = Vector3.Cross(contactArmB, impulse);

        stateA.AngularVelocity -= info.objectA.ApplyInverseInertiaTensor(angularImpulseA);
        stateB.AngularVelocity += info.objectB.ApplyInverseInertiaTensor(angularImpulseB);
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

    private void RegisterGroundContact(CollisionInfo info)
    {
        if (info.objectA is Car carA && info.objectB is ScenarioPiece)
        {
            Vector3 supportNormal = -info.contactNormal;
            if (Vector3.Dot(supportNormal, carA.transform.up) > 0.25f)
                carA.SetGroundContact(supportNormal);
        }

        if (info.objectB is Car carB && info.objectA is ScenarioPiece)
        {
            Vector3 supportNormal = info.contactNormal;

            if (Vector3.Dot(supportNormal, carB.transform.up) > 0.25f)
                carB.SetGroundContact(supportNormal);
        }
    }

    private void ClearNodeTriangles()
    {
        foreach (OctreeNode node in octreeNodes)
            node.triangles.Clear();
    }

    private void ProcessSphereCandidates(OctreeNode node)
    {
        for (int i = 0; i < node.objects.Count; i++)
        {
            BaseCollisionObject ownerA = node.objects[i];

            if (!node.triangles.TryGetValue(ownerA, out List<TriangleReference> listA))
                continue;

            for (int j = i + 1; j < node.objects.Count; j++)
            {
                BaseCollisionObject ownerB = node.objects[j];

                if (ownerA.Mass <= 0f && ownerB.Mass <= 0f)
                    continue;

                if (!node.triangles.TryGetValue(ownerB, out List<TriangleReference> listB))
                    continue;

                ObjectPairKey objectPair = new ObjectPairKey(ownerA, ownerB);
                contactsPerPair.TryGetValue(objectPair, out int contactCount);

                if (contactCount >= maxContactsPerPair)
                    continue;

                foreach (TriangleReference triangleA in listA)
                {
                    foreach (TriangleReference triangleB in listB)
                    {
                        if (contactCount >= maxContactsPerPair)
                            break;

                        spherePairAttemptCount++;
                        sphereTestCount++;

                        if (!Collisions.SphereVsSphere(triangleA.sphere, triangleB.sphere))
                            continue;

                        sphereHitCount++;

                        TrianglePairKey trianglePair = new TrianglePairKey(triangleA, triangleB);

                        if (!testedTrianglePairs.Add(trianglePair))
                        {
                            duplicatePairCount++;
                            continue;
                        }

                        CollisionInfo collision = BuildCollisionInfo(triangleA, triangleB);

                        long preciseStart = System.Diagnostics.Stopwatch.GetTimestamp();
                        preciseCollisionTestCount++;

                        bool collided = CheckCollision(collision);
                        preciseCollisionTicks += System.Diagnostics.Stopwatch.GetTimestamp() - preciseStart;

                        if (!collided)
                            continue;

                        collisions.Add(collision);

                        contactCount++;
                        contactsPerPair[objectPair] = contactCount;

                        if (ownerA is ScenarioPiece || ownerB is ScenarioPiece)
                            scenarioContactCount++;
                    }

                    if (contactCount >= maxContactsPerPair)
                        break;
                }
            }
        }
    }

    private void UpdateOctree(OctreeNode node)
    {
        if (node == null)
            return;

        octreeNodes.Add(node);

        node.objects.Clear();
        node.triangles.Clear();

        List<BaseCollisionObject> objectsToCheck = node.Parent == null ? objects : node.Parent.objects;
        Collisions.CheckObjectsOctreeNodes(objectsToCheck, node);

        if (node.objects.Count < minObjectsToDivide)
            return;

        if (!Collisions.ObjectsCollideInsideNode(node))
            return;

        bool canSubdivide = node.Size / 2f >= minSize && node.Depth < maxOctreeDepth;
        bool shouldSubdivide = false;

        if (canSubdivide)
        {
            long startTicks = System.Diagnostics.Stopwatch.GetTimestamp();
            shouldSubdivide = Collisions.HasEnoughTrianglesInNode(node, minTrianglesToDivide);
            countTrianglesTicks += System.Diagnostics.Stopwatch.GetTimestamp() - startTicks;
        }

        if (shouldSubdivide)
        {
            node.GenerateChildren();

            foreach (OctreeNode child in node.Children)
                UpdateOctree(child);

            return;
        }

        long collectStart = System.Diagnostics.Stopwatch.GetTimestamp();
        Collisions.CollectTrianglesForLeaf(node, collisionStep);
        collectTrianglesTicks += System.Diagnostics.Stopwatch.GetTimestamp() - collectStart;

        if (node.triangles.Count < 2)
            return;

        long sphereStart = System.Diagnostics.Stopwatch.GetTimestamp();
        ProcessSphereCandidates(node);
        sphereCandidatesTicks += System.Diagnostics.Stopwatch.GetTimestamp() - sphereStart;
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

    //private void OnDrawGizmos()
    //{
    //    if (parentNode != null)
    //        parentNode.Draw();
    //}

    private void OnDrawGizmos()
    {
        if (staticOctree == null)
            return;

        Gizmos.color = Color.green;
        staticOctree.DrawGizmos();
    }
}