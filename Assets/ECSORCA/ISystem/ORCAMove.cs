using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.SocialPlatforms;
using Random = Unity.Mathematics.Random;

//[DisableAutoCreation]
partial struct ORCAMove : ISystem
{
    private DynamicBuffer<NavDestination> Destinations;
    private DynamicBuffer<NodeGroup> NodeGroups;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float dt = UnityEngine.Time.deltaTime;
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        // Query에서 가져온 Entity가 있다고 가정
        foreach (var (buffer, entity) in SystemAPI.Query<DynamicBuffer<NodeGroup>>()
                     .WithEntityAccess())
        {
            NodeGroups = buffer;
            break;
        }
        var allTraffic = new NativeList<TrafficData>(Allocator.Temp); // 안전하게 snapshot!
        var allTrafficPosition = new NativeList<LocalTransform>(Allocator.Temp); // 안전하게 snapshot!

        foreach (var (trafficData,trfficPosition) in SystemAPI.Query<RefRO<TrafficData>,RefRO<LocalTransform>>())
        {
            allTraffic.Add(trafficData.ValueRO);
            allTrafficPosition.Add(trfficPosition.ValueRO);
        }

        // 전체 유닛 가져오기
        // 전체 유닛 데이터 캐싱

        foreach (var (myunit, mylocalTransform, myentity) in SystemAPI.Query<RefRW<ORCAAgent>, RefRW<LocalTransform>>().WithEntityAccess())
        {
            switch (myunit.ValueRO.E_State)
            {
                case NPCState.None:
                    foreach (var buffer in SystemAPI
                                 .Query<DynamicBuffer<NavDestination>>())
                    {
                        Destinations = buffer;
                        break;
                    }

                    var time = (uint)(SystemAPI.Time.ElapsedTime * 1000);
                    var seed = math.asuint(myentity.Index * 73856093 ^ myentity.Version * 19349663 ^ time);
                    if (seed == 0) seed = 1; // 절대 0이 되면 안됨
                    var rng = new Random(seed);
                    int rngNum = rng.NextInt(0, Destinations.Length);
                    var clearBuffer = SystemAPI.GetBuffer<MoveNode>(myentity);
                    clearBuffer.Clear();

                    int2 transformint2 = new int2((int)mylocalTransform.ValueRO.Position.x, (int)mylocalTransform.ValueRO.Position.z);
                    int2 endPostint2 = new int2((int)Destinations[rngNum].Node.WorldPos.x, (int)Destinations[rngNum].Node.WorldPos.z);
                    while (math.distance(transformint2,endPostint2) < 4f)
                    {
                        rngNum = rng.NextInt(0, Destinations.Length);
                        endPostint2 = new int2((int)Destinations[rngNum].Node.WorldPos.x, (int)Destinations[rngNum].Node.WorldPos.z);
                    }

                    ecb.AddComponent(myentity, new PathRequest()
                    {
                        Start = new int2((int)mylocalTransform.ValueRO.Position.x, (int)mylocalTransform.ValueRO.Position.z),
                        End = new int2((int)Destinations[rngNum].Node.WorldPos.x, (int)Destinations[rngNum].Node.WorldPos.z)
                    });
                    //Debug.Log($"{new int2((int)transform.ValueRO.Position.x, (int)transform.ValueRO.Position.z)}  {new int2((int)Destinations[rngNum].Node.WorldPos.x, (int)Destinations[rngNum].Node.WorldPos.z)}");
                    ORCAAgent data = myunit.ValueRO;
                    data.E_State = NPCState.None;
                    ecb.SetComponent(myentity, data);


                    // 노드에 유닛버퍼 목록에 추가 및 삭제
                    var spawnIndex = Index(new int2((int)mylocalTransform.ValueRO.Position.x, (int)mylocalTransform.ValueRO.Position.z), 300);
                    var spawnNode = NodeGroups[spawnIndex].Entity;
                    var spawnInnerBuffer = SystemAPI.GetBuffer<NavNodeInnerEntity>(spawnNode);
                    bool isHave = false;

                    for (int i = 0; i < spawnInnerBuffer.Length; i++)
                    {
                        if (spawnInnerBuffer[i].Entity == myentity)
                            isHave = true;
                    }

                    if (!isHave)
                    {
                        spawnInnerBuffer.Add(new NavNodeInnerEntity() { Entity = myentity });
                    }


                    break;

                case NPCState.Move:
                    // 이동 루트 버퍼 가져오기
                    var pathBuffer = SystemAPI.GetBuffer<MoveNode>(myentity);
                    if (pathBuffer.Length == 0)
                        return;
                    // 도착했을 때 처리
                    if (myunit.ValueRO.CurrentPathIndex > pathBuffer.Length && math.distance(mylocalTransform.ValueRO.Position, myunit.ValueRO.TargetPosition) < 1f)
                    {
                        var exindex = Index(new int2((int)mylocalTransform.ValueRO.Position.x, (int)mylocalTransform.ValueRO.Position.z), 300);
                        var exnode = NodeGroups[exindex].Entity;
                        var exInnerBuffer = SystemAPI.GetBuffer<NavNodeInnerEntity>(exnode);
                    
                        for (int i = 0; i < exInnerBuffer.Length; i++)
                        {
                            if (exInnerBuffer[i].Entity.Equals(myentity))
                            {
                                exInnerBuffer.RemoveAt(i);
                            }
                        }
                        pathBuffer.Clear();
                        
                        myunit.ValueRW.E_State = NPCState.None;
                        continue; // 도착 완료
                    }

                    // 주변 유닛들 가져오기
                    var units = new NativeList<ORCAAgent>(Allocator.TempJob);
                    var transforms = new NativeList<LocalTransform>(Allocator.TempJob);
                    var entities = new NativeList<Entity>(Allocator.TempJob);
                    var resultVelocities = new NativeArray<float3>(1, Allocator.TempJob);

                    // foreach (var (unit, transform, entity) in SystemAPI.Query<RefRO<ORCAAgent>, RefRO<LocalTransform>>().WithEntityAccess())
                    // {
                    //     // 내 자신(myentity)도 포함되니, 필요하다면 continue
                    //     if (entity == myentity) continue;
                    //     units.Add(unit.ValueRO);
                    //     transforms.Add(transform.ValueRO);
                    //     entities.Add(entity);
                    // }
                    bool isCrossingLine = false;
                    // 2 그리드 반경 내의 유닛 가져오기
                    int neighborDistance = 2;
                    for (int x = -neighborDistance; x <= neighborDistance; x++)
                    {
                        for (int y = -neighborDistance; y <= neighborDistance; y++)
                        {
                            int2 myPos = new int2((int)mylocalTransform.ValueRO.Position.x, (int)mylocalTransform.ValueRO.Position.z);
                            int2 otherPos = myPos + new int2(x, y);
                            if (otherPos.x >= -150 && otherPos.x < 150 && otherPos.y >= -150 && otherPos.y < 150)
                            {
                                var navnode = NodeGroups[Index(otherPos, 300)].Entity;
                                var innerBuffer = SystemAPI.GetBuffer<NavNodeInnerEntity>(navnode);
                                foreach (var npc in innerBuffer)
                                {
                                    if (myentity == npc.Entity) continue;
                                    var neighborNpcData = state.EntityManager.GetComponentData<ORCAAgent>(npc.Entity);
                                    var neighborTr = state.EntityManager.GetComponentData<LocalTransform>(npc.Entity);
                                    entities.Add(npc.Entity);
                                    units.Add(neighborNpcData);
                                    transforms.Add(neighborTr);
                                }
                            }

                            if (NodeGroups[Index(otherPos, new int2(300, 300))].Node.E_RoadType == E_RoadType.Traffic)
                                isCrossingLine = true;
                        }
                    }

                    if (!isCrossingLine)
                        myunit.ValueRW.IsCrossingTraffic = false;

                    // 내 원래 포지션
                    var originPos = mylocalTransform.ValueRO.Position;
                    // ORCA 이동 로직 Job 실행
                    if (units.Length > 0)
                    {
                        // 회피
                        // 회피
                        var job = new ORCAMoveJob()
                        {
                            Agent = myunit.ValueRO,
                            Transform = mylocalTransform.ValueRO,
                            Agents = units,
                            Transforms = transforms,
                            NextVelocities = resultVelocities,
                            Tau = 2.0f,
                            OrcaMix = 0.5f,
                            SeparationDistance = 1.25f,
                            SeparationStrength = 2f
                        };


                        var handle = job.Schedule();
                        handle.Complete();
                        myunit.ValueRW.Velocity = resultVelocities[0];
                        mylocalTransform.ValueRW.Position += resultVelocities[0] * dt;
                    }
                    else
                    {
                        mylocalTransform.ValueRW.Position += math.normalize(myunit.ValueRO.TargetPosition - mylocalTransform.ValueRO.Position) * myunit.ValueRO.MoveSpeed * dt;
                    }

                    if (math.distance(mylocalTransform.ValueRO.Position, myunit.ValueRO.TargetPosition) < 1f)
                    {
                        int pathIndex = myunit.ValueRW.CurrentPathIndex-1;
                        if (pathIndex > 0)
                        {
                            int idx = Index(new int2((int)pathBuffer[pathIndex].WorldPos.x, (int)pathBuffer[pathIndex].WorldPos.z), new int2(300, 300)); // 경로에 해당하는 실제 노드 인덱스!
                            var elem = NodeGroups[idx];
                            var node = elem.Node;
                            node.DynamicWeight = math.max(0, node.DynamicWeight - 5); // 음수 방지
                            elem.Node = node;
                            NodeGroups[idx] = elem; // 꼭 같은 idx에 넣어야 함!
                            ecb.SetComponent(elem.Entity, node);
                        }


                        myunit.ValueRW.CurrentPathIndex++;
                        if (myunit.ValueRO.CurrentPathIndex < pathBuffer.Length)
                            myunit.ValueRW.TargetPosition = pathBuffer[myunit.ValueRO.CurrentPathIndex].WorldPos;
                    }

                    // 노드에 유닛버퍼 목록에 추가 및 삭제
                    var myindex = Index(new int2((int)originPos.x, (int)originPos.z), 300);
                    var newPosIndex = Index(new int2((int)mylocalTransform.ValueRO.Position.x, (int)mylocalTransform.ValueRO.Position.z), 300);
                    if (myindex != newPosIndex)
                    {
                        var exnode = NodeGroups[myindex].Entity;
                        var exInnerBuffer = SystemAPI.GetBuffer<NavNodeInnerEntity>(exnode);
                        var newnode = NodeGroups[newPosIndex].Entity;
                        var newInnerBuffer = SystemAPI.GetBuffer<NavNodeInnerEntity>(newnode);

                        for (int i = 0; i < exInnerBuffer.Length; i++)
                        {
                            if (exInnerBuffer[i].Entity.Equals(myentity))
                            {
                                exInnerBuffer.RemoveAt(i);
                            }
                        }

                        newInnerBuffer.Add(new NavNodeInnerEntity() { Entity = myentity });

                        // 교통 신호 대기 처리
                        if (NodeGroups[newPosIndex].Node.E_RoadType == E_RoadType.Traffic)
                        {
                            float dist = int.MaxValue;
                            TrafficData tData = new TrafficData();
                            for (int i = 0; i < allTrafficPosition.Length; i++)
                            {
                                if (math.distance(mylocalTransform.ValueRO.Position, allTrafficPosition[i].Position) < dist)
                                {
                                    tData = allTraffic[i];
                                }
                            }
                            
                            if (!tData.IsWalkable || (tData.IsWalkable && tData.TrafficTimer < 6f && !myunit.ValueRO.IsCrossingTraffic))
                            {
                                myunit.ValueRW.E_State = NPCState.WaitTraffic;
                            }
                            else
                            {
                                myunit.ValueRW.IsCrossingTraffic = true;
                            }
                        }

                    }

                    // 메모리 해제
                    units.Dispose();
                    transforms.Dispose();
                    entities.Dispose();
                    resultVelocities.Dispose();
                    break;
                case NPCState.WaitTraffic:
                {
                    // 내 원래 포지션
                    var originPosT = mylocalTransform.ValueRO.Position;
                    var myindexT = Index(new int2((int)originPosT.x, (int)originPosT.z), 300);
                    // 교통 신호 대기 처리
                    float dist = int.MaxValue;
                    TrafficData tData = new TrafficData();
                    for (int i = 0; i < allTrafficPosition.Length; i++)
                    {
                        if (math.distance(mylocalTransform.ValueRO.Position, allTrafficPosition[i].Position) < dist)
                            tData = allTraffic[i];
                    }

                    bool isReadyToCross = tData.IsWalkable && tData.TrafficTimer > 6f;
                    bool isCrossing = myunit.ValueRO.IsCrossingTraffic;

                    // (1) 아직 건너는 중이 아니고, 출발 조건이 되면 true로!
                    if (!isCrossing && isReadyToCross)
                    {
                        myunit.ValueRW.IsCrossingTraffic = true;
                        myunit.ValueRW.E_State = NPCState.Move;
                    }
                    // (2) 이미 건너는 중일 때 (즉, 도로 위에 있으면 신호만 파란불이면 출발)
                    else if (isCrossing)
                    {
                        myunit.ValueRW.E_State = NPCState.Move;
                    }
                    break;
                }
            }
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
        allTraffic.Dispose();
        allTrafficPosition.Dispose();
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
            if (allAgents[i].Priority == agent.Priority) continue; // 자기 자신 스킵
            var other = allAgents[i];
            var otherTr = allTr[i];
            float3 posB = otherTr.Position;
            float3 velocityB = other.Velocity;

            float3 relPos = posB - posA;
            float dist = math.length(relPos * 2f);
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
        bool isTrafficStop = false;
        float3 posA = agentTr.Position;
        float3 velocityA = agent.Velocity;
        float maxSpeed = agent.MoveSpeed;

        var orcaLines = new NativeArray<ORCALine>(allAgents.Length, Allocator.Temp);
        int lineCount = 0;

        for (int i = 0; i < allAgents.Length; i++)
        {
            if (allAgents[i].Priority == agent.Priority) continue; // 자기 자신 스킵
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
            if (math.distance(posA, posB) < 0.5f && allAgents[i].E_State == NPCState.WaitTraffic)
                isTrafficStop = true;

        }
        
        var result = LinearProgram.Solve(orcaLines, lineCount, vOpt, maxSpeed);
        if (isTrafficStop)
        {
            result *= 0;
        }
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
            if (allAgents[i].Priority == agent.Priority) continue; // 자기 자신 스킵
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
        public float3 point; // half-plane을 만드는 점
        public float3 normal; // half-plane의 법선

        public bool IsInside(float3 v) => math.dot(v - point, normal) >= 0f;

        public float3 Project(float3 v)
        {
            float d = math.dot(v - point, normal);
            return d >= 0 ? v : v - d * normal;
        }
    }
}