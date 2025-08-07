using Unity.Entities;
using Unity.Mathematics;

public struct Unit : IComponentData
{
    public float3 Velocity;
    public float3 MoveSpeed;
    public float3 Target;
    public float neighborDist;
    public float radius;
    public float3 LastVelocity;
    public int Weight;
}

public struct UnitATag : IComponentData{}
public struct UnitBTag : IComponentData{}
