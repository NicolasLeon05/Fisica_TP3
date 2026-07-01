public class AABBVolume : CollisionVolume
{
    public AABB Bounds;

    public AABBVolume(AABB bounds)
    {
        Bounds = bounds;
    }
}