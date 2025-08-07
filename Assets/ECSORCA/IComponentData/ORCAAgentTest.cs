using Unity.Entities;
using Unity.Mathematics;

public struct ORCAAgentTest : IComponentData
{
    public float3 Velocity;
    public float3 Target;
    public float Radius;
    public float MoveSpeed;
    public int   Priority; // 필요하면
}
