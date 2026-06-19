using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    [SerializeField] private Car car1;
    [SerializeField] private Car car2;
    [SerializeField] private OctreeNode parentNode;

    private List<OctreeNode> octreeNodes = new List<OctreeNode>();
    private List<OctreeNode> sharedNodes = new List<OctreeNode>();

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
        Collisions.CheckCarOctreeNodes(car2, octreeNodes);

        if (!CheckOctreeAABB())
            return;

        if (!Collisions.AABBvsAABB(car1.Bounds, car2.Bounds))
            return;
        else
            Debug.Log("CAR AABBB COLLISION");

        ClearNodeTriangles();

        CheckTriangleOctree(car1);
        CheckTriangleOctree(car2);

        CheckTriangleSphere();

        //if (CheckOctreeAABB())
        //    if (Collisions.AABBvsAABB(car1.Bounds, car2.Bounds))
        //        //Triangle Octree
        //        //Triangle Sphere
        //        //Triangles vertex
        //        //Triangles Ray
        //        return;
    }

    private void OnValidate()
    {
        //UpdateOctree(parentNode);
    }

    private bool CheckOctreeAABB()
    {
        bool collision = false;
        sharedNodes.Clear();

        foreach (OctreeNode node in car1.occupiedNodes)
        {
            if (car2.occupiedNodes.Contains(node))
            {
                collision = true;
                sharedNodes.Add(node);
            }
        }

        return collision;
    }

    private void ClearNodeTriangles()
    {
        foreach (OctreeNode node in octreeNodes)
            node.triangles.Clear();
    }

    private void CheckTriangleOctree(Car car)
    {
        foreach (Triangle triangle in car.Triangles)
        {
            Sphere sphere = car.GetTriangleSphere(triangle);

            foreach (OctreeNode node in sharedNodes)
            {
                //Debug.Log($"{node.name} contains {node.triangles.Count} triangles");
                if (Collisions.SphereVsAABB(sphere, node.Bounds))
                    node.triangles.Add(new TriangleReference(car, triangle));
            }
        }
    }

    private void CheckTriangleSphere()
    {
        foreach (OctreeNode node in sharedNodes)
        {
            for (int i = 0; i < node.triangles.Count; i++)
            {
                TriangleReference a = node.triangles[i];
                for (int j = i + 1; j < node.triangles.Count; j++)
                {
                    TriangleReference b = node.triangles[j];

                    if (a.owner == b.owner)
                        continue;

                    Sphere sphereA = a.owner.GetTriangleSphere(a.triangle);
                    Sphere sphereB = b.owner.GetTriangleSphere(b.triangle);
                    if (!Collisions.SphereVsSphere(sphereA, sphereB))
                        continue;

                    //Debug.Log("Sphere collision");
                }
            }
        }
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