public class TriangleReference
{
    public BaseCollisionObject owner;
    public Triangle triangle;
    public Sphere sphere;

    public TriangleReference(BaseCollisionObject owner, Triangle triangle, Sphere sphere)
    {
        this.owner = owner;
        this.triangle = triangle;
        this.sphere = sphere;
    }
}