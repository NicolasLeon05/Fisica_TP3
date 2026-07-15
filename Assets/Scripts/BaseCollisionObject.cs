using System.Collections.Generic;
using UnityEngine;

public abstract class BaseCollisionObject : MonoBehaviour
{
    public PhysicsState PreviousState { get; protected set; }
    public PhysicsState CurrentState { get; protected set; }

    public abstract float Mass { get; }
    public abstract float Restitution { get; }

    public abstract BVHNode BVHRoot { get; }

    public abstract CollisionVolume CollisionVolume { get; }

    public abstract List<Triangle> Triangles { get; }

    public abstract Sphere GetTriangleSphere(Triangle triangle);

    public abstract Vector3 CenterOfMass { get; }

    public abstract Vector3 ApplyInverseInertiaTensor(Vector3 worldVector);

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

    public AABB TransformAABB(AABB localAABB)
    {
        Vector3 min = localAABB.Min;
        Vector3 max = localAABB.Max;

        Vector3[] corners =
        {
        new Vector3(min.x, min.y, min.z),
        new Vector3(max.x, min.y, min.z),
        new Vector3(min.x, max.y, min.z),
        new Vector3(max.x, max.y, min.z),

        new Vector3(min.x, min.y, max.z),
        new Vector3(max.x, min.y, max.z),
        new Vector3(min.x, max.y, max.z),
        new Vector3(max.x, max.y, max.z)
    };

        Vector3 worldMin = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        Vector3 worldMax = new Vector3(float.MinValue, float.MinValue, float.MinValue);

        foreach (Vector3 corner in corners)
        {
            Vector3 world = transform.TransformPoint(corner);

            worldMin = Vector3.Min(worldMin, world);
            worldMax = Vector3.Max(worldMax, world);
        }

        return new AABB((worldMin + worldMax) * 0.5f, worldMax - worldMin);
    }

    public AABB InverseTransformAABB(AABB worldAABB)
    {
        Vector3 center = transform.InverseTransformPoint(worldAABB.center);

        Vector3 right = transform.InverseTransformVector(Vector3.right * worldAABB.halfSize.x);
        Vector3 up = transform.InverseTransformVector(Vector3.up * worldAABB.halfSize.y);
        Vector3 forward = transform.InverseTransformVector(Vector3.forward * worldAABB.halfSize.z);

        Vector3 halfSize = new Vector3(
            Mathf.Abs(right.x) + Mathf.Abs(up.x) + Mathf.Abs(forward.x),
            Mathf.Abs(right.y) + Mathf.Abs(up.y) + Mathf.Abs(forward.y),
            Mathf.Abs(right.z) + Mathf.Abs(up.z) + Mathf.Abs(forward.z));

        return new AABB(center, halfSize * 2f);
    }

    public abstract TriangleReference GetTriangleReference(int triangleIndex, int collisionStep);

    protected abstract Vector3 GetLinearVelocity();

    protected abstract Vector3 GetAngularVelocity();

    protected abstract void SetLinearVelocity(Vector3 velocity);

    protected abstract void SetAngularVelocity(Vector3 velocity);
}