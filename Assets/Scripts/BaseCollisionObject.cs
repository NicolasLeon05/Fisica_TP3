using System.Collections.Generic;
using UnityEngine;

public abstract class BaseCollisionObject : MonoBehaviour
{
    public abstract CollisionVolume CollisionVolume { get; }

    public abstract List<Triangle> Triangles { get; }

    public abstract Sphere GetTriangleSphere(Triangle triangle);
}