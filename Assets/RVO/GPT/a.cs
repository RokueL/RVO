using Unity.Burst;
using Unity.Collections;
using UnityEngine;
using Unity.Entities;
using Unity.Jobs;
using Unity.Transforms;
using Unity.Mathematics;

[DisableAutoCreation]
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class RVOUnifiedSystem : SystemBase
{
    EntityQuery _agentQuery;

    protected override void OnCreate()
    {
        _agentQuery = GetEntityQuery(ComponentType.ReadOnly<AgentData>());
    }

    protected override void OnUpdate()
    {
        float deltaTime = UnityEngine.Time.deltaTime;

        var agentArray = _agentQuery.ToComponentDataArray<AgentData>(Allocator.TempJob);
        var agentCount = agentArray.Length;

        var positions = new NativeArray<float3>(agentCount, Allocator.TempJob);
        var velocities = new NativeArray<float3>(agentCount, Allocator.TempJob);
        var ids = new NativeArray<int>(agentCount, Allocator.TempJob);

        for (int i = 0; i < agentCount; i++)
        {
            positions[i] = agentArray[i].Position;
            velocities[i] = agentArray[i].Velocity;
        }

        var positionsOut = new NativeArray<float3>(agentCount, Allocator.TempJob);
        var velocitiesOut = new NativeArray<float3>(agentCount, Allocator.TempJob);



        // 업데이트 + 디버그
        var entities = _agentQuery.ToEntityArray(Allocator.Temp);
        for (int i = 0; i < agentCount; i++)
        {
            var entity = entities[i];
            var agent = agentArray[i];

            agent.Position = positionsOut[i];
            agent.Velocity = velocitiesOut[i];

            EntityManager.SetComponentData(entity, agent);
        }

        positionsOut.Dispose();
        velocitiesOut.Dispose();

        agentArray.Dispose();
        positions.Dispose();
        velocities.Dispose();
        ids.Dispose();
        entities.Dispose();
    }

    void DrawDebug(float3 pos, float3 goal, float3 velocity)
    {
        Debug.DrawLine(pos, pos + goal, Color.blue);     // preferred
        Debug.DrawLine(pos, pos + velocity, Color.red);  // actual
        Debug.DrawRay(pos, Vector3.up * 0.1f, Color.green); // position marker
    }

    [BurstCompile]
    public partial struct ORVOJob : IJobEntity
    {
        [ReadOnly] public NativeArray<AgentData> AllNeighbors;

        public void Execute(ref AgentData agent)
        {
            NativeList<AgentData> nearby = new NativeList<AgentData>(Allocator.Temp);
            // 위치 기반 필터링
            for (int i = 0; i < AllNeighbors.Length; i++)
            {
                if (math.distancesq(AllNeighbors[i].Position, agent.Position) < 16f)
                    nearby.Add(AllNeighbors[i]);
            }

            agent.Velocity = ComputeNewVelocity(agent.Position, agent.Velocity, agent.PreferredVelocity, nearby, agent.Radius, agent.MaxSpeed);
        }
        
        float3 ComputeNewVelocity(float3 pos, float3 vel, float3 pref, NativeList<AgentData> neighbors, float radius, float maxSpeed)
        {
            float3 optimal = pref;

            foreach (var neighbor in neighbors)
            {
                float3 relPos = neighbor.Position - pos;
                float3 relVel = vel - neighbor.Velocity;
                float combinedRadius = radius + neighbor.Radius;
                float distSq = math.lengthsq(relPos);
                float combinedRadiusSq = combinedRadius * combinedRadius;

                // Too close, need to push away
                if (distSq < combinedRadiusSq)
                {
                    float3 away = math.normalize(pos - neighbor.Position);
                    optimal += away * 0.5f;
                    continue;
                }

                // Reciprocal VO 계산
                float3 relativePosition = neighbor.Position - pos;
                float3 relativeVelocity = vel - neighbor.Velocity;
                float distance = math.length(relativePosition);
                float3 unitRelPos = relativePosition / distance;

                float angle = math.asin(combinedRadius / distance); // 회피각
                float3 sideDir = math.cross(unitRelPos, new float3(0, 1, 0)); // y-up assumption
                float3 boundaryLeft = math.normalize(math.mul(quaternion.AxisAngle(sideDir, angle), unitRelPos));
                float3 boundaryRight = math.normalize(math.mul(quaternion.AxisAngle(sideDir, -angle), unitRelPos));

                // 이 VO를 피하는 속도 선택 필요 → 여기선 간단히 우회
                float3 avoidance = math.normalize(math.cross(unitRelPos, new float3(0, 1, 0)));
                optimal += avoidance * 0.3f;
            }

            // 최종 속도 정규화
            if (math.lengthsq(optimal) > maxSpeed * maxSpeed)
                optimal = math.normalize(optimal) * maxSpeed;

            return optimal;
        }

    }

}
