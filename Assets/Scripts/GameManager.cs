using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{

    [Header("Objects")]
    [SerializeField] private Car car1;
    [SerializeField] private Car car2;

    [Header("Octree")]
    [SerializeField] private float parentSize;
    [SerializeField] private float minSize;
    private OctreeNode parentNode;
    private List<BaseCollisionObject> objects = new();

    [Header("Constraints")]
    [SerializeField] private float minObjectsToDivide = 2f;
    [SerializeField] private float minTrianglesToDivide = 64f;

    private List<OctreeNode> octreeNodes = new List<OctreeNode>();

    private void Awake()
    {
        parentNode = new OctreeNode(transform.position, parentSize, null);
        objects.Add(car1);
        objects.Add(car2);
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
        parentNode.Clear();
        octreeNodes.Clear();
        UpdateOctree(parentNode);
        //Debug.Log(octreeNodes.Count);

        ClearNodeTriangles();
    }

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

                Sphere sphereA = a.sphere;
                Sphere sphereB = b.sphere;
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

        octreeNodes.Add(node);

        // Limpiar información del frame anterior
        node.objects.Clear();
        node.triangles.Clear();

        // 1) AABB de los modelos vs Octree
        Collisions.CheckObjectsOctreeNodes(objects, node);

        // Si el nodo contiene menos de dos objetos, no puede haber colisión
        if (node.objects.Count < minObjectsToDivide)
            return;

        // 2) ¿Los AABB de los autos colisionan?
        if (!Collisions.ObjectsCollisionInsideNode(node))
            return;

        // 3) Insertar únicamente los triángulos de esos autos dentro del nodo
        Collisions.CheckTriangleOctree(node);

        // 4) Si todavía hay demasiados triángulos, subdividir
        if (node.triangles.Count >= minTrianglesToDivide &&
            node.Size / 2f >= minSize)
        {
            node.GenerateChildren();

            foreach (OctreeNode child in node.Children)
                UpdateOctree(child);

            return;
        }

        // 5) Sphere vs Sphere de los triángulos
        if (node.triangles.Count < 2)
            return;

        List<TriangleReference> collidingTriangles = CheckTriangleSphere(node);

        if (collidingTriangles == null)
            return;

        // ==========================================
        // 6) Vertex vs Plane
        // ==========================================

        // TODO

        // ==========================================
        // 7) Ray vs Triangle
        // ==========================================

        // TODO
    }


    private void OnDrawGizmos()
    {
        if (parentNode != null)
            parentNode.Draw();
    }
}