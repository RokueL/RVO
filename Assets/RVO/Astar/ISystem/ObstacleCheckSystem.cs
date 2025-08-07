using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[Flags]
public enum E_RoadType : Byte
{
    None = 0,
    Collider = 1 << 0,
    Traffic = 1 << 1,
}

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(NavGridGeneratorSystem))]
public partial struct ObstacleCheckSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
    }

    //[BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        Debug.Log("콜라이더 체크");
        var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld;
        //state.Enabled = false;
        var nodeQuery = SystemAPI.QueryBuilder().WithAll<NavNode>().Build();
        var nodes = nodeQuery.ToEntityArray(Allocator.Temp);
        var nodeLookup = SystemAPI.GetComponentLookup<NavNode>(true);
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        // 관리용 엔티티 생성 (단 1개)
        var managerEntity = ecb.CreateEntity();
        ecb.AddComponent<NodeGroupTag>(managerEntity);
        var navNodeBuffer = ecb.AddBuffer<NodeGroup>(managerEntity);

        foreach (var entity in nodes)
        {
            var node = nodeLookup[entity];
            NavNode WriteNode = node;

            float3 worldPos = new float3(node.WorldPos.x, 0f, node.WorldPos.z);
            float3 halfSize = new float3(0.3f, 0, 0.3f);


            WriteNode.E_RoadType = CheckObstacle(worldPos, halfSize, physicsWorld);

            ecb.SetComponent(entity, WriteNode);


            navNodeBuffer.Add(new NodeGroup()
            {
                Entity = entity,
                Node = WriteNode
            });
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
        state.Enabled = false;
    }

    public E_RoadType CheckObstacle(float3 worldPos, float3 halfSize, PhysicsWorld physicsWorld)
    {
        E_RoadType roadType = E_RoadType.None;
        foreach (E_RoadType type in Enum.GetValues(typeof(E_RoadType)))
        {
            CollisionFilter filter = CollisionFilter.Default;
            switch (type)
            {
                case E_RoadType.None:
                {
                    filter = new CollisionFilter
                    {
                        BelongsTo = 0, // 나는 플레이어 레이어에 속하고
                        CollidesWith = (uint)E_RoadType.None, // 구조물과만 충돌하고 싶다
                        GroupIndex = 0
                    };
                    break;
                }
                case E_RoadType.Collider:
                    filter = new CollisionFilter
                    {
                        BelongsTo = (uint)E_RoadType.Collider, // 나는 플레이어 레이어에 속하고
                        CollidesWith = (uint)E_RoadType.Collider, // 구조물과만 충돌하고 싶다
                        GroupIndex = 0
                    };
                    break;
                case E_RoadType.Traffic:
                    filter = new CollisionFilter
                    {
                        BelongsTo = (uint)E_RoadType.Traffic, // 나는 플레이어 레이어에 속하고
                        CollidesWith = (uint)E_RoadType.Traffic, // 구조물과만 충돌하고 싶다
                        GroupIndex = 0
                    };
                    break;
            }

            var input = new OverlapAabbInput
            {
                Aabb = new Aabb
                {
                    Min = worldPos - halfSize,
                    Max = worldPos + halfSize
                },
                Filter = filter
            };

            NativeList<int> dummy = new(Allocator.Temp);
            bool hasHit = physicsWorld.OverlapAabb(input, ref dummy);
            if (hasHit)
            {
                roadType = type;
                break;
            }
        }

        return roadType;
    }
}