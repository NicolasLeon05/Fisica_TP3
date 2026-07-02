using UnityEngine;

public class TriangleReference
{
    public BaseCollisionObject owner;
    public Triangle triangle;
    public Sphere sphere;

    public Vector3 worldV1;
    public Vector3 worldV2;
    public Vector3 worldV3;
    public Vector3 normal;

    public TriangleReference(BaseCollisionObject owner, Triangle triangle, Sphere sphere)
    {
        this.owner = owner;
        this.triangle = triangle;
        this.sphere = sphere;

        UpdateWorldData();
    }

    public void UpdateSphere()
    {
        sphere.center = owner.transform.TransformPoint(triangle.localBoundingSphere.center);

        float scale = Mathf.Max(
            owner.transform.lossyScale.x,
            Mathf.Max(
                owner.transform.lossyScale.y,
                owner.transform.lossyScale.z));

        sphere.radius = triangle.localBoundingSphere.radius * scale;
    }

    public void UpdateWorldData()
    {
        worldV1 = owner.transform.TransformPoint(triangle.v1);
        worldV2 = owner.transform.TransformPoint(triangle.v2);
        worldV3 = owner.transform.TransformPoint(triangle.v3);

        normal = Vector3.Cross(worldV2 - worldV1, worldV3 - worldV1).normalized;
    }
}