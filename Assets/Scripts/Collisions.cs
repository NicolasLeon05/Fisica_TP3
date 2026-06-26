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

    public static void CheckCarOctreeNodes(List<Car> cars, OctreeNode node)
    {
        foreach (Car car in cars)
        {
            if (AABBvsAABB(car.Bounds, node.Bounds))
                if (!node.cars.Contains(car))
                    node.cars.Add(car);
        }
    }

    public static void CheckTriangleOctree(OctreeNode node)
    {
        foreach (Car car in node.cars)
        {
            foreach (Triangle triangle in car.Triangles)
            {
                Sphere sphere = car.GetTriangleSphere(triangle);

                //Debug.Log($"{node.name} contains {node.triangles.Count} triangles");
                if (SphereVsAABB(sphere, node.Bounds))
                    node.triangles.Add(new TriangleReference(car, triangle));
            }
        }
    }
}
