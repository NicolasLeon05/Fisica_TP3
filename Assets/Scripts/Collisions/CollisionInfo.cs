using UnityEngine;

public class CollisionInfo
{
    public BaseCollisionObject objectA;
    public BaseCollisionObject objectB;

    public TriangleReference triangleA;
    public TriangleReference triangleB;

    // Triangulo utilizado como plano en el test final
    public TriangleReference planeTriangle;

    // Triangulo que posee el vertice que penetra
    public TriangleReference penetratingTriangle;

    public PhysicsState previousStateA;
    public PhysicsState currentStateA;

    public PhysicsState previousStateB;
    public PhysicsState currentStateB;

    // Datos obtenidos durante la deteccion
    public Vector3 penetratingVertex;
    public Vector3 contactPoint;
    public Vector3 contactNormal;

    public float collisionTime;
    public float penetration;
}