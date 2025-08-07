using Unity.Burst;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;


[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(ObstacleCheckSystem))]
[BurstCompile]
[DisableAutoCreation]
public partial struct NodeWeightDecaySystem : ISystem
{
    private DynamicBuffer<NodeGroup> NodeGroups;
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        float decayPerSecond = 2f;
        float deltaTime = SystemAPI.Time.DeltaTime;

        foreach (var (buffer, entity) in SystemAPI.Query<DynamicBuffer<NodeGroup>>()
                     .WithEntityAccess())
        {
            NodeGroups = buffer;
            break;
        }
        for (int i = 0; i < NodeGroups.Length; i++)
        {
            var elem = NodeGroups[i];
            var node = elem.Node;
            node.DynamicWeight = math.max(0f, node.DynamicWeight - decayPerSecond * deltaTime);
            elem.Node = node;
            NodeGroups[i] = elem;
            ecb.SetComponent(elem.Entity, node);
        }
        
        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}