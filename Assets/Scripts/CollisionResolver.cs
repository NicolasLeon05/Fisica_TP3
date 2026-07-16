using System.Collections.Generic;
using UnityEngine;

public class CollisionResolver
{
    public void ResolveAll(List<CollisionInfo> collisions)
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

    private void ResolveCollision(CollisionInfo info)
    {
        float collisionTime = Mathf.Clamp01(info.collisionTime);

        PhysicsState impactStateA = info.objectA.GetInterpolatedState(info.previousStateA, info.currentStateA, collisionTime);
        PhysicsState impactStateB = info.objectB.GetInterpolatedState(info.previousStateB, info.currentStateB, collisionTime);

        info.objectA.ApplyTemporaryState(impactStateA);
        info.objectB.ApplyTemporaryState(impactStateB);

        if (!TriangleCollisionTester.TryBuildContact(info))
        {
            info.objectA.ApplyTemporaryState(info.currentStateA);
            info.objectB.ApplyTemporaryState(info.currentStateB);

            return;
        }

        ResolveImpulse(info, ref impactStateA, ref impactStateB);
        ApplyContactSeparation(info, ref impactStateA, ref impactStateB);

        PhysicsState finalStateA = impactStateA;
        PhysicsState finalStateB = impactStateB;

        bool bothObjectsAreDynamic = info.objectA.Mass > 0f && info.objectB.Mass > 0f;

        if (!bothObjectsAreDynamic)
        {
            AdvanceRemainingTime(ref finalStateA, collisionTime);
            AdvanceRemainingTime(ref finalStateB, collisionTime);
        }

        CommitFinalState(info.objectA, finalStateA);
        CommitFinalState(info.objectB, finalStateB);
    }

    private float FindCollisionTime(CollisionInfo info, int iterations)
    {
        PhysicsState previousStateA = info.previousStateA;
        PhysicsState previousStateB = info.previousStateB;

        info.objectA.ApplyTemporaryState(previousStateA);
        info.objectB.ApplyTemporaryState(previousStateB);

        // Ya estaban penetrados al comienzo del paso.
        if (TriangleCollisionTester.CheckCollision(info))
            return 0f;

        PhysicsState currentStateA = info.currentStateA;
        PhysicsState currentStateB = info.currentStateB;

        info.objectA.ApplyTemporaryState(currentStateA);
        info.objectB.ApplyTemporaryState(currentStateB);

        if (!TriangleCollisionTester.CheckCollision(info))
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

            if (TriangleCollisionTester.CheckCollision(info))
                right = mid;
            else
                left = mid;
        }

        return right;
    }

    private void ResolveImpulse(CollisionInfo info, ref PhysicsState stateA, ref PhysicsState stateB)
    {
        Vector3 normal = info.contactNormal.normalized;

        float inverseMassA = info.objectA.Mass > 0f ? 1f / info.objectA.Mass : 0f;
        float inverseMassB = info.objectB.Mass > 0f ? 1f / info.objectB.Mass : 0f;
        float inverseMassSum = inverseMassA + inverseMassB;

        if (inverseMassSum <= Mathf.Epsilon)
            return;

        Vector3 relativeVelocity = stateB.LinearVelocity - stateA.LinearVelocity;
        float velocityAlongNormal = Vector3.Dot(relativeVelocity, normal);

        if (velocityAlongNormal >= 0f)
            return;

        float restitution = Mathf.Min(info.objectA.Restitution, info.objectB.Restitution);
        const float RESTING_VELOCITY_THRESHOLD = 1f;

        if (Mathf.Abs(velocityAlongNormal) < RESTING_VELOCITY_THRESHOLD)
            restitution = 0f;

        float normalImpulseMagnitude = -(1f + restitution) * velocityAlongNormal / inverseMassSum;
        Vector3 normalImpulse = normal * normalImpulseMagnitude;

        stateA.LinearVelocity -= normalImpulse * inverseMassA;
        stateB.LinearVelocity += normalImpulse * inverseMassB;

        /*
         * Recalculamos la velocidad relativa después
         * del impulso normal.
         */
        relativeVelocity = stateB.LinearVelocity - stateA.LinearVelocity;

        Vector3 tangent = relativeVelocity - Vector3.Dot(relativeVelocity, normal) * normal;
        float tangentMagnitudeSquared = tangent.sqrMagnitude;

        if (tangentMagnitudeSquared > 0.000001f)
        {
            tangent /= Mathf.Sqrt(tangentMagnitudeSquared);

            float tangentVelocity = Vector3.Dot(relativeVelocity, tangent);

            float frictionImpulseMagnitude = -tangentVelocity / inverseMassSum;

            float frictionCoefficient = Mathf.Sqrt(info.objectA.FrictionCoefficient * info.objectB.FrictionCoefficient);
            float maximumFrictionImpulse = frictionCoefficient * normalImpulseMagnitude;

            frictionImpulseMagnitude = Mathf.Clamp(frictionImpulseMagnitude, -maximumFrictionImpulse, maximumFrictionImpulse);
            Vector3 frictionImpulse = tangent * frictionImpulseMagnitude;

            stateA.LinearVelocity -= frictionImpulse * inverseMassA;
            stateB.LinearVelocity += frictionImpulse * inverseMassB;
        }

        StopSmallBallTangentialVelocity(info.objectA, ref stateA, normal);
        StopSmallBallTangentialVelocity(info.objectB, ref stateB, normal);
    }

    private static void StopSmallBallTangentialVelocity(BaseCollisionObject collisionObject, ref PhysicsState state, Vector3 contactNormal)
    {
        if (!(collisionObject is Ball))
            return;

        Vector3 normalVelocity = Vector3.Project(state.LinearVelocity, contactNormal);
        Vector3 tangentialVelocity = state.LinearVelocity - normalVelocity;

        const float BALL_STOP_SPEED = 0.05f;

        if (tangentialVelocity.sqrMagnitude < BALL_STOP_SPEED * BALL_STOP_SPEED)
            state.LinearVelocity = normalVelocity;
    }

    private void ApplyContactSeparation(CollisionInfo info, ref PhysicsState stateA, ref PhysicsState stateB)
    {
        const float CONTACT_SKIN = 0.005f;

        float inverseMassA = info.objectA.Mass > 0f ? 1f / info.objectA.Mass : 0f;
        float inverseMassB = info.objectB.Mass > 0f ? 1f / info.objectB.Mass : 0f;
        float inverseMassSum = inverseMassA + inverseMassB;

        if (inverseMassSum <= Mathf.Epsilon)
            return;

        float correctionDistance = Mathf.Max(info.penetration, 0f) + CONTACT_SKIN;
        Vector3 correction = info.contactNormal.normalized * correctionDistance / inverseMassSum;

        stateA.Position -= correction * inverseMassA;
        stateB.Position += correction * inverseMassB;
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
    private void CommitFinalState(BaseCollisionObject obj, PhysicsState state)
    {
        if (obj.Mass <= 0f)
            return;

        obj.SetSimulationStates(state, state);
    }
}
