using System.Collections.Generic;
using UnityEngine;

public abstract class BaseCollisionObject : MonoBehaviour
{
    public PhysicsState PreviousState { get; protected set; }
    public PhysicsState CurrentState { get; protected set; }

    public abstract CollisionVolume CollisionVolume { get; }

    public abstract List<Triangle> Triangles { get; }

    public abstract Sphere GetTriangleSphere(Triangle triangle);

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

    public void InterpolateState(float t)
    {
        PhysicsState state = new PhysicsState();

        state.Position = Vector3.Lerp(PreviousState.Position, CurrentState.Position, t);
        state.Rotation = Quaternion.Slerp(PreviousState.Rotation, CurrentState.Rotation, t);
        state.LinearVelocity = Vector3.Lerp(PreviousState.LinearVelocity, CurrentState.LinearVelocity, t);
        state.AngularVelocity = Vector3.Lerp(PreviousState.AngularVelocity, CurrentState.AngularVelocity, t);

        RestoreState(state);
    }

    public virtual void RestoreState(PhysicsState state)
    {
        transform.SetPositionAndRotation(state.Position, state.Rotation);
        SetLinearVelocity(state.LinearVelocity);
        SetAngularVelocity(state.AngularVelocity);

        CurrentState = state;
    }

    protected abstract Vector3 GetLinearVelocity();

    protected abstract Vector3 GetAngularVelocity();

    protected abstract void SetLinearVelocity(Vector3 velocity);

    protected abstract void SetAngularVelocity(Vector3 velocity);
}