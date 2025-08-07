using Unity.Entities;
using Unity.Mathematics;

public struct ORCAAgent : IComponentData
{
    public float3 TargetPosition;
    public float3 Velocity;
    public float MoveSpeed;
    public float Radius;
    public int Priority;
    public bool IsCrossingTraffic;

    public NPCState E_State;
    public int CurrentPathIndex;
}
