using System.Collections.Generic;
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

    public static bool SphereVsAABB(Sphere sphere, AABB bounds)
    {
        float x = Mathf.Clamp(sphere.center.x, bounds.Min.x, bounds.Max.x);
        float y = Mathf.Clamp(sphere.center.y, bounds.Min.y, bounds.Max.y);
        float z = Mathf.Clamp(sphere.center.z, bounds.Min.z, bounds.Max.z);

        Vector3 closestPoint = new Vector3(x, y, z);

        Vector3 difference = sphere.center - closestPoint;

        return difference.sqrMagnitude <= sphere.radius * sphere.radius;
    }

    public static bool SphereVsSphere(Sphere a, Sphere b)
    {
        float radiusSum = a.radius + b.radius;

        return (a.center - b.center).sqrMagnitude <= radiusSum * radiusSum;
    }

    public static bool ObjectsCollisionInsideNode(OctreeNode node)
    {
        for (int i = 0; i < node.objects.Count; i++)
            for (int j = i + 1; j < node.objects.Count; j++)
                if (VolumeVsVolume(node.objects[i].CollisionVolume, node.objects[j].CollisionVolume))
                    return true;

        return false;
    }

    public static void CheckObjectsOctreeNodes(List<BaseCollisionObject> objects, OctreeNode node)
    {
        foreach (BaseCollisionObject obj in objects)
        {
            if (VolumeVsAABB(obj.CollisionVolume, node.Bounds))
                node.objects.Add(obj);
        }
    }

    public static void CheckTriangleOctree(OctreeNode node)
    {
        foreach (BaseCollisionObject obj in node.objects)
        {
            if (obj.Triangles == null)
                continue;

            foreach (Triangle triangle in obj.Triangles)
            {
                Sphere sphere = obj.GetTriangleSphere(triangle);

                if (SphereVsAABB(sphere, node.Bounds))
                    node.triangles.Add(new TriangleReference(obj, triangle, sphere));
            }
        }
    }

    public static bool VolumeVsAABB(CollisionVolume volume, AABB bounds)
    {
        if (volume is AABBVolume aabb)
            return AABBvsAABB(aabb.Bounds, bounds);

        if (volume is SphereVolume sphere)
            return SphereVsAABB(sphere.Sphere, bounds);

        return false;
    }

    public static bool VolumeVsVolume(CollisionVolume a, CollisionVolume b)
    {
        if (a is AABBVolume aa && b is AABBVolume bb)
            return AABBvsAABB(aa.Bounds, bb.Bounds);

        if (a is SphereVolume sa && b is SphereVolume sb)
            return SphereVsSphere(sa.Sphere, sb.Sphere);

        if (a is SphereVolume s && b is AABBVolume ab)
            return SphereVsAABB(s.Sphere, ab.Bounds);

        if (a is AABBVolume ba && b is SphereVolume ss)
            return SphereVsAABB(ss.Sphere, ba.Bounds);

        return false;
    }
}
