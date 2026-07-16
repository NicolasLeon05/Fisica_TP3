using System.Collections.Generic;
using UnityEngine;

public abstract class BaseCollisionObject : MonoBehaviour
{
    public PhysicsState PreviousState { get; protected set; }
    public PhysicsState CurrentState { get; protected set; }

    public abstract float Mass { get; }
    public abstract float Restitution { get; }
    public abstract float FrictionCoefficient { get; }

    public abstract AABB LocalBounds { get; }

    public abstract List<Triangle> Triangles { get; }

    public abstract TriangleReference[] TriangleReferences { get; }

    public abstract Sphere GetTriangleSphere(Triangle triangle);

    public abstract Transform CollisionMeshTransform { get; }

    public Matrix4x4 CollisionLocalToWorldMatrix
    {
        get
        {
            return CollisionMeshTransform.localToWorldMatrix;
        }
    }

    public Vector3 CollisionPointToWorld(Vector3 localPoint)
    {
        return CollisionMeshTransform.TransformPoint(localPoint);
    }

    public void SaveState()
    {
        PreviousState = CurrentState;

        CurrentState = new PhysicsState
        {
            Position = transform.position,
            Rotation = transform.rotation,
            LinearVelocity = GetLinearVelocity(),
            AngularVelocity = GetAngularVelocity()
        };
    }

    public PhysicsState GetInterpolatedState(PhysicsState from, PhysicsState to, float t)
    {
        PhysicsState state = new PhysicsState();
        state.Position = Vector3.Lerp(from.Position, to.Position, t);
        state.Rotation = Quaternion.Slerp(from.Rotation, to.Rotation, t);
        state.LinearVelocity = Vector3.Lerp(from.LinearVelocity, to.LinearVelocity, t);
        state.AngularVelocity = Vector3.Lerp(from.AngularVelocity, to.AngularVelocity, t);

        return state;
    }

    public AABB GetBoundsAtState(PhysicsState state)
    {
        Vector3 minimum = LocalBounds.Min;
        Vector3 maximum = LocalBounds.Max;

        Vector3[] corners =
        {
        new Vector3(minimum.x, minimum.y, minimum.z),
        new Vector3(maximum.x, minimum.y, minimum.z),
        new Vector3(minimum.x, maximum.y, minimum.z),
        new Vector3(maximum.x, maximum.y, minimum.z),

        new Vector3(minimum.x, minimum.y, maximum.z),
        new Vector3(maximum.x, minimum.y, maximum.z),
        new Vector3(minimum.x, maximum.y, maximum.z),
        new Vector3(maximum.x, maximum.y, maximum.z)
        };

        Matrix4x4 collisionToRootMatrix = transform.worldToLocalMatrix * CollisionMeshTransform.localToWorldMatrix;

        Vector3 rootScale = transform.lossyScale;


        Vector3 worldMinimum = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        Vector3 worldMaximum = new Vector3(float.MinValue, float.MinValue, float.MinValue);

        for (int i = 0; i < corners.Length; i++)
        {
            Vector3 rootLocalCorner = collisionToRootMatrix.MultiplyPoint3x4(corners[i]);
            Vector3 scaledCorner = Vector3.Scale(rootLocalCorner, rootScale);
            Vector3 worldCorner = state.Position + state.Rotation * scaledCorner;

            worldMinimum = Vector3.Min(worldMinimum, worldCorner);
            worldMaximum = Vector3.Max(worldMaximum, worldCorner);
        }

        return new AABB((worldMinimum + worldMaximum) * 0.5f, worldMaximum - worldMinimum);
    }

    public AABB GetSweptBounds(float margin = 0.05f)
    {
        AABB previousBounds = GetBoundsAtState(PreviousState);
        AABB currentBounds = GetBoundsAtState(CurrentState);

        Vector3 marginVector = Vector3.one * margin;

        Vector3 min = Vector3.Min(previousBounds.Min, currentBounds.Min) - marginVector;
        Vector3 max = Vector3.Max(previousBounds.Max, currentBounds.Max) + marginVector;

        return new AABB((min + max) * 0.5f, max - min);
    }

    public void ApplyTemporaryState(PhysicsState state)
    {
        transform.SetPositionAndRotation(state.Position, state.Rotation);
        SetLinearVelocity(state.LinearVelocity);
        SetAngularVelocity(state.AngularVelocity);
    }

    public virtual void RestoreState(PhysicsState state)
    {
        ApplyTemporaryState(state);
        CurrentState = state;
    }

    public void SetSimulationStates(PhysicsState previous, PhysicsState current)
    {
        PreviousState = previous;
        CurrentState = current;

        ApplyTemporaryState(current);
    }

    public void ResetSimulationState(Vector3 position, Quaternion rotation)
    {
        PhysicsState state = new PhysicsState
        {
            Position = position,
            Rotation = rotation,
            LinearVelocity = Vector3.zero,
            AngularVelocity = Vector3.zero
        };

        SetSimulationStates(state, state);
    }

    public abstract TriangleReference GetTriangleReference(int triangleIndex, int collisionStep);

    protected abstract Vector3 GetLinearVelocity();

    protected abstract Vector3 GetAngularVelocity();

    protected abstract void SetLinearVelocity(Vector3 velocity);

    protected abstract void SetAngularVelocity(Vector3 velocity);
}