using System.Collections.Generic;

public class BVHNode
{
    public AABB Bounds;

    public BVHNode Left;
    public BVHNode Right;

    public List<int> TriangleIndices;

    public bool IsLeaf => Left == null && Right == null;
}