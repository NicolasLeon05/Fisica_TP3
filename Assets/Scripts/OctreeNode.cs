using System;
using System.Collections.Generic;
using UnityEngine;

public class OctreeNode
{
    [SerializeField] private float size;
    [SerializeField] private List<OctreeNode> children = new List<OctreeNode>();

    private const int DIVIDE_AMOUNT = 8;
    private OctreeNode parent = null;
    private int depth;

    public bool HasChildren => children.Count > 0;

    public Dictionary<BaseCollisionObject, List<TriangleReference>> triangles = new();
    public List<BaseCollisionObject> objects = new();

    public List<OctreeNode> Children => children;
    public float Size => size;
    public AABB Bounds => new AABB(_position, new Vector3(size, size, size));
    public OctreeNode Parent
    {
        get => parent;
        set => parent = value;
    }
    public int Depth => depth;


    private Vector3 _position;

    private static readonly Vector3[] Directions = new Vector3[]
    {
        new Vector3(-1, -1, -1),
        new Vector3(1, -1, -1),
        new Vector3(-1, 1, -1),
        new Vector3(1, 1, -1),
        new Vector3(-1, -1, 1),
        new Vector3(1, -1, 1),
        new Vector3(-1, 1, 1),
        new Vector3(1, 1, 1)
    };

    public OctreeNode(Vector3 position, float size, OctreeNode parent)
    {
        _position = position;
        this.size = size;
        Parent = parent;
        depth = parent == null ? 0 : parent.depth + 1;
        triangles = new();
        objects = new();
    }


    public void Draw()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(_position, Vector3.one * size);

        foreach (var child in children)
            child.Draw();
    }

    [ContextMenu("Generate Children")]
    public void GenerateChildren()
    {
        if (children.Count > 0)
            return;

        float newSize = size / 2f;
        float delta = size / 4f;

        for (int i = 0; i < DIVIDE_AMOUNT; i++)
        {
            Vector3 childPosition = _position + (Directions[i] * delta);
            children.Add(GenerateChild(childPosition, newSize, this));
        }
    }

    private OctreeNode GenerateChild(Vector3 childPosition, float newSize, OctreeNode parent)
    {
        OctreeNode newChild = new(childPosition, newSize, parent);
        newChild.Parent = parent;
        newChild._position = childPosition;
        newChild.size = newSize;
        return newChild;
    }

    //public void InsertTriangle(TriangleReference triangle)
    //{
    //    // Si la esfera del triángulo no toca este nodo, no pertenece.
    //    if (!Collisions.SphereVsAABB(triangle.sphere, Bounds))
    //        return;
    //
    //    // Si soy hoja, lo guardo acá.
    //    if (!HasChildren)
    //    {
    //        triangles.Add(triangle);
    //        return;
    //    }
    //
    //    bool inserted = false;
    //
    //    foreach (OctreeNode child in children)
    //    {
    //        if (!Collisions.SphereVsAABB(triangle.sphere, child.Bounds))
    //            continue;
    //
    //        child.InsertTriangle(triangle);
    //        inserted = true;
    //    }
    //
    //    // Si por algún motivo no entró en ningún hijo,
    //    // queda almacenado en este nodo.
    //    if (!inserted)
    //        triangles.Add(triangle);
    //}

    public int GetTriangleCount()
    {
        int total = 0;

        foreach (var pair in triangles)
            total += pair.Value.Count;

        return total;
    }

    public void Clear()
    {
        objects.Clear();
        triangles.Clear();

        foreach (OctreeNode child in children)
            child.Clear();

        children.Clear();
    }

    public void SetPosition(Vector3 position)
    {
        _position = position;
    }
}
