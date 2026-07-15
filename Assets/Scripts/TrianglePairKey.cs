using System;

public readonly struct TrianglePairKey : IEquatable<TrianglePairKey>
{
    private readonly int ownerA;
    private readonly int ownerB;
    private readonly int triangleA;
    private readonly int triangleB;

    public TrianglePairKey(TriangleReference first, TriangleReference second)
    {
        int firstOwnerId = first.owner.GetInstanceID();
        int secondOwnerId = second.owner.GetInstanceID();

        // Normalizar el orden para que A-B y B-A sean el mismo par.
        if (firstOwnerId < secondOwnerId)
        {
            ownerA = firstOwnerId;
            triangleA = first.triangleIndex;

            ownerB = secondOwnerId;
            triangleB = second.triangleIndex;
        }
        else
        {
            ownerA = secondOwnerId;
            triangleA = second.triangleIndex;

            ownerB = firstOwnerId;
            triangleB = first.triangleIndex;
        }
    }

    public bool Equals(TrianglePairKey other)
    {
        return ownerA == other.ownerA &&
               ownerB == other.ownerB &&
               triangleA == other.triangleA &&
               triangleB == other.triangleB;
    }

    public override bool Equals(object obj)
    {
        return obj is TrianglePairKey other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + ownerA;
            hash = hash * 31 + ownerB;
            hash = hash * 31 + triangleA;
            hash = hash * 31 + triangleB;
            return hash;
        }
    }
}