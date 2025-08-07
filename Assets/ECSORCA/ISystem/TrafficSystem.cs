using Unity.Burst;
using Unity.Entities;
using UnityEngine;

partial struct TrafficSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<TrafficData>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var dt = Time.deltaTime;
        foreach (var (traffic,entity) in SystemAPI.Query<RefRW<TrafficData>>().WithEntityAccess())
        {
            if (traffic.ValueRO.TrafficTimer > 0)
                traffic.ValueRW.TrafficTimer -= dt;
            else
            {
                traffic.ValueRW.TrafficTimer = traffic.ValueRO.TrafficMaxTime;
                traffic.ValueRW.IsWalkable = !traffic.ValueRO.IsWalkable;
            }
        }
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        
    }
}
