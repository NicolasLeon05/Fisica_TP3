using System;
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
    [SerializeField] private int binarySearchLimit = 12;

    private List<OctreeNode> octreeNodes = new List<OctreeNode>();
    private readonly List<CollisionInfo> collisions = new List<CollisionInfo>();

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
        collisions.Clear();

        parentNode.Clear();
        octreeNodes.Clear();
        UpdateOctree(parentNode);

        foreach (CollisionInfo collision in collisions)
            ResolveCollision(collision);

        collisions.Clear();

        ClearNodeTriangles();
    }

    private void ResolveCollision(CollisionInfo info)
    {
        float t = FindCollisionTime(info, binarySearchLimit);

        info.objectA.InterpolateState(t);
        info.objectB.InterpolateState(t);

        info.collisionTime = t;

        if (!CheckCollision(info))
        {
            info.objectA.RestoreState(info.currentStateA);
            info.objectB.RestoreState(info.currentStateB);
            return;
        }

        CalculateContactData(info);

        // TODO
        // Calcular impulso

        info.objectA.RestoreState(info.currentStateA);
        info.objectB.RestoreState(info.currentStateB);
    }

    private float FindCollisionTime(CollisionInfo info, int iterations)
    {
        float left = 0f;
        float right = 1f;

        for (int i = 0; i < iterations; i++)
        {
            float mid = (left + right) * 0.5f;

            info.objectA.InterpolateState(mid);
            info.objectB.InterpolateState(mid);

            if (CheckCollision(info))
                right = mid;
            else
                left = mid;
        }

        return right;
    }

    private bool CheckCollision(CollisionInfo info)
    {
        if (CheckTriangleDirection(info.triangleA, info.triangleB, info))
            return true;

        if (CheckTriangleDirection(info.triangleB, info.triangleA, info))
            return true;

        return false;
    }

    private bool CheckTriangleDirection(TriangleReference planeTriangle, TriangleReference penetratingTriangle, CollisionInfo info)
    {
        Vector3 oppositeVertex;
        Vector3 edge1;
        Vector3 edge2;

        if (!Collisions.VertexPlaneTest(planeTriangle, penetratingTriangle,
            out oppositeVertex, out edge1, out edge2))
        {
            return false;
        }

        Vector3 dir1 = (edge1 - oppositeVertex).normalized;
        Vector3 dir2 = (edge2 - oppositeVertex).normalized;

        float dist1 = Vector3.Distance(oppositeVertex, edge1);
        float dist2 = Vector3.Distance(oppositeVertex, edge2);

        Vector3 hitPoint;

        bool hit =
            Collisions.RayVsTriangle(oppositeVertex, dir1, dist1, planeTriangle, out hitPoint) ||
            Collisions.RayVsTriangle(oppositeVertex, dir2, dist2, planeTriangle, out hitPoint);

        if (!hit)
            return false;

        info.planeTriangle = planeTriangle;
        info.penetratingTriangle = penetratingTriangle;

        info.penetratingVertex = oppositeVertex;
        info.contactPoint = hitPoint;

        return true;
    }

    private void CalculateContactData(CollisionInfo info)
    {
        TriangleReference plane = info.planeTriangle;

        Vector3 p1 = plane.owner.transform.TransformPoint(plane.triangle.v1);
        Vector3 p2 = plane.owner.transform.TransformPoint(plane.triangle.v2);
        Vector3 p3 = plane.owner.transform.TransformPoint(plane.triangle.v3);

        Vector3 normal = Vector3.Cross(p2 - p1, p3 - p1).normalized;

        // Siempre apuntar hacia el objeto que penetra
        Vector3 planeCenter = (p1 + p2 + p3) / 3f;

        if (Vector3.Dot(normal, info.penetratingVertex - planeCenter) < 0f)
            normal = -normal;

        info.contactNormal = normal;

        // Lo devuelve RayVsTriangle()
        // No hace falta recalcularlo.
        // info.contactPoint ya está cargado.

        info.penetration = Vector3.Dot(info.penetratingVertex - info.contactPoint, normal);

        if (info.penetration < 0f)
            info.penetration = -info.penetration;
    }

    private void ClearNodeTriangles()
    {
        foreach (OctreeNode node in octreeNodes)
            node.triangles.Clear();
    }


    private List<CollisionInfo> GetCollisionCandidates(OctreeNode node)
    {
        List<CollisionInfo> results = new();

        List<BaseCollisionObject> owners = new(node.triangles.Keys);

        for (int i = 0; i < owners.Count; i++)
        {
            List<TriangleReference> listA = node.triangles[owners[i]];

            for (int j = i + 1; j < owners.Count; j++)
            {
                List<TriangleReference> listB = node.triangles[owners[j]];

                foreach (TriangleReference triangleA in listA)
                {
                    foreach (TriangleReference triangleB in listB)
                    {
                        if (!Collisions.SphereVsSphere(triangleA.sphere, triangleB.sphere))
                            continue;

                        results.Add(BuildCollisionInfo(triangleA, triangleB));
                    }
                }
            }
        }

        return results;
    }

    private void UpdateOctree(OctreeNode node)
    {
        if (node == null)
            return;

        octreeNodes.Add(node);

        // Limpiar informacion del frame anterior
        node.objects.Clear();
        node.triangles.Clear();

        // 1) AABB de los modelos vs Octree
        Collisions.CheckObjectsOctreeNodes(objects, node);

        if (node.objects.Count < minObjectsToDivide)
            return;

        // 2) Los AABB de los autos colisionan?
        if (!Collisions.ObjectsCollideInsideNode(node))
            return;

        // 3) Insertar unicamente los triangulos de esos autos dentro del nodo
        Collisions.SaveTriangleOctree(node);

        // 4) Si todavía hay demasiados triangulos, subdividir
        if (node.GetTriangleCount() >= minTrianglesToDivide &&
            node.Size / 2f >= minSize)
        {
            node.GenerateChildren();

            foreach (OctreeNode child in node.Children)
                UpdateOctree(child);

            return;
        }

        if (node.triangles.Count < 2)
            return;

        // 5) Sphere vs Sphere de los triangulos
        List<CollisionInfo> candidates = GetCollisionCandidates(node);

        if (candidates.Count == 0)
            return;

        // 6) 
        foreach (CollisionInfo collision in candidates)
        {
            if (!CheckCollision(collision))
                continue;

            collisions.Add(collision);
        }

    }

    private CollisionInfo BuildCollisionInfo(TriangleReference triangleA, TriangleReference triangleB)
    {
        CollisionInfo info = new CollisionInfo();

        info.objectA = triangleA.owner;
        info.objectB = triangleB.owner;

        info.triangleA = triangleA;
        info.triangleB = triangleB;

        info.previousStateA = triangleA.owner.PreviousState;
        info.currentStateA = triangleA.owner.CurrentState;

        info.previousStateB = triangleB.owner.PreviousState;
        info.currentStateB = triangleB.owner.CurrentState;

        return info;
    }

    private void OnDrawGizmos()
    {
        //if (parentNode != null)
        //    parentNode.Draw();
    }
}