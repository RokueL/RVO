using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Jobs;
using UnityEngine;
using Random = Unity.Mathematics.Random;

[UpdateAfter(typeof(UnitSpawn))]
[DisableAutoCreation]
public partial struct AstarRVOSystemOptimized : ISystem
{
    private DynamicBuffer<NavDestination> Destinations;
    private DynamicBuffer<NodeGroup> NodeGroups;

    public void OnUpdate(ref SystemState state)
    {
        Debug.Log("Astar");
        float dt = UnityEngine.Time.deltaTime;
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        // Query에서 가져온 Entity가 있다고 가정
        foreach (var (buffer, entity) in SystemAPI.Query<DynamicBuffer<NodeGroup>>()
                     .WithEntityAccess())
        {
            NodeGroups = buffer;
            break;
        }

        // 전체 유닛 가져오기
        // 전체 유닛 데이터 캐싱

        foreach (var (myunit, mylocalTransform, myentity) in SystemAPI.Query<RefRW<NPCData>, RefRW<LocalTransform>>().WithEntityAccess())
        {
            switch (myunit.ValueRO.E_NPCState)
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
                    while (transformint2.x == endPostint2.x && transformint2.y == endPostint2.y)
                    {
                        // seed = math.asuint(entity.Index * 73856093 ^ entity.Version * 19349663 ^ time) + 1;
                        // rng = new Random(seed);
                        rngNum = rng.NextInt(0, Destinations.Length);
                        endPostint2 = new int2((int)Destinations[rngNum].Node.WorldPos.x, (int)Destinations[rngNum].Node.WorldPos.z);
                    }
                    ecb.AddComponent(myentity, new PathRequest()
                    {
                        Start = new int2((int)mylocalTransform.ValueRO.Position.x, (int)mylocalTransform.ValueRO.Position.z),
                        End = new int2((int)Destinations[rngNum].Node.WorldPos.x, (int)Destinations[rngNum].Node.WorldPos.z)
                    });
                    //Debug.Log($"{new int2((int)transform.ValueRO.Position.x, (int)transform.ValueRO.Position.z)}  {new int2((int)Destinations[rngNum].Node.WorldPos.x, (int)Destinations[rngNum].Node.WorldPos.z)}");
                    NPCData data = myunit.ValueRO;
                    data.E_NPCState = NPCState.None;
                    ecb.SetComponent(myentity, data);
                    break;
                case NPCState.Move:
                    var pathBuffer = SystemAPI.GetBuffer<MoveNode>(myentity);
                    if (pathBuffer.Length == 0)
                        return;
                    
                    if (myunit.ValueRO.CurrentPathIndex >= pathBuffer.Length)
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
                        
                        
                        mylocalTransform.ValueRW.Position = myunit.ValueRO.NPCMoveData.TargetPosition;
                        myunit.ValueRW.E_NPCState = NPCState.None;
                        continue; // 도착 완료
                    }
                    
                    var units = new NativeList<NPCData>(Allocator.TempJob);
                    var transforms = new NativeList<LocalTransform>(Allocator.TempJob);
                    var entities = new NativeList<Entity>(Allocator.TempJob);
                    var resultVelocities = new NativeArray<float3>(1, Allocator.TempJob);

                    int neighborDistance = myunit.ValueRO.NPCMoveData.neightborDistance;
                    for (int x = -neighborDistance; x <= neighborDistance; x++)
                    {
                        for (int y = -neighborDistance; y <= neighborDistance; y++)
                        {
                            int2 myPos = new int2((int)mylocalTransform.ValueRO.Position.x, (int)mylocalTransform.ValueRO.Position.z);
                            int2 otherPos = myPos + new int2(x, y);
                            if (otherPos.x >= 0 && otherPos.x <= 300 && otherPos.y >= 0 && otherPos.y <= 300)
                            {
                                var navnode = NodeGroups[Index(otherPos, 300)].Entity;
                                var innerBuffer = SystemAPI.GetBuffer<NavNodeInnerEntity>(navnode);
                                foreach (var npc in innerBuffer)
                                {
                                    if (myentity == npc.Entity) continue;
                                    var neighborNpcData = state.EntityManager.GetComponentData<NPCData>(npc.Entity);
                                    var neighborTr = state.EntityManager.GetComponentData<LocalTransform>(npc.Entity);
                                    entities.Add(npc.Entity);
                                    units.Add(neighborNpcData);
                                    transforms.Add(neighborTr);
                                }
                            }
                        }
                    }

                    var originPos = mylocalTransform.ValueRO.Position;

                    if (units.Length > 0)
                    {

                        // 회피
                        var job = new RVOJob
                        {
                            Unit = myunit.ValueRO,
                            Transform = mylocalTransform.ValueRO,
                            Entity = myentity,
                            Units = units,
                            Transforms = transforms,
                            Entities = entities,
                            ResultVelocities = resultVelocities,
                            DeltaTime = dt
                        };


                        var handle = job.Schedule();
                        handle.Complete();
                        Debug.Log($"<color=red>{units.Length}</color>");
                        mylocalTransform.ValueRW.Position += resultVelocities[0] * dt;
                    }
                    else
                    {
                        Debug.Log($"<color=cyan>{units.Length}</color>");
                        float3 targetDir = math.normalizesafe(myunit.ValueRO.NPCMoveData.TargetPosition - mylocalTransform.ValueRO.Position);
                        float3 preferredVelocity = targetDir * myunit.ValueRO.NPCMoveData.MoveSpeed * UnityEngine.Time.deltaTime;
                        mylocalTransform.ValueRW.Position += preferredVelocity;
                    }

                    if (math.distance(mylocalTransform.ValueRO.Position, myunit.ValueRO.NPCMoveData.TargetPosition) < 0.05f)
                    {
                        myunit.ValueRW.CurrentPathIndex++;
                        if(myunit.ValueRO.CurrentPathIndex < pathBuffer.Length)
                            myunit.ValueRW.NPCMoveData.TargetPosition = pathBuffer[myunit.ValueRO.CurrentPathIndex].WorldPos;
                    }
                    
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
                        // 현재 버퍼 길이에 따라 색상 지정
                        int length = newInnerBuffer.Length;
                        Color color = Color.gray;

                        if (length >= 1 && length <= 3)
                            color = Color.green;
                        else if (length >= 4 && length <= 6)
                            color = Color.yellow;
                        else if (length >= 7)
                            color = Color.red;

// 디버그 큐브 그리기
                        DrawDebugCube(new int3((int)mylocalTransform.ValueRO.Position.x,0, (int)mylocalTransform.ValueRO.Position.z), 0.3f, color, 1f);
                    }


                    units.Dispose();
                    transforms.Dispose();
                    entities.Dispose();
                    resultVelocities.Dispose();
                    break;
            }
        }
        
        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
    void DrawDebugCube(float3 center, float size, Color color, float duration = 1f)
    {
        float3 half = new float3(size / 2f);

        float3 p0 = center + new float3(-half.x, -half.y, -half.z);
        float3 p1 = center + new float3(half.x, -half.y, -half.z);
        float3 p2 = center + new float3(half.x, -half.y, half.z);
        float3 p3 = center + new float3(-half.x, -half.y, half.z);

        float3 p4 = center + new float3(-half.x, half.y, -half.z);
        float3 p5 = center + new float3(half.x, half.y, -half.z);
        float3 p6 = center + new float3(half.x, half.y, half.z);
        float3 p7 = center + new float3(-half.x, half.y, half.z);

        // 바닥
        Debug.DrawLine(p0, p1, color, duration);
        Debug.DrawLine(p1, p2, color, duration);
        Debug.DrawLine(p2, p3, color, duration);
        Debug.DrawLine(p3, p0, color, duration);

        // 천장
        Debug.DrawLine(p4, p5, color, duration);
        Debug.DrawLine(p5, p6, color, duration);
        Debug.DrawLine(p6, p7, color, duration);
        Debug.DrawLine(p7, p4, color, duration);

        // 수직 연결
        Debug.DrawLine(p0, p4, color, duration);
        Debug.DrawLine(p1, p5, color, duration);
        Debug.DrawLine(p2, p6, color, duration);
        Debug.DrawLine(p3, p7, color, duration);
    }

    public static int Index(int2 pos, int2 gridSize) => pos.x * gridSize.x + pos.y;

    [BurstCompile]
    public struct RVOJob : IJob
    {
        [ReadOnly] public NPCData Unit;
        [ReadOnly] public LocalTransform Transform;
        [ReadOnly] public Entity Entity;

        [ReadOnly] public NativeArray<NPCData> Units;
        [ReadOnly] public NativeArray<LocalTransform> Transforms;
        [ReadOnly] public NativeArray<Entity> Entities;
        public NativeArray<float3> ResultVelocities;
        public float DeltaTime;

        public void Execute()
        {
            float3 myPos = Transform.Position;
            float3 targetDir = math.normalizesafe(Unit.NPCMoveData.TargetPosition - myPos);
            float3 preferredVelocity = targetDir * Unit.NPCMoveData.MoveSpeed;
            // preferredVelocity = AvoidCrowdingSameDirection(
            //     myPos, preferredVelocity, Unit, Units, Transforms
            // );
            float3 avoidance = float3.zero;
            int nearbyCount = 0;
            bool hasNeighbor = false;

            for (int i = 0; i < Units.Length; i++)
            {
                float3 otherPos = Transforms[i].Position;
                float dist = math.distance(myPos, otherPos);
                // ✅ 주변에 이웃이 있는지 검사
                if (dist < Unit.NPCMoveData.neightborDistance)
                {
                    hasNeighbor = true;

                    float3 toOther = otherPos - myPos;
                    float3 otherGoalDir = math.normalizesafe(Units[i].NPCMoveData.TargetPosition - otherPos);
                    float alignment = math.dot(targetDir, otherGoalDir);

                    if (alignment > 0.8f && dist < 1f)
                    {
                        avoidance -= math.normalizesafe(toOther) / (dist + 0.01f);
                        nearbyCount++;
                    }
                }
            }

            if (nearbyCount > 0)
            {
                float3 offsetDir = math.normalizesafe(targetDir + math.normalizesafe(avoidance) * 0.5f);
                preferredVelocity = offsetDir * Unit.NPCMoveData.MoveSpeed * 0.8f;
            }

            float3 bestVelocity = preferredVelocity;
            // ✅ 이웃이 있으면 RVO 계산, 없으면 그냥 목표 방향
            if (hasNeighbor)
            {
                bestVelocity = ComputeRVOVelocity(myPos, preferredVelocity, Unit, Units, Transforms);
            }

            ResultVelocities[0] = math.normalizesafe(bestVelocity);
        }

        float3 ComputeRVOVelocity(float3 myPos, float3 preferredVelocity, NPCData myUnit,
            NativeArray<NPCData> units, NativeArray<LocalTransform> transforms
        )
        {
             float3 bestVelocity = preferredVelocity;
             float minPenalty = float.MaxValue;
             int samples = 36;

             // 이거 내 기준 원형으로 경로 겹치는 지 체크
             for (int s = 0; s < samples; s++)
             {
                 float angle = (360f / samples) * s;
                 float rad = math.radians(angle);
                 float3 dir = new float3(math.sin(rad), 0, math.cos(rad));
                 float3 sampleVel = dir * myUnit.NPCMoveData.MoveSpeed;
            
                 // 목표 방향에서 많이 어긋날수록 페널티가 큼
                 float penalty = math.lengthsq(sampleVel - preferredVelocity);
            
                 for (int j = 0; j < units.Length; j++)
                 {
                     // 다른 유닛 이동 방향과 내 이동방향의 시간 차에 따라 닿는 지 안 닿는 지 확인
                     float3 relPos = transforms[j].Position - myPos;
                     float3 vel = math.normalize(units[j].NPCMoveData.TargetPosition - transforms[j].Position) * units[j].NPCMoveData.MoveSpeed;
                     float3 relVel = sampleVel - vel;
                     // 두 유닛이 충돌한다고 간주하는 거리 기준
                     float combinedRadius = units[j].NPCMoveData.Radius * 2f;
                     // 두 유닛이 현재 속도로 갈 경우 충돌까지 걸리는 시간 (없으면 무한대 반환)
                     float ttc = ComputeTimeToCollision(relPos, relVel, combinedRadius);
            
                     float myWeight = myUnit.NPCMoveData.MoveWeight;
                     float otherWeight = units[j].NPCMoveData.MoveWeight;
                     float totalWeight = myWeight + otherWeight + 0.0001f; // 0 나눗셈 방지
                     float reciprocalWeight = otherWeight / totalWeight; // 내 시점에서 상대가 더 무거우면 내가 더 피해줌
                     //n초 안에 충돌할 가능성이 있는 경우, 가까울수록 큰 페널티 부여
                     if (ttc > 0 && ttc < 3f)
                     {
                         float ttcPenalty = 1.0f / ttc;
                         penalty += ttcPenalty * 10f * reciprocalWeight;
                     }
                 }
            
                 if (penalty < minPenalty)
                 {
                     minPenalty = penalty;
                     bestVelocity = sampleVel;
                 }
             }

            return bestVelocity;
        }

        float3 AvoidCrowdingSameDirection(
            float3 myPos,
            float3 preferredVelocity,
            Unit myUnit,
            NativeArray<Unit> units,
            NativeArray<LocalTransform> transforms
        )
        {
            float3 myDir = math.normalizesafe(preferredVelocity);
            float mySpeed = math.length(preferredVelocity);

            int nearbyCount = 0;
            float3 avoidance = float3.zero;

            for (int i = 0; i < units.Length; i++)
            {
                // 거리 비교
                float3 otherPos = transforms[i].Position;
                float3 toOther = otherPos - myPos;
                float dist = math.length(toOther);

                // 인식 범위 바깥이면 스킵
                if (dist > myUnit.neighborDist) continue;

                //다른 녀석의 방향 값
                float3 otherGoalDir = math.normalizesafe(units[i].Target - otherPos);
                // 👇 같은 방향 유사도 비교 (코사인 유사도 사용)
                float alignment = math.dot(myDir, otherGoalDir);
                // 1 = 같은 방향
                // 👉 가까우면 회피 대상
                if (alignment > 0.8f && dist < 1f)
                {
                    avoidance -= math.normalizesafe(toOther) / dist;
                    nearbyCount++;
                }
            }

            // 근처에 유닛 있으면 회피 기동 추가
            if (nearbyCount > 0)
            {
                float3 offsetDir = math.normalizesafe(myDir + math.normalizesafe(avoidance) * 0.5f);
                float reducedSpeed = mySpeed * 0.8f;
                return offsetDir * reducedSpeed;
            }

            return preferredVelocity;
        }

        float ComputeTimeToCollision(float3 relPos, float3 relVel, float combinedRadius)
        {
            // 상대 속도가 0이면 움직이지 않으므로 충돌 없음 → 무한대 반환
            float a = math.dot(relVel, relVel);
            if (a == 0) return float.MaxValue;

            //상대 위치와 상대 속도의 내적
            float b = math.dot(relPos, relVel);
            // 현재 거리의 제곱에서 반지름 합 제곱을 뺀 것 → 충돌 여부에 영향을 줌
            float c = math.dot(relPos, relPos) - combinedRadius * combinedRadius;
            // 이 값이 음수이면 해 없음 → 충돌 안 함
            float discr = b * b - a * c;

            //음수 → 루트 안이 음수 → 현실 해 없음 → 충돌 없음
            if (discr < 0) return float.MaxValue;
            float t = (b - math.sqrt(discr)) / a;
            return t < 0 ? float.MaxValue : t;
        }
    }
}