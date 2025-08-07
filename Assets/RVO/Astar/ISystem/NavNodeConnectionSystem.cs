using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(ObstacleCheckSystem))]
public partial struct NavNodeConnectionSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        Debug.Log("그리드연결");
        
        var tagQuery = SystemAPI.QueryBuilder().WithAll<NodeGroupTag>().Build();
        var managerEntity = tagQuery.GetSingletonEntity();
        var buffer = SystemAPI.GetBuffer<NodeGroup>(managerEntity);
        

   

        foreach (var (npcData,transform,entity) in 
                 SystemAPI.Query<RefRO<NPCData>,RefRO<LocalTransform>>().WithEntityAccess())
        {
            
            int2 pos = new int2((int)transform.ValueRO.Position.x, (int)transform.ValueRO.Position.z);
            var node = buffer[Index(pos, 100)].Entity;
            var nodebuffer = SystemAPI.GetBuffer<NavNodeInnerEntity>(node);
            nodebuffer.Add(new NavNodeInnerEntity()
            {
                Entity = entity
            });
        }

        state.Enabled = false;
    }
    public static int Index(int2 pos, int2 gridSize) => pos.x * gridSize.x + pos.y;
}
