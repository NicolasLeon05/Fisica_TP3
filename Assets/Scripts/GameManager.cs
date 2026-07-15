using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{

    [Header("Objects")]
    [SerializeField] private Car car1;
    [SerializeField] private Car car2;

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

    private double TicksToMilliseconds(long ticks)
    {
        return ticks * 1000.0 /
            System.Diagnostics.Stopwatch.Frequency;
    }
    //////////////////////////////////////////////////////////////////////////////////////////////////////////

    private readonly HashSet<TrianglePairKey> testedTrianglePairs = new();

    private void Awake()
    {
        parentNode = new OctreeNode(transform.position, parentSize, null);
        objects.Add(car1);
        objects.Add(car2);
        parentNode.SetPosition(transform.position);
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

        collisions.Clear();
        testedTrianglePairs.Clear();

        sphereTestCount = 0;
        sphereHitCount = 0;
        spherePairAttemptCount = 0;
        duplicatePairCount = 0;
        countTrianglesTicks = 0;
        collectTrianglesTicks = 0;
        sphereCandidatesTicks = 0;

        parentNode.Clear();
        octreeNodes.Clear();

        var stopwatch =
            System.Diagnostics.Stopwatch.StartNew();
        UpdateOctree(parentNode);
        stopwatch.Stop();

        if (Time.frameCount % 60 == 0)
        {
            Debug.Log(
            $"Total: {stopwatch.Elapsed.TotalMilliseconds:F2} ms | " +
            $"Conteo BVH: {TicksToMilliseconds(countTrianglesTicks):F2} ms | " +
            $"Recolectar: {TicksToMilliseconds(collectTrianglesTicks):F2} ms | " +
            $"Esferas: {TicksToMilliseconds(sphereCandidatesTicks):F2} ms | " +
            $"Nodos: {octreeNodes.Count} | " +
            $"Tests: {sphereTestCount} | " +
            $"Hits: {sphereHitCount} | " +
            $"Hits únicos: {testedTrianglePairs.Count} | " +
            $"Hits duplicados: {duplicatePairCount}");
        }

        foreach (CollisionInfo collision in collisions)
            ResolveCollision(collision);

        collisions.Clear();
        ClearNodeTriangles();
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

            info.objectA.SetSimulationStates(safeStateA, safeStateA);
            info.objectB.SetSimulationStates(safeStateB, safeStateB);

            return;
        }

        CalculateContactData(info);

        ResolveImpulse(info, ref impactStateA, ref impactStateB);

        ApplyContactSeparation(info, ref impactStateA, ref impactStateB);

        PhysicsState finalStateA = impactStateA;
        PhysicsState finalStateB = impactStateB;

        AdvanceRemainingTime(ref finalStateA, collisionTime);
        AdvanceRemainingTime(ref finalStateB, collisionTime);

        info.objectA.SetSimulationStates(finalStateA, finalStateA);
        info.objectB.SetSimulationStates(finalStateB, finalStateB);
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

        // Proteccion: este par ya no colisiona en el estado final.
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
        if (CheckTriangleDirection(info.triangleA, info.triangleB, info))
            return true;

        if (CheckTriangleDirection(info.triangleB, info.triangleA, info))
            return true;

        return false;
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

        Vector3 p1 = plane.owner.transform.TransformPoint(plane.triangle.v1);
        Vector3 p2 = plane.owner.transform.TransformPoint(plane.triangle.v2);
        Vector3 p3 = plane.owner.transform.TransformPoint(plane.triangle.v3);

        Vector3 normal = Vector3.Cross(p2 - p1, p3 - p1).normalized;

        Vector3 centerDirection = info.objectB.transform.position - info.objectA.transform.position;

        if (Vector3.Dot(normal, centerDirection) < 0f)
            normal = -normal;

        info.contactNormal = normal;
        info.penetration = Mathf.Abs(Vector3.Dot(info.penetratingVertex - info.contactPoint, normal));
    }

    private void ClearNodeTriangles()
    {
        foreach (OctreeNode node in octreeNodes)
            node.triangles.Clear();
    }


    private bool ProcessSphereCandidates(OctreeNode node)
    {
        for (int i = 0; i < node.objects.Count; i++)
        {
            BaseCollisionObject ownerA = node.objects[i];

            if (!node.triangles.TryGetValue(ownerA, out List<TriangleReference> listA))
                continue;

            for (int j = i + 1; j < node.objects.Count; j++)
            {
                BaseCollisionObject ownerB = node.objects[j];

                if (!node.triangles.TryGetValue(ownerB, out List<TriangleReference> listB))
                    continue;

                foreach (TriangleReference triangleA in listA)
                {
                    foreach (TriangleReference triangleB in listB)
                    {
                        spherePairAttemptCount++;
                        sphereTestCount++;

                        if (!Collisions.SphereVsSphere(triangleA.sphere, triangleB.sphere))
                            continue;

                        sphereHitCount++;

                        TrianglePairKey key = new TrianglePairKey(triangleA, triangleB);

                        if (!testedTrianglePairs.Add(key))
                        {
                            duplicatePairCount++;
                            continue;
                        }

                        CollisionInfo collision = BuildCollisionInfo(triangleA, triangleB);

                        // Vertex-Plane y Ray-Triangle.
                        if (!CheckCollision(collision))
                            continue;

                        collisions.Add(collision);
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private bool UpdateOctree(OctreeNode node)
    {
        if (node == null)
            return false;

        octreeNodes.Add(node);

        node.objects.Clear();
        node.triangles.Clear();

        // 1) Volúmenes de los objetos contra el nodo del octree.
        List<BaseCollisionObject> objectsToCheck = node.Parent == null ? objects : node.Parent.objects;

        Collisions.CheckObjectsOctreeNodes(objectsToCheck, node);

        if (node.objects.Count < minObjectsToDivide)
            return false;

        // 2) Los volúmenes generales de los objetos se superponen.
        if (!Collisions.ObjectsCollideInsideNode(node))
            return false;

        // 3) Contar triángulos mediante la BVH.
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
            {
                // Una rama encontró una colisión precisa.
                // No hace falta revisar las demás.
                if (UpdateOctree(child))
                    return true;
            }

            return false;
        }

        // 4) Recolectar solamente para hojas finales.
        long collectStart = System.Diagnostics.Stopwatch.GetTimestamp();
        Collisions.CollectTrianglesForLeaf(node, collisionStep);
        collectTrianglesTicks += System.Diagnostics.Stopwatch.GetTimestamp() - collectStart;

        if (node.triangles.Count < 2)
            return false;

        // 5) Esferas y pruebas precisas.
        long sphereStart = System.Diagnostics.Stopwatch.GetTimestamp();
        bool collisionFound = ProcessSphereCandidates(node);
        sphereCandidatesTicks += System.Diagnostics.Stopwatch.GetTimestamp() - sphereStart;
        return collisionFound;

        //if (candidates.Count == 0)
        //    return;

        //// 6) Vertex-Plane y Ray-Triangle.
        //foreach (CollisionInfo collision in candidates)
        //{
        //    if (!CheckCollision(collision))
        //        continue;
        //
        //    collisions.Add(collision);
        //}

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

    private void OnDrawGizmos()
    {
        //if (parentNode != null)
        //    parentNode.Draw();
    }
}