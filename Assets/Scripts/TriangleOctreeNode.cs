using System.Collections.Generic;
using UnityEngine;

public class TriangleOctreeNode
{
    public AABB Bounds { get; }
    public int Depth { get; }

    public List<TriangleReference> Triangles { get; } = new();

    public TriangleOctreeNode[] Children { get; private set; }

    public bool IsLeaf => Children == null;

    public float Size => Bounds.halfSize.x * 2f;

    public TriangleOctreeNode(Vector3 center, float size, int depth)
    {
        Bounds = new AABB(center, Vector3.one * size);
        Depth = depth;
    }

    public void CreateChildren()
    {
        if (Children != null)
            return;

        Children = new TriangleOctreeNode[8];

        float childSize = Size * 0.5f;
        float centerOffset = childSize * 0.5f;

        int index = 0;

        for (int x = -1; x <= 1; x += 2)
        {
            for (int y = -1; y <= 1; y += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 offset = new Vector3(x * centerOffset, y * centerOffset, z * centerOffset);

                    Children[index] = new TriangleOctreeNode(Bounds.center + offset, childSize, Depth + 1);

                    index++;
                }
            }
        }
    }

    public void ClearTrianglesRecursive()
    {
        Triangles.Clear();

        if (Children == null)
            return;

        for (int i = 0; i < Children.Length; i++)
            Children[i].ClearTrianglesRecursive();
    }

    public void DrawGizmos()
    {
        Gizmos.DrawWireCube(Bounds.center, Bounds.halfSize * 2f);

        if (Children == null)
            return;

        foreach (TriangleOctreeNode child in Children)
            child.DrawGizmos();
    }
}