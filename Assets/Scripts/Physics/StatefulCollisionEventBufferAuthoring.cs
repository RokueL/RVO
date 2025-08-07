using System.Runtime.InteropServices;
using Unity.Assertions;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Unity.Physics.Stateful
{
    // StatefulCollisionEventBufferAuthoring은 Unity의 MonoBehaviour로, 
    // 물리 충돌 이벤트가 포함된 버퍼를 정의하고, 해당 엔티티에 대해 세부 사항을 계산할지 여부를 결정합니다.
    public class StatefulCollisionEventBufferAuthoring : MonoBehaviour
    {
        [Tooltip("선택하면 이 엔티티의 충돌 이벤트 동적 버퍼에서 세부 정보가 계산됩니다")]
        public bool CalculateDetails = false; // 충돌 이벤트에 세부 사항을 계산할지 여부를 결정하는 변수

        // Baker 클래스는 Authoring 컴포넌트에서 데이터를 추출하고 Entity로 변환하는 역할을 합니다.
        class StatefulCollisionEventBufferBaker : Baker<StatefulCollisionEventBufferAuthoring>
        {
            public override void Bake(StatefulCollisionEventBufferAuthoring authoring)
            {
                // TransformUsageFlags.Dynamic 플래그를 사용하여 해당 엔티티를 생성합니다.
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                // CalculateDetails가 true일 경우, StatefulCollisionEventDetails를 엔티티에 추가합니다.
                if (authoring.CalculateDetails)
                {
                    var dynamicBufferTag = new StatefulCollisionEventDetails
                    {
                        CalculateDetails = authoring.CalculateDetails
                    };

                    AddComponent(entity, dynamicBufferTag); // StatefulCollisionEventDetails 컴포넌트 추가
                }

                // 충돌 이벤트를 저장할 버퍼 추가
                AddBuffer<StatefulCollisionEvent>(entity);
            }
        }
    }

    // StatefulCollisionEventDetails 컴포넌트는 엔티티가 세부 사항을 계산할지를 설정하는 플래그입니다.
    public struct StatefulCollisionEventDetails : IComponentData
    {
        public bool CalculateDetails; // 세부 사항을 계산할지 여부를 설정하는 변수
    }

    // StatefulCollisionEvent는 DynamicBuffer에 저장할 수 있는 충돌 이벤트 구조체입니다.
    public struct StatefulCollisionEvent : IBufferElementData, IStatefulSimulationEvent<StatefulCollisionEvent>
    {
        public Entity EntityA { get; set; } // 첫 번째 엔티티
        public Entity EntityB { get; set; } // 두 번째 엔티티
        public int BodyIndexA { get; set; } // 첫 번째 엔티티의 바디 인덱스
        public int BodyIndexB { get; set; } // 두 번째 엔티티의 바디 인덱스
        public ColliderKey ColliderKeyA { get; set; } // 첫 번째 엔티티의 충돌체 키
        public ColliderKey ColliderKeyB { get; set; } // 두 번째 엔티티의 충돌체 키
        public StatefulEventState State { get; set; } // 이벤트 상태 (Enter, Stay, Exit 등)
        public float3 Normal; // 충돌 법선 벡터

        // CalculateDetails가 체크된 경우, 이 필드는 유효한 값이 있으며 그렇지 않으면 기본값으로 초기화됩니다.
        internal Details CollisionDetails;

        // 생성자: CollisionEvent를 StatefulCollisionEvent로 변환합니다.
        public StatefulCollisionEvent(CollisionEvent collisionEvent)
        {
            EntityA = collisionEvent.EntityA;
            EntityB = collisionEvent.EntityB;
            BodyIndexA = collisionEvent.BodyIndexA;
            BodyIndexB = collisionEvent.BodyIndexB;
            ColliderKeyA = collisionEvent.ColliderKeyA;
            ColliderKeyB = collisionEvent.ColliderKeyB;
            State = default;
            Normal = collisionEvent.Normal;
            CollisionDetails = default;
        }

        // 충돌 세부 사항을 설명하는 구조체
        public struct Details
        {
            internal bool IsValid; // 세부 사항이 유효한지 여부

            // 1이면 정점 충돌, 2이면 모서리 충돌, 3 이상이면 면 충돌
            public int NumberOfContactPoints;

            // 추정된 임펄스
            public float EstimatedImpulse;
            // 평균 접촉 지점 위치
            public float3 AverageContactPointPosition;

            // 생성자: 세부 사항을 초기화합니다.
            public Details(int numContactPoints, float estimatedImpulse, float3 averageContactPosition)
            {
                IsValid = (0 < numContactPoints); // 유효한 접촉점이 있을 경우에만 세부 사항이 유효함
                NumberOfContactPoints = numContactPoints;
                EstimatedImpulse = estimatedImpulse;
                AverageContactPointPosition = averageContactPosition;
            }
        }

        // 주어진 엔티티와 다른 엔티티를 반환하는 함수
        public Entity GetOtherEntity(Entity entity)
        {
            Assert.IsTrue((entity == EntityA) || (entity == EntityB)); // 엔티티 A 또는 B만 유효함
            return entity == EntityA ? EntityB : EntityA;
        }

        // 주어진 엔티티에서 다른 엔티티로 향하는 법선 벡터를 반환합니다.
        public float3 GetNormalFrom(Entity entity)
        {
            Assert.IsTrue((entity == EntityA) || (entity == EntityB)); // 엔티티 A 또는 B만 유효함
            return math.select(-Normal, Normal, entity == EntityB); // EntityB에 대해서는 반대 방향 법선을 반환
        }

        // 충돌 세부 사항이 유효한지 확인하고, 유효한 경우 세부 사항을 반환합니다.
        public bool TryGetDetails(out Details details)
        {
            details = CollisionDetails;
            return CollisionDetails.IsValid; // 세부 사항이 유효한지 여부를 반환
        }

        // 이벤트를 비교하는 함수
        public int CompareTo(StatefulCollisionEvent other) => ISimulationEventUtilities.CompareEvents(this, other);
    }
}