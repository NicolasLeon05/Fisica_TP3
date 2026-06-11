using System.Collections.Generic;
using UnityEngine;
using static Unity.VisualScripting.Metadata;
using static UnityEngine.EventSystems.EventTrigger;

public class OctreeNode : MonoBehaviour
{
    [SerializeField] private float size;
    private List<OctreeNode> children = new List<OctreeNode>();
    private const int CHILDREN_AMOUNT = 8;

    public List<OctreeNode> Children => children;
    public float Size => size;

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

            children.Add(childNode);
        }
    }

}
