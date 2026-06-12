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
        //ACCELERATION
        float move1 = 0f;

        if (Input.GetKey(KeyCode.W))
            move1 = 1f;
        else if (Input.GetKey(KeyCode.S))
            move1 = -1f;

        car1.SetMovementInput(move1);

        //ROTATION
        float rotate1 = 0f;

        if (Input.GetKey(KeyCode.A))
            rotate1 = -1f;
        else if (Input.GetKey(KeyCode.D))
            rotate1 = 1f;

        car1.SetRotationInput(rotate1);

        //JUMP
        if (Input.GetKeyDown(KeyCode.Space))
            car1.Jump();

        //CAR 2
        //ACCELERATION
        float move2 = 0f;

        if (Input.GetKey(KeyCode.UpArrow))
            move2 = 1f;
        else if (Input.GetKey(KeyCode.DownArrow))
            move2 = -1f;

        car2.SetMovementInput(move2);

        //ROTATION
        float rotate2 = 0f;

        if (Input.GetKey(KeyCode.LeftArrow))
            rotate2 = -1f;
        else if (Input.GetKey(KeyCode.RightArrow))
            rotate2 = 1f;

        car2.SetRotationInput(rotate2);


        //JUMP
        if (Input.GetKeyDown(KeyCode.RightControl))
            car2.Jump();
    }

    private void FixedUpdate()
    {
        octreeNodes.Clear();
        UpdateOctree(parentNode);
        //DebugOctreeNodes();
        Collisions.CheckCarOctreeNodes(car1, octreeNodes);

        if (CheckOctreeAABB())
            if (Collisions.AABBvsAABB(car1.Bounds, car2.Bounds))
                //Triangle Sphere
                    //Triangles vertex
                        //Triangles Ray
                return;
    }

    private void OnValidate()
    {
        //UpdateOctree(parentNode);
    }

    private bool CheckOctreeAABB()
    {
        for (int i = 0; i < car1.occupiedNodes.Count; i++)
            if (car2.occupiedNodes.Contains(car1.occupiedNodes[i]))
                return true;

        return false;
    }

    private void UpdateOctree(OctreeNode node)
    {
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