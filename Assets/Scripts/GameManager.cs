using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    [SerializeField] private Car car1;
    [SerializeField] private Car car2;
    [SerializeField] private OctreeNode parentNode;

    private List<OctreeNode> octreeNodes = new List<OctreeNode>();

    private void Update()
    {
        //CAR 1
        float move1 = 0f;
        if (Input.GetKey(KeyCode.W))
            move1 = 1f;
        else if (Input.GetKey(KeyCode.S))
            move1 = -1f;
        car1.SetMovementInput(move1);

        float rotate1 = 0f;
        if (Input.GetKey(KeyCode.A))
            rotate1 = -1f;
        else if (Input.GetKey(KeyCode.D))
            rotate1 = 1f;
        car1.SetRotationInput(rotate1);

        if (Input.GetKeyDown(KeyCode.Space))
            car1.Jump();

        //CAR 2
        float move2 = 0f;
        if (Input.GetKey(KeyCode.UpArrow))
            move2 = 1f;
        else if (Input.GetKey(KeyCode.DownArrow))
            move2 = -1f;
        car2.SetMovementInput(move2);

        float rotate2 = 0f;
        if (Input.GetKey(KeyCode.LeftArrow))
            rotate2 = -1f;
        else if (Input.GetKey(KeyCode.RightArrow))
            rotate2 = 1f;
        car2.SetRotationInput(rotate2);

        if (Input.GetKeyDown(KeyCode.RightControl))
            car2.Jump();
    }

    private void FixedUpdate()
    {
        DebugOctreeNodes();
        UpdateOctree(parentNode);
    }

    private void OnValidate()
    {
        //UpdateOctree(parentNode);
    }

    private void UpdateOctree(OctreeNode node)
    {
        if (node == null)
        {
            Debug.LogWarning("Node is null");
            if (octreeNodes.Contains(node))
                octreeNodes.Remove(node);
        }

        if (!octreeNodes.Contains(node))
            octreeNodes.Add(node);

        foreach (OctreeNode child in node.Children)
            UpdateOctree(child);
    }

    [ContextMenu("Debug Octree Nodes")]
    private void DebugOctreeNodes()
    {
        foreach (OctreeNode node in octreeNodes)
        {
            if (node == null)
                Debug.LogWarning("Node is null 2");
            else
                Debug.Log(node.gameObject.name);
        }
    }
}