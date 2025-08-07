using Unity.Assertions;
using Unity.Entities;
using UnityEngine;

namespace Unity.Physics.Stateful
{
    // StatefulTriggerEventBufferAuthoring 컴포넌트
    // GameObject에 추가하면 해당 오브젝트는 ECS Entity로 변환될 때 StatefulTriggerEvent 버퍼를 갖게 됨.
    public class StatefulTriggerEventBufferAuthoring : MonoBehaviour
    {
        // Baker 클래스: GameObject를 ECS Entity로 변환하는 역할
        class Baker : Baker<StatefulTriggerEventBufferAuthoring>
        {
            public override void Bake(StatefulTriggerEventBufferAuthoring authoring)
            {
                // 현재 GameObject를 ECS Entity로 변환 (Transform 사용 방식: 동적)
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                // StatefulTriggerEvent를 저장할 Dynamic Buffer 추가
                AddBuffer<StatefulTriggerEvent>(entity);
            }
        }
    }

    // StatefulTriggerEvent 구조체
    // Trigger 이벤트를 저장할 수 있는 데이터 구조 (IStatefulSimulationEvent 인터페이스 구현)
    public struct StatefulTriggerEvent : IBufferElementData, IStatefulSimulationEvent<StatefulTriggerEvent>
    {
        // 트리거 이벤트에 연관된 두 개의 엔티티
        public Entity EntityA { get; set; }
        public Entity EntityB { get; set; }

        // 물리 본체(Body)의 인덱스
        public int BodyIndexA { get; set; }
        public int BodyIndexB { get; set; }

        // 충돌한 Collider의 키
        public ColliderKey ColliderKeyA { get; set; }
        public ColliderKey ColliderKeyB { get; set; }

        // 현재 이벤트 상태 (Enter, Stay, Exit)
        public StatefulEventState State { get; set; }

        // 생성자: 기존의 TriggerEvent 데이터를 StatefulTriggerEvent로 변환
        public StatefulTriggerEvent(TriggerEvent triggerEvent)
        {
            EntityA = triggerEvent.EntityA;
            EntityB = triggerEvent.EntityB;
            BodyIndexA = triggerEvent.BodyIndexA;
            BodyIndexB = triggerEvent.BodyIndexB;
            ColliderKeyA = triggerEvent.ColliderKeyA;
            ColliderKeyB = triggerEvent.ColliderKeyB;
            State = default; // 초기 상태 설정 (Enter, Stay, Exit 없음)
        }

        // 특정 엔티티에 대해, 이벤트에 연관된 '다른 엔티티'를 반환하는 메서드
        public Entity GetOtherEntity(Entity entity)
        {
            // 전달된 엔티티가 EntityA 또는 EntityB인지 검증 (디버그용)
            Assert.IsTrue((entity == EntityA) || (entity == EntityB));

            // EntityA가 입력되면 EntityB를 반환하고, 반대로 EntityB가 입력되면 EntityA 반환
            return (entity == EntityA) ? EntityB : EntityA;
        }

        // 이벤트 비교를 위한 메서드 (정렬, 검색 등에 사용 가능)
        public int CompareTo(StatefulTriggerEvent other)
        {
            return ISimulationEventUtilities.CompareEvents(this, other);
        }
    }

    // StatefulTriggerEvent를 제외하는 컴포넌트
    // 이 컴포넌트가 추가된 엔티티는 StatefulTriggerEventBufferSystem에서 이벤트를 수집하지 않음
    public struct StatefulTriggerEventExclude : IComponentData { }
}