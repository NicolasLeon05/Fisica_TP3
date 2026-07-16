using System.Collections.Generic;
using UnityEngine;

public sealed class TriangleOctree
{
    private readonly Vector3 center;
    private readonly float rootSize;

    private readonly float minNodeSize;
    private readonly int maxDepth;
    private readonly int maxTrianglesPerNode;

    public int RejectedOutsideRoot { get; private set; }
    public int RejectedInvalidBounds { get; private set; }

    public TriangleOctreeNode Root { get; private set; }

    public int RootReferenceCount => Root != null ? Root.Triangles.Count : 0;

    public int NodeCount => CountNodes(Root);

    public int StoredReferenceCount => CountStoredReferences(Root);

    public TriangleOctree(Vector3 center, float rootSize, float minNodeSize, int maxDepth, int maxTrianglesPerNode)
    {
        this.center = center;
        this.rootSize = rootSize;
        this.minNodeSize = minNodeSize;
        this.maxDepth = maxDepth;
        this.maxTrianglesPerNode = maxTrianglesPerNode;

        Reset();
    }

    public void Reset()
    {
        Root = new TriangleOctreeNode(center, rootSize, 0);
    }

    public void Build(IReadOnlyList<TriangleReference> triangleReferences)
    {
        Reset();

        RejectedOutsideRoot = 0;
        RejectedInvalidBounds = 0;

        for (int i = 0; i < triangleReferences.Count; i++)
            Insert(triangleReferences[i]);
    }

    public void Refill(IReadOnlyList<TriangleReference> triangleReferences)
    {
        Root.ClearTrianglesRecursive();

        RejectedOutsideRoot = 0;
        RejectedInvalidBounds = 0;

        for (int i = 0; i < triangleReferences.Count; i++)
            Insert(triangleReferences[i]);
    }

    public void Insert(TriangleReference reference)
    {
        if (reference == null)
            return;

        if (!IsValidAABB(reference.bounds))
        {
            RejectedInvalidBounds++;
            return;
        }

        if (!AABBIntersectsAABB(reference.bounds, Root.Bounds))
        {
            RejectedOutsideRoot++;
            return;
        }

        InsertRecursive(Root, reference);
    }

    private void InsertRecursive(TriangleOctreeNode node, TriangleReference reference)
    {
        while (!node.IsLeaf)
        {
            TriangleOctreeNode containingChild = GetContainingChild(node, reference.bounds);

            if (containingChild == null)
            {
                node.Triangles.Add(reference);
                return;
            }

            node = containingChild;
        }

        node.Triangles.Add(reference);

        if (ShouldSubdivide(node))
            Subdivide(node);
    }

    private void Subdivide(TriangleOctreeNode node)
    {
        if (!node.IsLeaf)
            return;

        node.CreateChildren();
        TriangleReference[] previousTriangles = node.Triangles.ToArray();
        node.Triangles.Clear();

        for (int i = 0; i < previousTriangles.Length; i++)
        {
            TriangleReference reference = previousTriangles[i];
            TriangleOctreeNode containingChild = GetContainingChild(node, reference.bounds);

            if (containingChild != null)
                InsertRecursive(containingChild, reference);
            else
                node.Triangles.Add(reference);
            /*
             * No entra completamente en un hijo.
             * Permanece almacenado en el padre.
             */
        }
    }

    private bool ShouldSubdivide(TriangleOctreeNode node)
    {
        if (node.Triangles.Count <= maxTrianglesPerNode)
            return false;

        if (node.Depth >= maxDepth)
            return false;

        float childSize = node.Size * 0.5f;

        return childSize >= minNodeSize;
    }

    private TriangleOctreeNode GetContainingChild(TriangleOctreeNode node, AABB triangleBounds)
    {
        if (node.Children == null)
            return null;

        Vector3 nodeCenter = node.Bounds.center;

        int childIndex = 0;

        if (triangleBounds.center.x >= nodeCenter.x)
            childIndex += 4;

        if (triangleBounds.center.y >= nodeCenter.y)
            childIndex += 2;

        if (triangleBounds.center.z >= nodeCenter.z)
            childIndex += 1;

        TriangleOctreeNode child = node.Children[childIndex];

        if (!AABBFullyInsideAABB(triangleBounds, child.Bounds))
            return null;

        return child;
    }

    private static bool AABBFullyInsideAABB(AABB inner, AABB outer)
    {
        Vector3 difference = inner.center - outer.center;

        return
            Mathf.Abs(difference.x) + inner.halfSize.x <= outer.halfSize.x &&
            Mathf.Abs(difference.y) + inner.halfSize.y <= outer.halfSize.y &&
            Mathf.Abs(difference.z) + inner.halfSize.z <= outer.halfSize.z;
    }

    private static bool AABBIntersectsAABB(AABB a, AABB b)
    {
        Vector3 difference = a.center - b.center;

        return
            Mathf.Abs(difference.x) <= a.halfSize.x + b.halfSize.x &&
            Mathf.Abs(difference.y) <= a.halfSize.y + b.halfSize.y &&
            Mathf.Abs(difference.z) <= a.halfSize.z + b.halfSize.z;
    }

    public void Query(Sphere querySphere, List<TriangleReference> results)
    {
        if (results == null)
            return;

        QueryRecursive(Root, querySphere, results);
    }


    public void Query(AABB queryBounds, List<TriangleReference> results)
    {
        if (results == null)
            return;

        QueryRecursive(Root, queryBounds, results);
    }

    private void QueryRecursive(TriangleOctreeNode node, Sphere querySphere, List<TriangleReference> results)
    {
        if (node == null)
            return;

        if (!SphereIntersectsAABB(querySphere, node.Bounds))
            return;

        foreach (TriangleReference reference in node.Triangles)
            results.Add(reference);

        if (node.Children == null)
            return;

        foreach (TriangleOctreeNode child in node.Children)
            QueryRecursive(child, querySphere, results);
    }

    private void QueryRecursive(TriangleOctreeNode node, AABB queryBounds, List<TriangleReference> results)
    {
        if (node == null)
            return;

        if (!AABBIntersectsAABB(queryBounds, node.Bounds))
            return;

        for (int i = 0; i < node.Triangles.Count; i++)
        {
            TriangleReference reference = node.Triangles[i];

            if (AABBIntersectsAABB(queryBounds, reference.bounds))
                results.Add(reference);
        }

        if (node.Children == null)
            return;

        for (int i = 0; i < node.Children.Length; i++)
            QueryRecursive(node.Children[i], queryBounds, results);
    }

    private static bool SphereIntersectsAABB(Sphere sphere, AABB bounds)
    {
        Vector3 closestPoint = new Vector3(Mathf.Clamp(sphere.center.x, bounds.Min.x, bounds.Max.x),

            Mathf.Clamp(sphere.center.y, bounds.Min.y, bounds.Max.y),
            Mathf.Clamp(sphere.center.z, bounds.Min.z, bounds.Max.z));

        float distanceSquared = (sphere.center - closestPoint).sqrMagnitude;

        return distanceSquared <= sphere.radius * sphere.radius;
    }

    private static int CountNodes(TriangleOctreeNode node)
    {
        if (node == null)
            return 0;

        int count = 1;

        if (node.Children == null)
            return count;

        foreach (TriangleOctreeNode child in node.Children)
            count += CountNodes(child);

        return count;
    }

    private static int CountStoredReferences(TriangleOctreeNode node)
    {
        if (node == null)
            return 0;

        int count = node.Triangles.Count;

        if (node.Children == null)
            return count;

        for (int i = 0; i < node.Children.Length; i++)
            count += CountStoredReferences(node.Children[i]);

        return count;
    }

    private static bool IsFinite(Vector3 value)
    {
        return
            !float.IsNaN(value.x) &&
            !float.IsNaN(value.y) &&
            !float.IsNaN(value.z) &&
            !float.IsInfinity(value.x) &&
            !float.IsInfinity(value.y) &&
            !float.IsInfinity(value.z);
    }

    private static bool IsValidAABB(AABB bounds)
    {
        return
            IsFinite(bounds.Min) &&
            IsFinite(bounds.Max) &&
            bounds.Min.x <= bounds.Max.x &&
            bounds.Min.y <= bounds.Max.y &&
            bounds.Min.z <= bounds.Max.z;
    }

    public void DrawGizmos()
    {
        if (Root == null)
            return;

        Root.DrawGizmos();
    }
}