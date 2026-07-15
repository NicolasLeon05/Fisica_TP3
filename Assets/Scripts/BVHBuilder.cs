using System.Collections.Generic;
using UnityEngine;

public static class BVHBuilder
{
    private const int MAX_TRIANGLES_PER_LEAF = 64;

    public static BVHNode Build(List<Triangle> triangles)
    {
        if (triangles == null || triangles.Count == 0)
            return null;

        List<int> indices = new List<int>(triangles.Count);

        for (int i = 0; i < triangles.Count; i++)
            indices.Add(i);

        return BuildRecursive(triangles, indices);
    }

    private static BVHNode BuildRecursive(List<Triangle> triangles, List<int> indices)
    {
        BVHNode node = new BVHNode
        {
            Bounds = CalculateBounds(triangles, indices)
        };

        if (indices.Count <= MAX_TRIANGLES_PER_LEAF)
        {
            node.TriangleIndices = indices;
            return node;
        }

        int splitAxis = GetLongestAxis(node.Bounds);

        indices.Sort((indexA, indexB) =>
        {
            float centerA = GetAxisValue(triangles[indexA].center, splitAxis);
            float centerB = GetAxisValue(triangles[indexB].center, splitAxis);

            return centerA.CompareTo(centerB);
        });

        int middle = indices.Count / 2;

        // Proteccion ante una division invalida.
        if (middle <= 0 || middle >= indices.Count)
        {
            node.TriangleIndices = indices;
            return node;
        }

        List<int> leftIndices = indices.GetRange(0, middle);
        List<int> rightIndices = indices.GetRange(middle, indices.Count - middle);

        node.Left = BuildRecursive(triangles, leftIndices);
        node.Right = BuildRecursive(triangles, rightIndices);

        return node;
    }

    private static int GetLongestAxis(AABB bounds)
    {
        Vector3 size = bounds.halfSize * 2f;

        if (size.x >= size.y && size.x >= size.z)
            return 0;

        if (size.y >= size.x && size.y >= size.z)
            return 1;

        return 2;
    }

    private static float GetAxisValue(Vector3 value, int axis)
    {
        switch (axis)
        {
            case 0:
                return value.x;

            case 1:
                return value.y;

            default:
                return value.z;
        }
    }

    private static AABB CalculateBounds(List<Triangle> triangles, List<int> indices)
    {
        Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);

        foreach (int index in indices)
        {
            Triangle triangle = triangles[index];

            min = Vector3.Min(min, triangle.v1);
            min = Vector3.Min(min, triangle.v2);
            min = Vector3.Min(min, triangle.v3);

            max = Vector3.Max(max, triangle.v1);
            max = Vector3.Max(max, triangle.v2);
            max = Vector3.Max(max, triangle.v3);
        }

        Vector3 center = (min + max) * 0.5f;
        Vector3 size = max - min;
        return new AABB(center, size);
    }
}