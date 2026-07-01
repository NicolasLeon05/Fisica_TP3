public class SphereVolume : CollisionVolume
{
    public Sphere Sphere;

    public SphereVolume(Sphere sphere)
    {
        Sphere = sphere;
    }
}