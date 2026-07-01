using UnityEngine;

public class Triangle
{
    public Vector3 v1;
    public Vector3 v2;
    public Vector3 v3;

    public Vector3 normal;
    public Vector3 center;

    public Sphere localBoundingSphere;

    public Triangle(Vector3 v1, Vector3 v2, Vector3 v3)
    {
        this.v1 = v1;
        this.v2 = v2;
        this.v3 = v3;

        Vector3 edge1 = v2 - v1;
        Vector3 edge2 = v3 - v1;

        normal = Vector3.Cross(edge1, edge2).normalized;
        center = (v1 + v2 + v3) / 3f;

        localBoundingSphere = CreateBoundingSphere();
    }

    private Sphere CreateBoundingSphere()
    {
        float A = Vector3.Distance(v1, v2);
        float B = Vector3.Distance(v2, v3);
        float C = Vector3.Distance(v3, v1);

        Vector3 a = v3;
        Vector3 b = v1;
        Vector3 c = v2;

        if (B < C)
        {
            (B, C) = (C, B);
            (b, c) = (c, b);
        }

        if (A < B)
        {
            (A, B) = (B, A);
            (a, b) = (b, a);
        }

        Sphere sphere;

        if ((B * B) + (C * C) <= (A * A))
        {
            sphere = new Sphere((b + c) * 0.5f, A * 0.5f);
        }
        else
        {
            float cosA = (B * B + C * C - A * A) / (2f * B * C);
            float radius = A / (2f * Mathf.Sqrt(1f - cosA * cosA));

            Vector3 alpha = a - c;
            Vector3 beta = b - c;

            Vector3 cross = Vector3.Cross(alpha, beta);

            Vector3 center = Vector3.Cross(
                    beta * alpha.sqrMagnitude - alpha * beta.sqrMagnitude,
                    cross) / (2f * cross.sqrMagnitude) + c;

            sphere = new Sphere(center, radius);
        }

        return sphere;
    }
}