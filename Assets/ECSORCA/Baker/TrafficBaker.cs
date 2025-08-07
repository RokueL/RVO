using Unity.Entities;
using UnityEngine;

class TrafficBaker : MonoBehaviour
{
    public float TrafficMaxTime;
}

class TrafficBakerBaker : Baker<TrafficBaker>
{
    public override void Bake(TrafficBaker authoring)
    {
        var entity = GetEntity(TransformUsageFlags.None);
        
        AddComponent(entity, new TrafficData()
        {
            TrafficMaxTime = authoring.TrafficMaxTime,
            TrafficTimer = authoring.TrafficMaxTime
        });
    }
}

public struct TrafficData : IComponentData
{
    public float TrafficMaxTime;
    public float TrafficTimer;
    public bool IsWalkable;
}
