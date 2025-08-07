using Unity.Entities;
using Unity.Mathematics;

public struct AgentData : IComponentData
{
    public float3 Position;
    public float3 Velocity;
    public float3 PreferredVelocity;
    public float Radius;
    public float MaxSpeed;
}


public struct AgentDebugData : IComponentData
{
    public float3 Forward;
    public float3 PreferredVelocity;
    public float3 FinalVelocity;
}