public class TriangleReference
{
    public BaseCollisionObject owner;
    public Triangle triangle;
    public int triangleIndex;

    public Sphere sphere;
    public AABB bounds;

    public int lastUpdatedStep;

    public TriangleReference(BaseCollisionObject owner, Triangle triangle, int triangleIndex)
    {
        this.owner = owner;
        this.triangle = triangle;
        this.triangleIndex = triangleIndex;
    }
}