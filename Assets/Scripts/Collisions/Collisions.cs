using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public static class Collisions
{
    const float EPSILON = 0.0001f;
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

    public static bool ObjectsCollideInsideNode(OctreeNode node)
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

    public static bool CheckMinTrianglesContained(OctreeNode node, int minTriangles)
    {
        int trianglesContained = 0;

        foreach (BaseCollisionObject obj in node.objects)
        {
            if (obj.Triangles == null)
                continue;

            foreach (Triangle triangle in obj.Triangles)
            {
                if (trianglesContained >= minTriangles)
                    return true;

                Sphere sphere = obj.GetTriangleSphere(triangle);

                if (SphereVsAABB(sphere, node.Bounds))
                    trianglesContained++;
            }
        }

        return false;
    }

    public static void SaveTriangleOctree(OctreeNode node)
    {
        ParallelOptions options = new ParallelOptions
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount
        };

        if (node.Parent == null)
        {
            Parallel.ForEach(node.objects, options, obj =>
            {
                if (!(obj.Triangles == null))
                {

                    List<TriangleReference> list = new();

                    Parallel.ForEach(obj.TriangleReferences, options, reference =>
                    {
                        if (SphereVsAABB(reference.sphere, node.Bounds))
                            list.Add(reference);
                    });

                    if (list.Count > 0)
                        node.triangles.Add(obj, list);
                }
            });

            return;
        }

        Parallel.ForEach(node.Parent.triangles, options, pair =>
        {
            List<TriangleReference> list = new();

                Parallel.ForEach(pair.Value, options, triangle =>
                {
                    if (SphereVsAABB(triangle.sphere, node.Bounds))
                        list.Add(triangle);
                });

            if (list.Count > 0)
                node.triangles.Add(pair.Key, list);

        });

        //foreach (var pair in node.Parent.triangles)
        //{
        //    List<TriangleReference> list = new();
        //
        //    foreach (TriangleReference triangle in pair.Value)
        //    {
        //        if (!SphereVsAABB(triangle.sphere, node.Bounds))
        //            continue;
        //
        //        list.Add(triangle);
        //    }
        //
        //    if (list.Count > 0)
        //        node.triangles.Add(pair.Key, list);
        //}
    }

    public static bool VertexPlaneTest(TriangleReference planeTriangle, TriangleReference testTriangle,
    out Vector3 oppositeVertex, out Vector3 edgeVertex1, out Vector3 edgeVertex2)
    {
        oppositeVertex = Vector3.zero;
        edgeVertex1 = Vector3.zero;
        edgeVertex2 = Vector3.zero;

        Vector3 p1 = planeTriangle.worldV1;
        Vector3 normal = planeTriangle.normal;

        Vector3 v0 = testTriangle.worldV1;
        Vector3 v1 = testTriangle.worldV2;
        Vector3 v2 = testTriangle.worldV3;

        float d0 = Vector3.Dot(normal, v0 - p1);
        float d1 = Vector3.Dot(normal, v1 - p1);
        float d2 = Vector3.Dot(normal, v2 - p1);

        bool hasPositive =
            d0 > EPSILON ||
            d1 > EPSILON ||
            d2 > EPSILON;

        bool hasNegative =
            d0 < -EPSILON ||
            d1 < -EPSILON ||
            d2 < -EPSILON;

        if (!(hasPositive && hasNegative))
            return false;

        int positives = (d0 > EPSILON ? 1 : 0)
                      + (d1 > EPSILON ? 1 : 0)
                      + (d2 > EPSILON ? 1 : 0);

        if (positives == 1)
        {
            if (d0 > EPSILON)
            {
                oppositeVertex = v0;
                edgeVertex1 = v1;
                edgeVertex2 = v2;
            }
            else if (d1 > EPSILON)
            {
                oppositeVertex = v1;
                edgeVertex1 = v2;
                edgeVertex2 = v0;
            }
            else
            {
                oppositeVertex = v2;
                edgeVertex1 = v0;
                edgeVertex2 = v1;
            }

            return true;
        }

        int negatives = (d0 < -EPSILON ? 1 : 0)
                      + (d1 < -EPSILON ? 1 : 0)
                      + (d2 < -EPSILON ? 1 : 0);

        if (negatives == 1)
        {
            if (d0 < -EPSILON)
            {
                oppositeVertex = v0;
                edgeVertex1 = v1;
                edgeVertex2 = v2;
            }
            else if (d1 < -EPSILON)
            {
                oppositeVertex = v1;
                edgeVertex1 = v2;
                edgeVertex2 = v0;
            }
            else
            {
                oppositeVertex = v2;
                edgeVertex1 = v0;
                edgeVertex2 = v1;
            }

            return true;
        }

        return false;
    }

    public static bool RayVsTriangle(Vector3 rayOrigin, Vector3 rayDirection, float maxDistance, TriangleReference triangle,
        out Vector3 hitPoint)
    {
        hitPoint = Vector3.zero;
        const float EPSILON = 0.00001f;

        Vector3 v0 = triangle.worldV1;
        Vector3 v1 = triangle.worldV2;
        Vector3 v2 = triangle.worldV3;

        Vector3 edge1 = v1 - v0;
        Vector3 edge2 = v2 - v0;

        Vector3 h = Vector3.Cross(rayDirection, edge2);
        float a = Vector3.Dot(edge1, h);

        if (Mathf.Abs(a) < EPSILON)
            return false;

        float f = 1f / a;

        Vector3 s = rayOrigin - v0;

        float u = f * Vector3.Dot(s, h);

        if (u < 0f || u > 1f)
            return false;

        Vector3 q = Vector3.Cross(s, edge1);

        float v = f * Vector3.Dot(rayDirection, q);

        if (v < 0f || u + v > 1f)
            return false;

        float t = f * Vector3.Dot(edge2, q);

        if (t < 0f || t > maxDistance)
            return false;

        hitPoint = rayOrigin + rayDirection * t;
        return true;
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
