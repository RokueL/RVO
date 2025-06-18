using Unity.Entities;
using UnityEngine;

class RVOBaker : MonoBehaviour
{
    public int SpawnCount;
}

class RVOBakerBaker : Baker<RVOBaker>
{
    public override void Bake(RVOBaker authoring)
    {
        var entity = GetEntity(TransformUsageFlags.None);
        AddComponent(entity,new RVOData()
        {
            SpawnCount = authoring.SpawnCount
        });
    }
}

public struct RVOData : IComponentData
{
    public int SpawnCount;
}
