using UnityEngine;

public class AABB
{
    public Vector3 center;
    public Vector3 halfSize;

    public AABB(Vector3 center, Vector3 size)
    {
        this.center = center;
        this.halfSize = size / 2f;
    }

    public Vector3 Min => center - halfSize;
    public Vector3 Max => center + halfSize;
}