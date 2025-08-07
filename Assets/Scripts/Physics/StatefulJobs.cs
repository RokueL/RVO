using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace Unity.Physics.Stateful
{
    public static class StatefulEventCollectionJobs
    {
        // Trigger 이벤트를 StatefulTriggerEvent로 변환하여 NativeList에 추가하는 Job
        [BurstCompile]
        public struct CollectTriggerEvents : ITriggerEventsJob
        {
            // 저장할 Trigger 이벤트를 담을 리스트
            public NativeList<StatefulTriggerEvent> TriggerEvents;

            // TriggerEvent를 StatefulTriggerEvent로 변환하여 리스트에 추가
            public void Execute(TriggerEvent triggerEvent) => TriggerEvents.Add(new StatefulTriggerEvent(triggerEvent));
        }

        // Collision 이벤트를 StatefulCollisionEvent로 변환하여 NativeList에 추가하는 Job
        [BurstCompile]
        public struct CollectCollisionEvents : ICollisionEventsJob
        {
            // 저장할 Collision 이벤트를 담을 리스트
            public NativeList<StatefulCollisionEvent> CollisionEvents;

            // CollisionEvent를 StatefulCollisionEvent로 변환하여 리스트에 추가
            public void Execute(CollisionEvent collisionEvent) => CollisionEvents.Add(new StatefulCollisionEvent(collisionEvent));
        }

        // Collision 이벤트와 함께 세부 정보를 계산하여 StatefulCollisionEvent로 변환하고 NativeList에 추가하는 Job
        [BurstCompile]
        public struct CollectCollisionEventsWithDetails : ICollisionEventsJob
        {
            // 저장할 Collision 이벤트를 담을 리스트
            public NativeList<StatefulCollisionEvent> CollisionEvents;

            // 물리 월드와 세부 사항 계산에 사용할 컴포넌트 조회
            [ReadOnly] public PhysicsWorld PhysicsWorld;
            [ReadOnly] public ComponentLookup<StatefulCollisionEventDetails> EventDetails;

            // 세부 사항 계산을 강제할지 여부
            public bool ForceCalculateDetails;

            // CollisionEvent를 처리하고 세부 사항을 계산하여 StatefulCollisionEvent로 변환
            public void Execute(CollisionEvent collisionEvent)
            {
                var statefulCollisionEvent = new StatefulCollisionEvent(collisionEvent);

                // 세부 사항 계산 여부 결정
                bool calculateDetails = ForceCalculateDetails;
                if (!calculateDetails && EventDetails.HasComponent(collisionEvent.EntityA))
                {
                    calculateDetails = EventDetails[collisionEvent.EntityA].CalculateDetails;
                }
                if (!calculateDetails && EventDetails.HasComponent(collisionEvent.EntityB))
                {
                    calculateDetails = EventDetails[collisionEvent.EntityB].CalculateDetails;
                }

                // 세부 사항을 계산하고 상태에 저장
                if (calculateDetails)
                {
                    var details = collisionEvent.CalculateDetails(ref PhysicsWorld);
                    statefulCollisionEvent.CollisionDetails = new StatefulCollisionEvent.Details(
                        details.EstimatedContactPointPositions.Length,
                        details.EstimatedImpulse,
                        details.AverageContactPointPosition);
                }

                // 계산된 CollisionEvent를 리스트에 추가
                CollisionEvents.Add(statefulCollisionEvent);
            }
        }

        // 이벤트 스트림을 Dynamic Buffer로 변환하는 Job
        // T: StatefulSimulationEvent 타입
        // C: 제외할 컴포넌트 타입
        [BurstCompile]
        public struct ConvertEventStreamToDynamicBufferJob<T, C> : IJob
            where T : unmanaged, IBufferElementData, IStatefulSimulationEvent<T>  // T는 IStatefulSimulationEvent를 구현해야 함
            where C : unmanaged, IComponentData  // C는 IComponentData를 구현해야 함
        {
            // 이전과 현재의 이벤트 리스트
            public NativeList<T> PreviousEvents;
            public NativeList<T> CurrentEvents;

            // 이벤트를 저장할 Dynamic Buffer 조회
            public BufferLookup<T> EventLookup;

            // 제외할 컴포넌트를 사용할지 여부
            public bool UseExcludeComponent;

            // 제외할 컴포넌트를 조회하는 컴포넌트 핸들
            [ReadOnly] public ComponentLookup<C> EventExcludeLookup;

            // 이벤트를 변환하고 Dynamic Buffer에 추가하는 작업
            public void Execute()
            {
                // 이벤트를 저장할 임시 리스트
                var statefulEvents = new NativeList<T>(CurrentEvents.Length, Allocator.Temp);

                // 이전 및 현재 이벤트에서 상태를 기반으로 상태 변경된 이벤트를 가져옴
                StatefulSimulationEventBuffers<T>.GetStatefulEvents(PreviousEvents, CurrentEvents, statefulEvents);

                // 상태가 변경된 이벤트를 처리
                for (int i = 0; i < statefulEvents.Length; i++)
                {
                    var statefulEvent = statefulEvents[i];

                    // 이벤트가 추가될 엔티티 A와 B가 동적으로 버퍼를 가지고 있는지 확인
                    var addToEntityA = EventLookup.HasBuffer(statefulEvent.EntityA) &&
                        (!UseExcludeComponent || !EventExcludeLookup.HasComponent(statefulEvent.EntityA));
                    var addToEntityB = EventLookup.HasBuffer(statefulEvent.EntityB) &&
                        (!UseExcludeComponent || !EventExcludeLookup.HasComponent(statefulEvent.EntityA));

                    // 엔티티 A와 B에 이벤트를 추가
                    if (addToEntityA)
                    {
                        EventLookup[statefulEvent.EntityA].Add(statefulEvent);
                    }

                    if (addToEntityB)
                    {
                        EventLookup[statefulEvent.EntityB].Add(statefulEvent);
                    }
                }
            }
        }
    }
}