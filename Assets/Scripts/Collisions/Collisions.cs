using UnityEngine;

public static class Collisions
{
    public static bool SphereVsSphere(Sphere a, Sphere b)
    {
        float radiusSum = a.radius + b.radius;

        return (a.center - b.center).sqrMagnitude <= radiusSum * radiusSum;
    }

    public static bool VertexPlaneTest(TriangleReference planeTriangle, TriangleReference testTriangle,
        out Vector3 oppositeVertex, out Vector3 edgeVertex1, out Vector3 edgeVertex2)
    {
        oppositeVertex = Vector3.zero;
        edgeVertex1 = Vector3.zero;
        edgeVertex2 = Vector3.zero;

        //Triangulo del plano (en mundo)
        Vector3 p1 = planeTriangle.owner.CollisionPointToWorld(planeTriangle.triangle.v1);
        Vector3 p2 = planeTriangle.owner.CollisionPointToWorld(planeTriangle.triangle.v2);
        Vector3 p3 = planeTriangle.owner.CollisionPointToWorld(planeTriangle.triangle.v3);

        //Normal del plano
        Vector3 normal = Vector3.Cross(p2 - p1, p3 - p1).normalized;

        //Vertices del otro triangulo (en mundo)
        Vector3[] vertices =
        {
        testTriangle.owner.CollisionPointToWorld(testTriangle.triangle.v1),
        testTriangle.owner.CollisionPointToWorld(testTriangle.triangle.v2),
        testTriangle.owner.CollisionPointToWorld(testTriangle.triangle.v3)
        };

        float[] distances = new float[3];

        bool hasPositive = false;
        bool hasNegative = false;

        for (int i = 0; i < 3; i++)
        {
            distances[i] = Vector3.Dot(normal, vertices[i] - p1);

            if (distances[i] > 0f)
                hasPositive = true;
            else if (distances[i] < 0f)
                hasNegative = true;
        }

        //Si todos estan del mismo lado, no atraviesa el plano.
        if (!(hasPositive && hasNegative))
            return false;

        //Devolver el vertice que quedo solo del otro lado.
        if (hasPositive)
        {
            int positives = 0;
            int index = -1;

            for (int i = 0; i < 3; i++)
            {
                if (distances[i] > 0f)
                {
                    positives++;
                    index = i;
                }
            }

            if (positives == 1)
            {
                oppositeVertex = vertices[index];
                edgeVertex1 = vertices[(index + 1) % 3];
                edgeVertex2 = vertices[(index + 2) % 3];
                return true;
            }
        }

        if (hasNegative)
        {
            int negatives = 0;
            int index = -1;

            for (int i = 0; i < 3; i++)
            {
                if (distances[i] < 0f)
                {
                    negatives++;
                    index = i;
                }
            }

            if (negatives == 1)
            {
                oppositeVertex = vertices[index];
                edgeVertex1 = vertices[(index + 1) % 3];
                edgeVertex2 = vertices[(index + 2) % 3];
                return true;
            }
        }

        return false;
    }

    public static bool RayVsTriangle(Vector3 rayOrigin, Vector3 rayDirection, float maxDistance, TriangleReference triangle,
        out Vector3 hitPoint)
    {
        hitPoint = Vector3.zero;
        const float EPSILON = 0.00001f;

        Vector3 v0 = triangle.owner.CollisionPointToWorld(triangle.triangle.v1);
        Vector3 v1 = triangle.owner.CollisionPointToWorld(triangle.triangle.v2);
        Vector3 v2 = triangle.owner.CollisionPointToWorld(triangle.triangle.v3);

        Vector3 edge1 = v1 - v0;
        Vector3 edge2 = v2 - v0;

        Vector3 h = Vector3.Cross(rayDirection, edge2);
        float a = Vector3.Dot(edge1, h);

        if (Mathf.Abs(a) < EPSILON)
            return false;

        float f = 1f / a;

        Vector3 s = rayOrigin - v0;

        float u = f * Vector3.Dot(s, h);

        if (u < 0f || u > 1f)
            return false;

        Vector3 q = Vector3.Cross(s, edge1);

        float v = f * Vector3.Dot(rayDirection, q);

        if (v < 0f || u + v > 1f)
            return false;

        float t = f * Vector3.Dot(edge2, q);

        if (t < 0f || t > maxDistance)
            return false;

        hitPoint = rayOrigin + rayDirection * t;
        return true;
    }

    public static bool AABBIntersectsAABB(AABB a, AABB b)
    {
        Vector3 difference = a.center - b.center;

        return
            Mathf.Abs(difference.x) <= a.halfSize.x + b.halfSize.x &&
            Mathf.Abs(difference.y) <= a.halfSize.y + b.halfSize.y &&
            Mathf.Abs(difference.z) <= a.halfSize.z + b.halfSize.z;
    }

    public static Sphere GetMinimumTriangleSphere(Vector3 a, Vector3 b, Vector3 c)
    {
        float abSquared = (b - a).sqrMagnitude;
        float acSquared = (c - a).sqrMagnitude;
        float bcSquared = (c - b).sqrMagnitude;

        Vector3 edgeA;
        Vector3 edgeB;
        Vector3 thirdPoint;

        if (abSquared >= acSquared && abSquared >= bcSquared)
        {
            edgeA = a;
            edgeB = b;
            thirdPoint = c;
        }
        else if (acSquared >= abSquared && acSquared >= bcSquared)
        {
            edgeA = a;
            edgeB = c;
            thirdPoint = b;
        }
        else
        {
            edgeA = b;
            edgeB = c;
            thirdPoint = a;
        }

        Vector3 diameterCenter = (edgeA + edgeB) * 0.5f;
        float diameterRadius = Vector3.Distance(edgeA, edgeB) * 0.5f;

        if ((thirdPoint - diameterCenter).sqrMagnitude <= diameterRadius * diameterRadius + 0.000001f)
            return new Sphere(diameterCenter, diameterRadius);

        Vector3 ab = b - a;
        Vector3 ac = c - a;
        Vector3 normal = Vector3.Cross(ab, ac);

        float normalSquared = normal.sqrMagnitude;

        if (normalSquared <= 0.0000001f)
            return new Sphere(diameterCenter, diameterRadius);

        Vector3 circumcenter =
            a +
            (
                Vector3.Cross(ac, normal) * ab.sqrMagnitude +
                Vector3.Cross(normal, ab) * ac.sqrMagnitude
            ) /
            (2f * normalSquared);

        float circumradius = Vector3.Distance(circumcenter, a);

        return new Sphere(circumcenter, circumradius);
    }

    public static AABB MergeAABB(AABB a, AABB b)
    {
        if (a == null)
            return b;

        if (b == null)
            return a;

        Vector3 minimum = Vector3.Min(a.Min, b.Min);
        Vector3 maximum = Vector3.Max(a.Max, b.Max);

        return new AABB((minimum + maximum) * 0.5f, maximum - minimum);
    }

    public static Sphere MergeSpheres(Sphere a, Sphere b)
    {
        if (a.radius <= 0f)
            return b;

        if (b.radius <= 0f)
            return a;

        Vector3 difference = b.center - a.center;
        float distance = difference.magnitude;
        
        //Una esfera contiene completamente a la otra.  
        if (a.radius >= distance + b.radius)
            return a;

        if (b.radius >= distance + a.radius)
            return b;

        if (distance <= Mathf.Epsilon)
            return new Sphere(a.center, Mathf.Max(a.radius, b.radius));

        float mergedRadius = (distance + a.radius + b.radius) * 0.5f;
        Vector3 direction = difference / distance;
        Vector3 mergedCenter = a.center + direction * (mergedRadius - a.radius);

        return new Sphere(mergedCenter, mergedRadius);
    }
}
