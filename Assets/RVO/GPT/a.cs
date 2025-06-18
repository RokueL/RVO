using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;
[UpdateAfter(typeof(ORCAAgentSystem))]
partial struct a : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        
    }

    [BurstCompile]
    public partial struct VelocityMovementSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (velocity, transform, entity) in SystemAPI.Query<Velocity, RefRW<LocalTransform>>().WithEntityAccess())
            {
                transform.ValueRW.Position += velocity.Value * SystemAPI.Time.DeltaTime;
            }
        }
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        
    }
}
