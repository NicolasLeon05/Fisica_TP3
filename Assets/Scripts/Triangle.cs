using UnityEngine;

public class Triangle
{
    public Vector3 v1;
    public Vector3 v2;
    public Vector3 v3;

    public Vector3 normal;
    public Vector3 center;

    public Triangle(Vector3 v1, Vector3 v2, Vector3 v3)
    {
        this.v1 = v1;
        this.v2 = v2;
        this.v3 = v3;

        Vector3 edge1 = v2 - v1;
        Vector3 edge2 = v3 - v1;

        normal = Vector3.Cross(edge1, edge2).normalized;
        center = (v1 + v2 + v3) / 3f;

        Vector3 min = Vector3.Min(v1, Vector3.Min(v2, v3));
        Vector3 max = Vector3.Max(v1, Vector3.Max(v2, v3));
    }
}