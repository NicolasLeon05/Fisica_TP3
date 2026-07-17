using System;

public readonly struct ObjectPairKey : IEquatable<ObjectPairKey>
{
    private readonly int objectAId;
    private readonly int objectBId;

    public ObjectPairKey(BaseCollisionObject objectA, BaseCollisionObject objectB)
    {
        int idA = objectA.GetInstanceID();
        int idB = objectB.GetInstanceID();

        if (idA < idB)
        {
            objectAId = idA;
            objectBId = idB;
        }
        else
        {
            objectAId = idB;
            objectBId = idA;
        }
    }

    public bool Equals(ObjectPairKey other)
    {
        return objectAId == other.objectAId &&
               objectBId == other.objectBId;
    }

    public override bool Equals(object obj)
    {
        return obj is ObjectPairKey other && Equals(other);
    }
}