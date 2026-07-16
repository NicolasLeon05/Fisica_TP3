public interface IDynamicCollisionBody
{
    BaseCollisionObject CollisionObject { get; }

    void SimulatePhysicsStep();

    void UpdateTriangleReferencesParallel(int collisionStep);
}