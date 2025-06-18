using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;

[DisableAutoCreation]
partial struct b : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        
    }

    public void OnUpdate(ref SystemState state)
    {
        Debug.Log("스폰 시작");
        
        var em = state.EntityManager;
        // 메쉬, 머테리얼 가져오기
        var query = SystemAPI.QueryBuilder().WithAll<GraphicData>().Build();
        var graphicSetting = query.GetSingleton<GraphicData>();

        // 렌더 매쉬 지정
        RenderMeshArray renderMeshArrayy = new RenderMeshArray(    graphicSetting.material.ToArray(),
            graphicSetting.mesh.ToArray());
        
        
        // 스폰 카운트 만큼 생성
        var instances = new NativeArray<Entity>(1, Allocator.Temp);
        var normalEntity = em.CreateEntity();
        em.Instantiate(normalEntity, instances);
        float3 spawnOffset = new float3(1, 0, 0);
        int arrayCount = 0;
        var offset = new float3(0,0,-20);
        
        foreach (var entity in instances)
        {
            float3 spawnPos = math.float3(0);
            int rowSize = 5; // 한 줄에 5개씩
            float3 rowOffset = new float3(0, 0, -2); // 줄 간 간격

            int row = arrayCount / rowSize;
            int col = arrayCount % rowSize;

            spawnPos = offset - (spawnOffset * col + rowOffset * row);
            SpawnEntity(em,entity,spawnPos,new float3(2f,0f,20f),renderMeshArrayy,graphicSetting,
                MaterialType.E_Normal,MeshType.E_Capsule);
            em.AddComponentData(entity, new UnitATag());
            arrayCount++;
        }
        
        
        // 스폰 카운트 만큼 생성
        var instances2 = new NativeArray<Entity>(1, Allocator.Temp);
        var normalEntity2 = em.CreateEntity();
        em.Instantiate(normalEntity2, instances2);
        float3 spawnOffset2 = new float3(1, 0, 0);
        int arrayCount2 = 0;
        var offset2 = new float3(0,0,20);

        foreach (var entity in instances2)
        {
            float3 spawnPos = math.float3(0);
            int rowSize = 5; // 한 줄에 5개씩
            float3 rowOffset = new float3(0, 0, 2); // 줄 간 간격

            int row = arrayCount2 / rowSize;
            int col = arrayCount2 % rowSize;

            spawnPos = offset2 - (spawnOffset2 * col + rowOffset * row);
            SpawnEntity(em,entity,spawnPos,new float3(2f,0f,-20f),renderMeshArrayy,graphicSetting,
                MaterialType.E_Normal2,MeshType.E_Capsule);
            em.AddComponentData(entity, new UnitBTag());
            arrayCount2++;
        }
        
        Debug.Log("스폰 끝");
        state.Enabled = false;
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        
    }
    
    public void SpawnEntity(EntityManager em, Entity entity, float3 pos, float3 target, 
        RenderMeshArray renderMeshArray, GraphicData graphicSetting,MaterialType materialType, MeshType meshType)
    {
        em.AddComponentData(entity, new Agent()
        {
            MoveSpeed = 3,
            Radius = 0.5f,
            Target = target
        });
        // 스폰
        em.AddComponentData(entity, LocalTransform.FromPosition(pos));
        em.AddComponentData(entity, new Velocity()
        {
            Value = float3.zero
        });
        // 렌더 설정
        var renderDesc = new RenderMeshDescription(
            shadowCastingMode: ShadowCastingMode.On,
            receiveShadows: true,
            layer: 0,
            renderingLayerMask: 1 << 1 // ECS 렌더 레이어 1번에 해당
        );
        // 바운드
        var bounds = new RenderBounds {Value = graphicSetting.mesh[0].bounds.ToAABB()};
        em.AddComponentData(entity, bounds);
        // 렌더 매쉬
        RenderMeshUtility.AddComponents(
            entity,
            em,
            renderDesc,
            renderMeshArray,
            MaterialMeshInfo.FromRenderMeshArrayIndices((int)materialType, (int)meshType)
        );
    }
}
