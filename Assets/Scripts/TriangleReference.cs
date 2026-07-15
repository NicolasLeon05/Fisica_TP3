public class TriangleReference
{
    public BaseCollisionObject owner;
    public Triangle triangle;
    public Sphere sphere;
    public int triangleIndex;

    public int lastUpdatedStep = -1;

    public TriangleReference(BaseCollisionObject owner, Triangle triangle, int triangleIndex)
    {
        this.owner = owner;
        this.triangle = triangle;
        this.triangleIndex = triangleIndex;
    }
}