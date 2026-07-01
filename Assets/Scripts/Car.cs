using System;
using System.Collections.Generic;
using Unity.Burst.Intrinsics;
using UnityEngine;

public class Car : BaseCollisionObject
{
    [Header("Physics")]
    [SerializeField] private float mass = 10f;
    [SerializeField] private float inputForce = 150f;
    [SerializeField] private float frictionCoefficient = 0.5f;
    [SerializeField] private float restitution = 0.3f;
    [SerializeField] private MeshRenderer meshRenderer;
    [SerializeField] private MeshFilter meshFilter;

    [Header("Rotation")]
    [SerializeField] private float rotationSpeed = 70f;

    [Header("Jump")]
    [SerializeField] private float jumpImpulse = 5f;

    private const float GRAVITY = 9.8f;

    private float forwardSpeed;
    private float verticalSpeed;

    private float movementInput;
    private float rotationInput;

    private bool isGrounded = true;

    private List<Triangle> triangles = new List<Triangle>();


    public float Mass => mass;
    public float Restitution => restitution;
    public AABB Bounds => new AABB(transform.position, meshRenderer.bounds.extents * 2);

    public float ForwardSpeed
    {
        get => forwardSpeed;
        set => forwardSpeed = value;
    }

    public float VerticalSpeed
    {
        get => verticalSpeed;
        set => verticalSpeed = value;
    }

    private AABBVolume collisionVolume;

    public override CollisionVolume CollisionVolume
    {
        get
        {
            collisionVolume ??= new AABBVolume(Bounds);
            collisionVolume.Bounds = Bounds;
            return collisionVolume;
        }
    }

    public override List<Triangle> Triangles => triangles;

    private void Awake()
    {
        SaveTriangles();
    }


    private void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;

        float appliedForce = movementInput * inputForce;
        float frictionForce = 0f;
        float normalForce = mass * GRAVITY;

        if (Mathf.Abs(forwardSpeed) > 0.001f)
        {
            frictionForce = -Mathf.Sign(forwardSpeed) * frictionCoefficient * normalForce;
        }
        else if (movementInput != 0)
        {
            float maxStaticFriction = frictionCoefficient * normalForce;

            if (Mathf.Abs(appliedForce) < maxStaticFriction)
            {
                forwardSpeed = 0f;
                return;
            }

            frictionForce = -Mathf.Sign(appliedForce) * maxStaticFriction;
        }

        float totalForce = appliedForce + frictionForce;
        float acceleration = totalForce / mass;


        forwardSpeed += acceleration * dt;

        if (movementInput == 0 && Mathf.Abs(forwardSpeed) < 0.01f)
            forwardSpeed = 0f;

        /*
        if (!isGrounded)
        {
            verticalSpeed -= GRAVITY * dt;
        }
        */

        Vector3 horizontalMovement = transform.forward * forwardSpeed * dt;
        Vector3 verticalMovement = Vector3.up * verticalSpeed * dt;
        transform.position += horizontalMovement + verticalMovement;


        float steeringFactor = Mathf.Clamp01(Mathf.Abs(forwardSpeed) / 5f);
        transform.Rotate(Vector3.up, rotationInput * rotationSpeed * steeringFactor * dt);
    }

    public void Jump()
    {
        if (!isGrounded)
            return;

        verticalSpeed = jumpImpulse;
        isGrounded = false;
    }

    public void SetGrounded(bool grounded)
    {
        isGrounded = grounded;
    }


    public void SetMovementInput(float value)
    {
        movementInput = Mathf.Clamp(value, -1f, 1f);
    }

    public void SetRotationInput(float value)
    {
        rotationInput = Mathf.Clamp(value, -1f, 1f);
    }

    private void SaveTriangles()
    {
        Mesh mesh = meshFilter.mesh;

        Vector3[] vertices = mesh.vertices;
        int[] trianglesArray = mesh.triangles;

        triangles.Clear();

        for (int i = 0; i < trianglesArray.Length; i += 3)
        {
            Vector3 v1 = vertices[trianglesArray[i]];
            Vector3 v2 = vertices[trianglesArray[i + 1]];
            Vector3 v3 = vertices[trianglesArray[i + 2]];

            triangles.Add(new Triangle(v1, v2, v3));
        }
    }

    public override Sphere GetTriangleSphere(Triangle triangle)
    {
        Vector3 worldCenter = transform.TransformPoint(triangle.localBoundingSphere.center);

        float scale =
            Mathf.Max(
                transform.lossyScale.x,
                Mathf.Max(transform.lossyScale.y, transform.lossyScale.z));

        return new Sphere(worldCenter, triangle.localBoundingSphere.radius * scale);
    }


    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(Bounds.center, Bounds.halfSize * 2);

        if (triangles == null)
            return;

        //foreach (Triangle triangle in triangles)
        //{
        //    Sphere sphere = GetTriangleSphere(triangle);
        //    Gizmos.color = Color.cyan;
        //    Gizmos.DrawWireSphere(sphere.center, sphere.radius);
        //}
    }
}