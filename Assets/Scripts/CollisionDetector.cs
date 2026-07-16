using System.Collections.Generic;
using UnityEngine;

public class CollisionDetector
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
        private readonly List<CollisionCandidate> candidates = new();

        public int Count => candidates.Count;

        public CollisionCandidate this[int index] => candidates[index];

        public void Clear()
        {
            candidates.Clear();
        }

        public void Add(CollisionCandidate candidate)
        {
            candidates.Add(candidate);
        }

        public void SortByScoreDescending()
        {
            candidates.Sort((a, b) => b.score.CompareTo(a.score));
        }
    }

    private TriangleOctree staticOctree;
    private TriangleOctree dynamicOctree;

    private readonly Dictionary<ObjectPairKey, CandidateBuffer> candidateBuffers = new();

    private readonly List<TriangleReference> dynamicPairQueryResults = new(2048);
    private readonly List<TriangleReference> dynamicPairTrianglesA = new(1024);
    private readonly List<TriangleReference> dynamicPairTrianglesB = new(1024);
    private readonly List<TriangleReference> staticObjectCandidates = new(1024);

    private readonly int maxPreciseCandidatesPerPair;
    private readonly int maxContactsPerPair;
    private readonly int carTemporalSearchSteps;
    private readonly int ballTemporalSearchSteps;
    private readonly int binarySearchIterations;

    public CollisionDetector(TriangleOctree staticOctree, TriangleOctree dynamicOctree, int maxContactsPerPair, int carTemporalSearchSteps, int ballTemporalSearchSteps, int binarySearchIterations)
    {
        this.staticOctree = staticOctree;
        this.dynamicOctree = dynamicOctree;
        this.maxContactsPerPair = Mathf.Max(1, maxContactsPerPair);
        this.carTemporalSearchSteps = Mathf.Max(1, carTemporalSearchSteps);
        this.ballTemporalSearchSteps = Mathf.Max(1, ballTemporalSearchSteps);
        this.binarySearchIterations = Mathf.Max(1, binarySearchIterations);
    }

    public void Detect(IReadOnlyList<BaseCollisionObject> dynamicObjects, List<CollisionInfo> results)
    {
        results.Clear();

        foreach (CandidateBuffer buffer in candidateBuffers.Values)
        { buffer.Clear(); }

        for (int i = 0; i < dynamicObjects.Count; i++)
        {
            for (int j = i + 1; j < dynamicObjects.Count; j++)
                DetectDynamicDynamicCollisions(dynamicObjects[i], dynamicObjects[j]);

            DetectDynamicStaticCollisions(dynamicObjects[i]);
        }

        BuildCollisionsFromCandidates(results);
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
            while (firstPossibleB < dynamicPairTrianglesB.Count && dynamicPairTrianglesB[firstPossibleB].bounds.Max.x < triangleA.bounds.Min.x)
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

    private void AddCollisionCandidate(TriangleReference triangleA, TriangleReference triangleB)
    {
        if (triangleA.owner == triangleB.owner)
            return;

        if (triangleA.owner.Mass <= 0f && triangleB.owner.Mass <= 0f)
            return;

        ObjectPairKey objectPair = new ObjectPairKey(triangleA.owner, triangleB.owner);

        if (!candidateBuffers.TryGetValue(objectPair, out CandidateBuffer buffer))
        {
            buffer = new CandidateBuffer();
            candidateBuffers.Add(objectPair, buffer);
        }

        Sphere sphereA = triangleA.currentSphere;
        Sphere sphereB = triangleB.currentSphere;

        Vector3 centerDifference = sphereB.center - sphereA.center;
        float combinedRadius = sphereA.radius + sphereB.radius;

        float score = combinedRadius * combinedRadius - centerDifference.sqrMagnitude;

        buffer.Add(new CollisionCandidate(triangleA, triangleB, score));
    }

    private void BuildCollisionsFromCandidates(List<CollisionInfo> results)
    {
        foreach (KeyValuePair<ObjectPairKey, CandidateBuffer> entry in candidateBuffers)
        {
            CandidateBuffer buffer = entry.Value;

            if (buffer.Count == 0)
                continue;

            buffer.SortByScoreDescending();

            CollisionInfo bestCollision = null;
            float earliestTime = float.PositiveInfinity;

            for (int i = 0; i < buffer.Count; i++)
            {
                CollisionCandidate candidate = buffer[i];

                CollisionInfo collision = BuildCollisionInfo(candidate.triangleA, candidate.triangleB);

                bool involvesBall = collision.objectA is Ball || collision.objectB is Ball;

                int maximumTemporalSteps = involvesBall ? ballTemporalSearchSteps : carTemporalSearchSteps;
                int temporalSteps = CalculateTemporalSteps(collision, maximumTemporalSteps);

                if (!TriangleCollisionTester.TryFindFirstCollision(collision, temporalSteps, binarySearchIterations,
                    out float collisionTime, out _))
                    continue;

                /*
                 * Ya estaban en contacto al comienzo.
                 * No existe un impacto anterior a t = 0.
                 */
                if (collisionTime <= 0.0001f)
                {
                    bestCollision = collision;
                    earliestTime = 0f;
                    break;
                }

                if (collisionTime >= earliestTime)
                    continue;

                bestCollision = collision;
                earliestTime = collisionTime;
            }

            if (bestCollision == null)
                continue;

            bestCollision.collisionTime = earliestTime;
            results.Add(bestCollision);
        }
    }

    private static int CalculateTemporalSteps(CollisionInfo collision, int maximumSteps)
    {
        Vector3 movementA = collision.currentStateA.Position - collision.previousStateA.Position;
        Vector3 movementB = collision.currentStateB.Position - collision.previousStateB.Position;

        float relativeMovement = (movementA - movementB).magnitude;

        const float DISTANCE_PER_SAMPLE = 0.1f;

        int steps = Mathf.CeilToInt(relativeMovement / DISTANCE_PER_SAMPLE);

        return Mathf.Clamp(steps, 1, maximumSteps);
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

    private static AABB GetObjectBounds(BaseCollisionObject collisionObject)
    {
        return collisionObject.GetSweptBounds();
    }

}
