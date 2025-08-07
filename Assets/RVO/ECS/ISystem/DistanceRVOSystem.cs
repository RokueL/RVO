using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Jobs;
using UnityEngine;

[UpdateAfter(typeof(UnitSpawn))]
[DisableAutoCreation]
public partial struct DistanceRVOSystemOptimized : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        Debug.Log("Distance");
        float dt = UnityEngine.Time.deltaTime;

        // 전체 유닛 가져오기
        // 전체 유닛 데이터 캐싱

        foreach (var (myunit, mylocalTransform, myentity) in SystemAPI.Query<RefRW<Unit>, RefRW<LocalTransform>>().WithEntityAccess())
        {
            var units = new NativeList<Unit>(Allocator.TempJob);
            var transforms = new NativeList<LocalTransform>(Allocator.TempJob);
            var entities = new NativeList<Entity>(Allocator.TempJob);
            var resultVelocities = new NativeArray<float3>(1, Allocator.TempJob);

            foreach (var (unit, localTransform, entity) in SystemAPI.Query<RefRO<Unit>, RefRW<LocalTransform>>().WithEntityAccess())
            {
                var distance = math.distance(mylocalTransform.ValueRO.Position , localTransform.ValueRO.Position);
                if (distance < 2 && myentity != entity)
                {
                    units.Add(unit.ValueRO);
                    transforms.Add(localTransform.ValueRO);
                    entities.Add(entity);
                }
            }
            
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

            if (units.Length > 0)
            {
                Debug.Log($"<color=red>{units.Length}</color>");
                mylocalTransform.ValueRW.Position += resultVelocities[0] * dt * myunit.ValueRO.MoveSpeed;
            }
            else
            {
                Debug.Log($"<color=cyan>{units.Length}</color>");
                float3 targetDir = math.normalize(myunit.ValueRO.Target - mylocalTransform.ValueRO.Position);
                float3 preferredVelocity = targetDir * myunit.ValueRO.MoveSpeed * UnityEngine.Time.deltaTime;
                mylocalTransform.ValueRW.Position += preferredVelocity;
            }

            units.Dispose();
            transforms.Dispose();
            entities.Dispose();
            resultVelocities.Dispose();
        }
    }

    [BurstCompile]
    public struct RVOJob : IJob
    {
        [ReadOnly] public Unit Unit;
        [ReadOnly] public LocalTransform Transform;
        [ReadOnly] public Entity Entity;

        [ReadOnly] public NativeArray<Unit> Units;
        [ReadOnly] public NativeArray<LocalTransform> Transforms;
        [ReadOnly] public NativeArray<Entity> Entities;
        public NativeArray<float3> ResultVelocities;
        public float DeltaTime;

        public void Execute()
        {
            float3 myPos = Transform.Position;
            float3 targetDir = math.normalizesafe(Unit.Target - myPos);
            float3 preferredVelocity = targetDir * Unit.MoveSpeed;
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
                if (dist < Unit.neighborDist)
                {
                    hasNeighbor = true;

                    float3 toOther = otherPos - myPos;
                    float3 otherGoalDir = math.normalizesafe(Units[i].Target - otherPos);
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
                preferredVelocity = offsetDir * Unit.MoveSpeed;
            }

            float3 bestVelocity = preferredVelocity;
            // ✅ 이웃이 있으면 RVO 계산, 없으면 그냥 목표 방향
            if (hasNeighbor)
            {
                bestVelocity = ComputeRVOVelocity(myPos, preferredVelocity, Unit, Units, Transforms);
            }

            ResultVelocities[0] = math.normalize(bestVelocity);
        }

        float3 ComputeRVOVelocity(float3 myPos, float3 preferredVelocity, Unit myUnit,
            NativeArray<Unit> units, NativeArray<LocalTransform> transforms
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
                 float3 sampleVel = dir * myUnit.MoveSpeed;
            
                 // 목표 방향에서 많이 어긋날수록 페널티가 큼
                 float penalty = math.lengthsq(sampleVel - preferredVelocity);
            
                 for (int j = 0; j < units.Length; j++)
                 {
                     // 다른 유닛 이동 방향과 내 이동방향의 시간 차에 따라 닿는 지 안 닿는 지 확인
                     float3 relPos = transforms[j].Position - myPos;
                     float3 vel = math.normalize(units[j].Target - transforms[j].Position) * units[j].MoveSpeed;
                     float3 relVel = sampleVel - vel;
                     // 두 유닛이 충돌한다고 간주하는 거리 기준
                     float combinedRadius = units[j].radius * 2f;
                     // 두 유닛이 현재 속도로 갈 경우 충돌까지 걸리는 시간 (없으면 무한대 반환)
                     float ttc = ComputeTimeToCollision(relPos, relVel, combinedRadius);
            
                     float myWeight = myUnit.Weight;
                     float otherWeight = units[j].Weight;
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