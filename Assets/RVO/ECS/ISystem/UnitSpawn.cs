using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Graphics;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;
using Random = Unity.Mathematics.Random;

public enum MeshType : byte
{
    E_Capsule = 0,
    E_Cube = 1 << 0,
    E_Circle = 1 << 1,
    
}

public enum MaterialType : byte
{
    E_Normal = 0,
    E_Normal2 = 1 << 0,
    E_Normal3 = 1 << 1,
    
}
//[DisableAutoCreation]
partial struct UnitSpawn : ISystem
{
    private SpawnCountData _spawnCountData;
    private MaterialType E_MaterialType;
    private DynamicBuffer<NavSpawnPoint> SpawnPoints;
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
        //생성 설정
        foreach (var (buffer, entity) in SystemAPI.Query<RefRO<SpawnCountData>>()
                     .WithEntityAccess())
        {
            _spawnCountData = buffer.ValueRO;
            break;
        }
        
        // 렌더 매쉬 지정
        RenderMeshArray renderMeshArrayy = new RenderMeshArray(    graphicSetting.material.ToArray(),
            graphicSetting.mesh.ToArray());
        
        // 스폰 카운트 만큼 생성
        var instances = new NativeArray<Entity>(_spawnCountData.SpawnCount, Allocator.Temp);
        var normalEntity = em.CreateEntity();
        em.Instantiate(normalEntity, instances);
        int arrayCount = 0;
        
        
        foreach (var buffer in SystemAPI
                     .Query<DynamicBuffer<NavSpawnPoint>>())
        {
            SpawnPoints = buffer;
            break;
        }
        
        var spawnPositions = new NativeArray<float3>(SpawnPoints.Length, Allocator.Temp);
        for (int i = 0; i < SpawnPoints.Length; i++)
            spawnPositions[i] = SpawnPoints[i].Node.WorldPos;
        
        foreach (var entity in instances)
        {
            var time = (uint)(SystemAPI.Time.ElapsedTime * 1000);
            var seed = math.asuint(entity.Index * 73856093 ^ entity.Version * 19349663 ^ time);
            if (seed == 0) seed = 1; // 절대 0이 되면 안됨
            var rng = new Random(seed);
            int rngNum = rng.NextInt(0, spawnPositions.Length);
            
            float3 spawnPos = spawnPositions[rngNum];
        
            SpawnEntity(em,entity,spawnPos,renderMeshArrayy,graphicSetting,
                MaterialType.E_Normal,MeshType.E_Capsule,arrayCount);
        
            em.AddComponentData(entity, new ORCAAgent()
            {
                MoveSpeed = 3f,
                Radius = 0.5f,
                Priority =  entity.Index,
                E_State = NPCState.None,
            });
            em.AddBuffer<MoveNode>(entity);
            
            arrayCount++;
        }

        
        state.Enabled = false;
    }

    public void SpawnEntity(EntityManager em, Entity entity, float3 pos, 
        RenderMeshArray renderMeshArray, GraphicData graphicSetting,MaterialType materialType, MeshType meshType, int weight)
    {
        // 스폰
        em.AddComponentData(entity, LocalTransform.FromPosition(pos));
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

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        
    }
    

}

