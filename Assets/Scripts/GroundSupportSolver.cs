using System.Collections.Generic;
using UnityEngine;

public class GroundSupportSolver
{
    private readonly List<TriangleReference> supportQueryResults = new(128);
    private readonly TriangleOctree staticOctree;

    public GroundSupportSolver(TriangleOctree staticOctree)
    {
        this.staticOctree = staticOctree;
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
        out Vector3 hitNormal, out float hitDistance, out ScenarioPiece hitPiece)
    {
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
            TriangleReference triangle = supportQueryResults[i];

            if (!Collisions.RayVsTriangle(origin, direction, maximumDistance, triangle, out Vector3 currentHitPoint))
                continue;

            float currentDistance = Vector3.Distance(origin, currentHitPoint);

            if (currentDistance >= hitDistance)
                continue;

            Vector3 currentNormal = GetTriangleNormal(triangle, origin);

            if (Vector3.Dot(currentNormal, carUp) <= 0.05f)
                continue;

            hitNormal = currentNormal;
            hitDistance = currentDistance;
            hitPiece = triangle.owner as ScenarioPiece;

            foundSupport = true;
        }

        return foundSupport;
    }

    public void Maintain(Car car)
    {
        if (car == null)
            return;

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
                out Vector3 hitNormal, out float hitDistance, out ScenarioPiece hitPiece))
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
}
