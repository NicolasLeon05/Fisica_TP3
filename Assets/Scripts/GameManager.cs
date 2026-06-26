using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

public class GameManager : MonoBehaviour
{

    [Header("Objects")]
    [SerializeField] private Car car1;
    [SerializeField] private Car car2;
    [SerializeField] private OctreeNode parentNode;
    private List<Car> cars = new();

    [Header("Constraints")]
    [SerializeField] private float minObjectsToDivide = 2f;
    [SerializeField] private float minTrianglesToDivide = 64f;

    private List<OctreeNode> octreeNodes = new List<OctreeNode>();

    private void Awake()
    {
        cars.Add(car1);
        cars.Add(car2);
        parentNode.SetPosition(transform.position);
    }

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

        if (!Collisions.AABBvsAABB(car1.Bounds, car2.Bounds))
            return;
        else
            Debug.Log("CAR AABBB COLLISION");

        ClearNodeTriangles();

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

    //private bool CheckOctreeAABB()
    //{
    //    bool collision = false;
    //    sharedNodes.Clear();
    //
    //    foreach (OctreeNode node in car1.occupiedNodes)
    //    {
    //        if (car2.occupiedNodes.Contains(node))
    //        {
    //            collision = true;
    //            sharedNodes.Add(node);
    //        }
    //    }
    //
    //    return collision;
    //}

    private void ClearNodeTriangles()
    {
        foreach (OctreeNode node in octreeNodes)
            node.triangles.Clear();
    }


    private List<TriangleReference> CheckTriangleSphere(OctreeNode node)
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

                List<TriangleReference> collidingTriangles = new();
                collidingTriangles.Add(a);
                collidingTriangles.Add(b);

                return collidingTriangles;
                //Guardar triangulos que colisionaron
                //Debug.Log("Sphere collision");
            }
        }

        return null;
    }

    private void UpdateOctree(OctreeNode node)
    {
        if (node == null)
            return;

        int a = Time.frameCount;

        if (!octreeNodes.Contains(node))
            octreeNodes.Add(node);

        Collisions.CheckCarOctreeNodes(cars, node);
        Collisions.CheckTriangleOctree(node);

        if (node.cars.Count >= minObjectsToDivide)
        {
            node.GenerateChildren();
            foreach (OctreeNode child in node.Children)
                UpdateOctree(child);
        }
        else if (node.triangles.Count >= minTrianglesToDivide)
        {
            node.GenerateChildren();
            foreach (OctreeNode child in node.Children)
                UpdateOctree(child);
        }
        else if (node.triangles.Count >= 2) //SI ENTRA ACA, YA NO SE SUBDIVIDE Y ANALIZA LA COLISION
        {
            if (!Collisions.AABBvsAABB(car1.Bounds, car2.Bounds))
                return;

            List<TriangleReference> collidingTriangles = CheckTriangleSphere(node);
            if (collidingTriangles != null)
            {
                //Analizar puntos del triangulo en relacion a la normal del otro
                //Rayo desde el punto opuesto a los otros y verificar que cortan el otro triangulo
            }
        }
        return;
    }

    [ContextMenu("Debug Octree Nodes")]
    private void DebugOctreeNodes()
    {
        //foreach (OctreeNode node in octreeNodes)
        //{
        //    if (node == null)
        //        Debug.LogWarning("Node is null 2");
        //    else
        //        Debug.Log(node.gameObject.name);
        //}
    }
}