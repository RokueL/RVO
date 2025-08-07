using Unity.Entities;
using UnityEngine;

class SpawnCountBaker : MonoBehaviour
{
    public int SpawnCount;
}

class RVOBakerBaker : Baker<SpawnCountBaker>
{
    public override void Bake(SpawnCountBaker authoring)
    {
        var entity = GetEntity(TransformUsageFlags.None);
        AddComponent(entity,new SpawnCountData()
        {
            SpawnCount = authoring.SpawnCount
        });
    }
}

public struct SpawnCountData : IComponentData
{
    public int SpawnCount;
}
