using UnityEngine;

public static class TriangleCollisionTester
{
    public static bool CheckCollision(CollisionInfo info)
    {
        bool objectAIsScenario = info.objectA is ScenarioPiece;
        bool objectBIsScenario = info.objectB is ScenarioPiece;

        // El escenario siempre es el plano.
        //if (objectAIsScenario && !objectBIsScenario)
        //    return CheckTriangleDirection(info.triangleA, info.triangleB, info);
        //
        //if (objectBIsScenario && !objectAIsScenario)
        //    return CheckTriangleDirection(info.triangleB, info.triangleA, info);
        //
        ///*
        // * Pelota contra objeto dinamico:
        // * el auto funciona como plano y el triangulo
        // * de la pelota como triangulo penetrante.
        // */
        //if (info.objectA is Ball && !(info.objectB is Ball))
        //    return CheckTriangleDirection(info.triangleB, info.triangleA, info);
        //
        //if (info.objectB is Ball && !(info.objectA is Ball))
        //    return CheckTriangleDirection(info.triangleA, info.triangleB, info);
        //
        //if (CheckTriangleDirection(info.triangleA, info.triangleB, info))
        //    return true;

        return CheckTriangleDirection(info.triangleB, info.triangleA, info);
    }

    private static bool CheckTriangleDirection(TriangleReference planeTriangle, TriangleReference penetratingTriangle, CollisionInfo info)
    {
        if (!Collisions.VertexPlaneTest(planeTriangle, penetratingTriangle, out Vector3 oppositeVertex,
            out Vector3 edge1, out Vector3 edge2))
        {
            return false;
        }

        Vector3 difference1 = edge1 - oppositeVertex;
        Vector3 difference2 = edge2 - oppositeVertex;

        float distance1 = difference1.magnitude;
        float distance2 = difference2.magnitude;

        if (distance1 <= Mathf.Epsilon && distance2 <= Mathf.Epsilon)
            return false;

        bool hit = false;
        Vector3 hitPoint = Vector3.zero;

        if (distance1 > Mathf.Epsilon)
        {
            hit = Collisions.RayVsTriangle(oppositeVertex, difference1 / distance1, distance1, planeTriangle,
                out hitPoint);
        }

        if (!hit && distance2 > Mathf.Epsilon)
        {
            hit = Collisions.RayVsTriangle(oppositeVertex, difference2 / distance2, distance2, planeTriangle,
                out hitPoint);
        }

        if (!hit)
            return false;

        info.planeTriangle = planeTriangle;
        info.penetratingTriangle = penetratingTriangle;
        info.penetratingVertex = oppositeVertex;
        info.contactPoint = hitPoint;

        return true;
    }

    public static bool TryFindFirstCollision(CollisionInfo info, int temporalSearchSteps, int binarySearchIterations,
    out float collisionTime, out float penetration)
    {
        collisionTime = 0f;
        penetration = 0f;

        PhysicsState previousStateA = info.previousStateA;
        PhysicsState currentStateA = info.currentStateA;
        PhysicsState previousStateB = info.previousStateB;
        PhysicsState currentStateB = info.currentStateB;

        info.objectA.ApplyTemporaryState(previousStateA);
        info.objectB.ApplyTemporaryState(previousStateB);

        if (TryBuildContact(info))
        {
            collisionTime = 0f;
            penetration = info.penetration;

            RestoreCurrentStates(info, currentStateA, currentStateB);

            return true;
        }

        temporalSearchSteps = Mathf.Max(1, temporalSearchSteps);
        float leftTime = 0f;

        for (int step = 1; step <= temporalSearchSteps; step++)
        {
            float rightTime = step / (float)temporalSearchSteps;

            ApplyInterpolatedStates(info, previousStateA, currentStateA, previousStateB, currentStateB, rightTime);

            if (!CheckCollision(info))
            {
                leftTime = rightTime;
                continue;
            }

            float binaryLeft = leftTime;
            float binaryRight = rightTime;

            for (int iteration = 0; iteration < binarySearchIterations; iteration++)
            {
                float middle = (binaryLeft + binaryRight) * 0.5f;

                ApplyInterpolatedStates(info, previousStateA, currentStateA, previousStateB, currentStateB, middle);

                if (CheckCollision(info))
                    binaryRight = middle;
                else
                    binaryLeft = middle;
            }

            collisionTime = binaryRight;

            ApplyInterpolatedStates(info, previousStateA, currentStateA, previousStateB, currentStateB, collisionTime);

            if (TryBuildContact(info))
                penetration = info.penetration;

            RestoreCurrentStates(info, currentStateA, currentStateB);

            return true;
        }

        RestoreCurrentStates(info, currentStateA, currentStateB);

        return false;
    }

    private static void ApplyInterpolatedStates(CollisionInfo info, PhysicsState previousStateA, PhysicsState currentStateA, PhysicsState previousStateB, PhysicsState currentStateB, float time)
    {
        PhysicsState stateA = info.objectA.GetInterpolatedState(previousStateA, currentStateA, time);
        PhysicsState stateB = info.objectB.GetInterpolatedState(previousStateB, currentStateB, time);

        info.objectA.ApplyTemporaryState(stateA);
        info.objectB.ApplyTemporaryState(stateB);
    }

    private static void RestoreCurrentStates(CollisionInfo info, PhysicsState currentStateA, PhysicsState currentStateB)
    {
        info.objectA.ApplyTemporaryState(currentStateA);
        info.objectB.ApplyTemporaryState(currentStateB);
    }

    public static bool TryBuildContact(CollisionInfo info)
    {
        if (!CheckCollision(info))
            return false;

        CalculateContactData(info);

        return info.contactNormal.sqrMagnitude > Mathf.Epsilon;
    }

    private static void CalculateContactData(CollisionInfo info)
    {
        TriangleReference plane = info.planeTriangle;
        TriangleReference penetrating = info.penetratingTriangle;

        Vector3 p1 = plane.owner.CollisionPointToWorld(plane.triangle.v1);
        Vector3 p2 = plane.owner.CollisionPointToWorld(plane.triangle.v2);
        Vector3 p3 = plane.owner.CollisionPointToWorld(plane.triangle.v3);

        Vector3 normal = Vector3.Cross(p2 - p1, p3 - p1).normalized;

        Vector3 penetratingP1 = penetrating.owner.CollisionPointToWorld(penetrating.triangle.v1);
        Vector3 penetratingP2 = penetrating.owner.CollisionPointToWorld(penetrating.triangle.v2);
        Vector3 penetratingP3 = penetrating.owner.CollisionPointToWorld(penetrating.triangle.v3);
        Vector3 penetratingCenter = (penetratingP1 + penetratingP2 + penetratingP3) / 3f;

        /*
         * La normal queda orientada desde el triángulo
         * plano hacia el triángulo penetrante.
         */
        if (Vector3.Dot(normal, penetratingCenter - p1) < 0f)
            normal = -normal;

        /*
         * El resolver espera que contactNormal apunte
         * desde objectA hacia objectB.
         */
        if (plane.owner == info.objectA)
            info.contactNormal = normal;
        else
            info.contactNormal = -normal;

        info.penetration = Mathf.Abs(Vector3.Dot(info.penetratingVertex - info.contactPoint, normal));
    }
}