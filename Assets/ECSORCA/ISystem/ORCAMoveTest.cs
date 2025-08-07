using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[DisableAutoCreation]
[BurstCompile]
public partial struct ORCAAgentMoveSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        var agentQuery = SystemAPI.QueryBuilder()
            .WithAll<ORCAAgent, LocalTransform>()
            .Build();
        int agentCount = agentQuery.CalculateEntityCount();
        if (agentCount == 0) return;

        var agents = agentQuery.ToComponentDataArray<ORCAAgent>(Allocator.TempJob);
        var transforms = agentQuery.ToComponentDataArray<LocalTransform>(Allocator.TempJob);

        var nextVelocities = new NativeArray<float3>(agentCount, Allocator.TempJob);
        

        foreach (var (agent,transform,entity) in SystemAPI.Query<RefRW<ORCAAgent>,RefRW<LocalTransform>>().WithEntityAccess())
        {
            var job = new ORCAMoveJob
            {
                Agent = agent.ValueRO,
                Transform = transform.ValueRO,
                Agents = agents,
                Transforms = transforms,
                NextVelocities = nextVelocities,
                Tau = 2.0f,
                OrcaMix = 0.5f,
                SeparationDistance = 1.5f,
                SeparationStrength = 2.0f
            };
        
            state.Dependency = job.Schedule();
            state.Dependency.Complete();
            
            agent.ValueRW.Velocity = nextVelocities[0];
            transform.ValueRW.Position += nextVelocities[0] * SystemAPI.Time.DeltaTime;
        }
        
        
        // var job = new ORCAMoveJob
        // {
        //     Agents = agents,
        //     Transforms = transforms,
        //     NextVelocities = nextVelocities,
        //     Tau = 2.0f,
        //     OrcaMix = 0.5f,
        //     SeparationDistance = 1.5f,
        //     SeparationStrength = 2.0f
        // };
        //
        // state.Dependency = job.Schedule(agentCount, 128, state.Dependency);
        // state.Dependency.Complete();
        //
        // // 결과 적용
        // var entityArray = agentQuery.ToEntityArray(Allocator.TempJob);
        // for (int i = 0; i < agentCount; i++)
        // {
        //     var e = entityArray[i];
        //     var agentRW = SystemAPI.GetComponentRW<ORCAAgent>(e);
        //     var trRW = SystemAPI.GetComponentRW<LocalTransform>(e);
        //
        //     agentRW.ValueRW.Velocity = nextVelocities[i];
        //     trRW.ValueRW.Position += nextVelocities[i] * SystemAPI.Time.DeltaTime;
        // }

        agents.Dispose();
        transforms.Dispose();
        nextVelocities.Dispose();
        //entityArray.Dispose();
    }

    [BurstCompile]
    public struct ORCAMoveJob : IJob
    {
        [ReadOnly] public ORCAAgent Agent;
        [ReadOnly] public LocalTransform Transform;
        
        [ReadOnly] public NativeArray<ORCAAgent> Agents;
        [ReadOnly] public NativeArray<LocalTransform> Transforms;
        public NativeArray<float3> NextVelocities;

        public float Tau, OrcaMix, SeparationDistance, SeparationStrength;

        public void Execute()
        {
            
            float3 vRVO = ComputeRVO(Agent, Transform, Agents, Transforms);
            float3 vORCA = ComputeORCA(Agent, Transform, Agents, Transforms, Tau, vRVO);
            float3 vHybrid = math.lerp(vRVO, vORCA, OrcaMix);
            float3 vFinal = ComputeSeparation(Agent, Transform, Agents, Transforms, vHybrid, SeparationDistance, SeparationStrength);

            NextVelocities[0] = vFinal;
        }
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
    }

    public static int Index(int2 pos, int2 gridSize) => (pos.x + gridSize.x / 2) * gridSize.x + (pos.y + gridSize.y / 2);



// 🟩 Linear Program: ORCA half-plane 제약 내 목표점과 가장 가까운 점 찾기
    public static class LinearProgram
    {
        // ORCA 제약선 내에서 vPref에 가장 가까운 점
        public static float3 Solve(
            NativeArray<ORCALine> lines, int lineCount, float3 vPref, float maxSpeed)
        {
            float3 result = math.length(vPref) > maxSpeed ? math.normalizesafe(vPref) * maxSpeed : vPref;
            for (int i = 0; i < lineCount; i++)
                if (!lines[i].IsInside(result))
                    result = lines[i].Project(result);

            if (math.length(result) > maxSpeed)
                result = math.normalizesafe(result) * maxSpeed;
            return result;
        }
    }
    
    public static float3 Solve(NativeList<ORCALine> halfPlanes, float3 vPref, float maxSpeed)
    {
        // 속도 제한 내에서 시작
        float3 result = math.length(vPref) > maxSpeed ? math.length(vPref) * maxSpeed : vPref;

        // 한 제약씩 검사하면서 안쪽에 있도록 보정
        foreach (var hp in halfPlanes)
        {
            if (!hp.IsInside(result))
            {
                result = hp.Project(result);
            }
        }

        // 속도 제한 한 번 더 보정
        if (math.length(result) > maxSpeed)
            result = math.normalizesafe(result) * maxSpeed;

        halfPlanes.Dispose();
        return result;
    }
    
    public static float3 ComputeRVO(
        ORCAAgent agent, LocalTransform agentTr,
        NativeArray<ORCAAgent> allAgents, NativeArray<LocalTransform> allTr)
    {
        float3 posA = agentTr.Position;
        float3 velocityA = agent.Velocity;
        float3 vPref = math.normalizesafe(agent.TargetPosition - posA) * agent.MoveSpeed;
        float3 vOpt = vPref;

        for (int i = 0; i < allAgents.Length; i++)
        {
            if (allTr[i].Position.Equals(posA)) continue; // 자기 자신 스킵
            var other = allAgents[i];
            var otherTr = allTr[i];
            float3 posB = otherTr.Position;
            float3 velocityB = other.Velocity;

            float3 relPos = posB - posA;
            float dist = math.length(relPos);
            float combinedRadius = agent.Radius + other.Radius;
            if (dist < 0.001f || dist < combinedRadius) continue;

            float3 apex = (velocityA + velocityB) * 0.5f;
            float3 dir = math.normalizesafe(relPos);
            float theta = math.asin(math.clamp(combinedRadius / dist, -1f, 1f));
            float3 vRel = vOpt - apex;
            float cosAngle = math.dot(math.normalizesafe(vRel), dir);
            float angle = math.acos(math.clamp(cosAngle, -1f, 1f));
            if (angle < theta)
            {
                float len = math.length(vRel);
                float3 left = math.mul(quaternion.AxisAngle(math.up(), -theta), dir);
                float3 right = math.mul(quaternion.AxisAngle(math.up(), theta), dir);
                float sign = math.sign(math.dot(math.cross(dir, vRel), math.up()));
                float3 boundaryDir = sign < 0 ? left : right;
                vOpt = apex + boundaryDir * len;
            }
        }
        return vOpt;
    }

    // ORCA 계산
    public static float3 ComputeORCA(
        ORCAAgent agent, LocalTransform agentTr,
        NativeArray<ORCAAgent> allAgents, NativeArray<LocalTransform> allTr,
        float tau, float3 vOpt)
    {
        float3 posA = agentTr.Position;
        float3 velocityA = agent.Velocity;
        float maxSpeed = agent.MoveSpeed;

        var orcaLines = new NativeArray<ORCALine>(allAgents.Length, Allocator.Temp);
        int lineCount = 0;

        for (int i = 0; i < allAgents.Length; i++)
        {
            if (allTr[i].Position.Equals(posA)) continue; // 자기 자신 스킵
            var other = allAgents[i];
            var otherTr = allTr[i];
            float3 posB = otherTr.Position;
            float3 velocityB = other.Velocity;
            float combinedRadius = agent.Radius + other.Radius;
            float3 relPos = posB - posA;
            float3 relVel = velocityA - velocityB;
            float dist = math.length(relPos);

            float3 u, n;
            if (dist < combinedRadius)
            {
                n = math.normalizesafe(relPos);
                u = (combinedRadius - dist) / tau * n;
            }
            else
            {
                float3 dir = math.normalizesafe(relPos);
                float theta = math.asin(math.clamp(combinedRadius / dist, -1f, 1f));
                float3 relVelProj = relVel - dir * math.dot(relVel, dir);
                n = math.lengthsq(relVelProj) > 0.001f ? math.normalizesafe(relVelProj) : dir;
                u = (combinedRadius / tau - math.length(relVelProj)) * n;
            }

            float3 planePoint = velocityA + 0.5f * u;
            orcaLines[lineCount++] = new ORCALine { point = planePoint, normal = n };
        }

        var result = LinearProgram.Solve(orcaLines, lineCount, vOpt, maxSpeed);
        orcaLines.Dispose();
        return result;
    }

    // Separation force
    public static float3 ComputeSeparation(
        ORCAAgent agent, LocalTransform agentTr,
        NativeArray<ORCAAgent> allAgents, NativeArray<LocalTransform> allTr,
        float3 orcaVec, float separationDistance, float separationStrength)
    {
        float3 posA = agentTr.Position;
        float3 separation = float3.zero;
        for (int i = 0; i < allAgents.Length; i++)
        {
            if (allTr[i].Position.Equals(posA)) continue;
            var otherTr = allTr[i];
            float3 diff = posA - otherTr.Position;
            float dist = math.length(diff);
            if (dist > 0.01f && dist < separationDistance)
                separation += math.normalizesafe(diff) * (separationDistance - dist) * separationStrength;
        }
        float3 vFinal = orcaVec + separation;
        if (math.length(vFinal) > agent.MoveSpeed)
            vFinal = math.normalizesafe(vFinal) * agent.MoveSpeed;
        return vFinal;
    }
    

    public struct ORCALine
    {
        public float3 point;   // half-plane을 만드는 점
        public float3 normal;  // half-plane의 법선

        public bool IsInside(float3 v) => math.dot(v - point, normal) >= 0f;

        public float3 Project(float3 v)
        {
            float d = math.dot(v - point, normal);
            return d >= 0 ? v : v - d * normal;
        }
    }

}

