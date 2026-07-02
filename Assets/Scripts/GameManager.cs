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
        List<BaseCollisionObject> owners = new(node.triangles.Keys);

        for (int i = 0; i < owners.Count; i++)
        {
            for (int j = i + 1; j < owners.Count; j++)
            {
                List<TriangleReference> listA = node.triangles[owners[i]];

                List<TriangleReference> listB = node.triangles[owners[j]];

                foreach (var a in listA)
                {
                    foreach (var b in listB)
                    {
                        if (!Collisions.SphereVsSphere(a.sphere, b.sphere))
                            continue;

                        return new List<TriangleReference>() { a, b };
                    }
                }
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
        Collisions.SaveTriangleOctree(node);

        // 4) Si todavía hay demasiados triángulos, subdividir
        if (node.GetTriangleCount() >= minTrianglesToDivide &&
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

        // 6) Vertex vs Plane
        Vector3 oppositeVertex;
        Vector3 edgeVertex1;
        Vector3 edgeVertex2;

        if (!Collisions.VertexPlaneTest(collidingTriangles[0], collidingTriangles[1],
                out oppositeVertex, out edgeVertex1, out edgeVertex2))
            return;

        // 7) Ray vs Triangle
        Vector3 dir1 = (edgeVertex1 - oppositeVertex).normalized;
        Vector3 dir2 = (edgeVertex2 - oppositeVertex).normalized;

        float dist1 = Vector3.Distance(oppositeVertex, edgeVertex1);
        float dist2 = Vector3.Distance(oppositeVertex, edgeVertex2);

        bool hit = Collisions.RayVsTriangle(oppositeVertex, dir1, dist1, collidingTriangles[0]) ||
            Collisions.RayVsTriangle(oppositeVertex, dir2, dist2, collidingTriangles[0]);

        //if (hit)
        //    Debug.Log("Colision confirmada");
        //Resolver colision
    }


    private void OnDrawGizmos()
    {
        //if (parentNode != null)
        //    parentNode.Draw();
    }
}