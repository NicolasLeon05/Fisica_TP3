using UnityEngine;

public static class Collisions
{
    public static bool AABBvsAABB(AABB bounds1, AABB bounds2)
    {
        if (bounds1.Max.x < bounds2.Min.x || bounds1.Min.x > bounds2.Max.x)
            return false;
        if (bounds1.Max.y < bounds2.Min.y || bounds1.Min.y > bounds2.Max.y)
            return false;
        if (bounds1.Max.z < bounds2.Min.z || bounds1.Min.z > bounds2.Max.z)
            return false;

        return true;
    }
}
