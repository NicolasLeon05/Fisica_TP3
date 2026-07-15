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

    public static bool HasEnoughTrianglesInNode(OctreeNode octreeNode, int triangleLimit)
    {
        int triangleCount = 0;

        foreach (BaseCollisionObject obj in octreeNode.objects)
        {
            if (obj.BVHRoot == null || obj.Triangles == null)
                continue;

            AABB localOctreeBounds = obj.InverseTransformAABB(octreeNode.Bounds);

            if (CountTrianglesInBVH(obj.BVHRoot, obj, localOctreeBounds, triangleLimit, ref triangleCount))
                return true;
        }

        return false;
    }

    private static bool CountTrianglesInBVH(BVHNode bvhNode, BaseCollisionObject owner, AABB localOctreeBounds, int triangleLimit, ref int triangleCount)
    {
        if (bvhNode == null)
            return false;

        //Todo esta en espacio local.
        if (!AABBvsAABB(bvhNode.Bounds, localOctreeBounds))
            return false;

        if (!bvhNode.IsLeaf)
        {
            if (CountTrianglesInBVH(bvhNode.Left, owner, localOctreeBounds, triangleLimit, ref triangleCount))
                return true;

            if (CountTrianglesInBVH(bvhNode.Right, owner, localOctreeBounds, triangleLimit, ref triangleCount))
                return true;

            return false;
        }

        foreach (int triangleIndex in bvhNode.TriangleIndices)
        {
            Triangle triangle = owner.Triangles[triangleIndex];

            if (!AABBvsAABB(triangle.localAABB, localOctreeBounds))
                continue;

            triangleCount++;

            if (triangleCount >= triangleLimit)
                return true;
        }

        return false;
    }

    public static void CollectTrianglesForLeaf(OctreeNode octreeNode, int collisionStep)
    {
        foreach (BaseCollisionObject obj in octreeNode.objects)
        {
            if (obj.BVHRoot == null || obj.Triangles == null)
                continue;

            AABB localOctreeBounds = obj.InverseTransformAABB(octreeNode.Bounds);

            CollectLeafTrianglesFromBVH(obj.BVHRoot, obj, localOctreeBounds, octreeNode, collisionStep);
        }
    }

    private static void CollectLeafTrianglesFromBVH(BVHNode bvhNode, BaseCollisionObject owner, AABB localOctreeBounds, OctreeNode octreeNode, int collisionStep)
    {
        if (bvhNode == null)
            return;

        if (!AABBvsAABB(bvhNode.Bounds, localOctreeBounds))
            return;

        if (!bvhNode.IsLeaf)
        {
            CollectLeafTrianglesFromBVH(bvhNode.Left, owner, localOctreeBounds, octreeNode, collisionStep);
            CollectLeafTrianglesFromBVH(bvhNode.Right, owner, localOctreeBounds, octreeNode, collisionStep);

            return;
        }

        List<TriangleReference> list = null;

        foreach (int triangleIndex in bvhNode.TriangleIndices)
        {
            Triangle triangle = owner.Triangles[triangleIndex];

            // Filtro barato en espacio local.
            if (!AABBvsAABB(triangle.localAABB, localOctreeBounds))
                continue;

            TriangleReference reference = owner.GetTriangleReference(triangleIndex, collisionStep);

            // Mantener la esfera mínima como prueba final
            // de pertenencia al nodo del octree.
            if (!SphereVsAABB(reference.sphere, octreeNode.Bounds))
                continue;

            if (list == null)
            {
                if (!octreeNode.triangles.TryGetValue(owner, out list))
                {
                    list = new List<TriangleReference>();
                    octreeNode.triangles.Add(owner, list);
                }
            }

            // Se reutiliza siempre la misma referencia.
            list.Add(reference);
        }
    }

    public static bool VertexPlaneTest(TriangleReference planeTriangle, TriangleReference testTriangle,
        out Vector3 oppositeVertex, out Vector3 edgeVertex1, out Vector3 edgeVertex2)
    {
        oppositeVertex = Vector3.zero;
        edgeVertex1 = Vector3.zero;
        edgeVertex2 = Vector3.zero;

        //Triangulo del plano (en mundo)
        Vector3 p1 = planeTriangle.owner.transform.TransformPoint(planeTriangle.triangle.v1);
        Vector3 p2 = planeTriangle.owner.transform.TransformPoint(planeTriangle.triangle.v2);
        Vector3 p3 = planeTriangle.owner.transform.TransformPoint(planeTriangle.triangle.v3);

        //Normal del plano
        Vector3 normal = Vector3.Cross(p2 - p1, p3 - p1).normalized;

        //Vertices del otro triangulo (en mundo)
        Vector3[] vertices =
        {
        testTriangle.owner.transform.TransformPoint(testTriangle.triangle.v1),
        testTriangle.owner.transform.TransformPoint(testTriangle.triangle.v2),
        testTriangle.owner.transform.TransformPoint(testTriangle.triangle.v3)
        };

        float[] distances = new float[3];

        bool hasPositive = false;
        bool hasNegative = false;

        for (int i = 0; i < 3; i++)
        {
            distances[i] = Vector3.Dot(normal, vertices[i] - p1);

            if (distances[i] > 0f)
                hasPositive = true;
            else if (distances[i] < 0f)
                hasNegative = true;
        }

        //Si todos estan del mismo lado, no atraviesa el plano.
        if (!(hasPositive && hasNegative))
            return false;

        //Devolver el vertice que quedo solo del otro lado.
        if (hasPositive)
        {
            int positives = 0;
            int index = -1;

            for (int i = 0; i < 3; i++)
            {
                if (distances[i] > 0f)
                {
                    positives++;
                    index = i;
                }
            }

            if (positives == 1)
            {
                oppositeVertex = vertices[index];
                edgeVertex1 = vertices[(index + 1) % 3];
                edgeVertex2 = vertices[(index + 2) % 3];
                return true;
            }
        }

        if (hasNegative)
        {
            int negatives = 0;
            int index = -1;

            for (int i = 0; i < 3; i++)
            {
                if (distances[i] < 0f)
                {
                    negatives++;
                    index = i;
                }
            }

            if (negatives == 1)
            {
                oppositeVertex = vertices[index];
                edgeVertex1 = vertices[(index + 1) % 3];
                edgeVertex2 = vertices[(index + 2) % 3];
                return true;
            }
        }

        return false;
    }

    public static bool RayVsTriangle(Vector3 rayOrigin, Vector3 rayDirection, float maxDistance, TriangleReference triangle,
        out Vector3 hitPoint)
    {
        hitPoint = Vector3.zero;
        const float EPSILON = 0.00001f;

        Vector3 v0 = triangle.owner.transform.TransformPoint(triangle.triangle.v1);
        Vector3 v1 = triangle.owner.transform.TransformPoint(triangle.triangle.v2);
        Vector3 v2 = triangle.owner.transform.TransformPoint(triangle.triangle.v3);

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
