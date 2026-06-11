using System.Collections.Generic;
using UnityEngine;

public class OctreeNode : MonoBehaviour
{
    [SerializeField] private float size;
    private List<OctreeNode> children = new List<OctreeNode>();
    private const int CHILDREN_AMOUNT = 8;
    private OctreeNode parent = null;


    public List<OctreeNode> Children => children;
    public float Size => size;
    public AABB Bounds => new AABB(transform.position, new Vector3(size, size, size));
    public OctreeNode Parent => parent;

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

    public void Initialize(Vector3 position, float size)
    {
        transform.position = position;
        this.size = size;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(transform.position, new Vector3(size, size, size));
    }

    [ContextMenu("Generate Children")]
    private void GenerateChildren()
    {
        if (children.Count > 0)
            return;

        float newSize = size / 2f;
        float delta = size / 4f;

        for (int i = 0; i < CHILDREN_AMOUNT; i++)
        {
            GameObject childGO = new GameObject($"OctreeNode_Child_{i}");
            childGO.transform.SetParent(transform);

            OctreeNode childNode = childGO.AddComponent<OctreeNode>();
            Vector3 childPosition = transform.position + (Directions[i] * delta);
            childNode.Initialize(childPosition, newSize);
            childNode.SetParent(this);
            children.Add(childNode);
        }
    }

    [ContextMenu("Destroy Children")]
    private void DestroyChildren()
    {
        Debug.Log("Destroy called, children amount: " + children.Count);
        if (children.Count == 0)
            return;

        foreach (var child in children)
        {
            child.DestroyChildren();
            DestroyImmediate(child.gameObject);
        }

        children.Clear();
    }

    public void SetParent(OctreeNode parent)
    {
        this.parent = parent;
    }
}
