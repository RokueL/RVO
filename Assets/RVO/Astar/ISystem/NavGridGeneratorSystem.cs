using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(UnitSpawn))]
public partial struct NavGridGeneratorSystem : ISystem
{
    public void OnCreate(ref SystemState state) { }

    public void OnUpdate(ref SystemState state)
    {
        
        Debug.Log("그리드");
        
        const int gridSizeX = 300;
        const int gridSizeY = 300;
        const float spacing = 1.0f;

        var ecb = new EntityCommandBuffer(Allocator.Temp);

        for (int x = -gridSizeX/2; x < gridSizeX/2; x++)
        {
            for (int y = -gridSizeY/2; y < gridSizeY/2; y++)
            {
                Entity node = ecb.CreateEntity();
                NavNode nodeData = new NavNode()
                {
                    Entity = node,
                    GridPos = new int2(x, y),
                    WorldPos = new float3(x * spacing, 0, y * spacing),
                    E_RoadType = E_RoadType.None, // 기본적으로 모두 걷기 가능
                };
                ecb.AddComponent(node, nodeData);
                ecb.AddBuffer<NavNodeInnerEntity>(node);
            }
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();

        state.Enabled = false; // 1회성 실행
    }
}

