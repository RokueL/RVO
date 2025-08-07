using Unity.Entities;
using Unity.Jobs;
using Unity.Physics.Systems;
using Unity.Collections;
using Unity.Burst;

namespace Unity.Physics.Stateful
{
    // 이 시스템은 TriggerEvent의 스트림을 StatefulTriggerEvent로 변환하여 Dynamic Buffer에 저장할 수 있도록 합니다.
    // 이를 통해 충돌 상태(Enter, Stay, Exit)를 추적할 수 있습니다.
    [UpdateInGroup(typeof(PhysicsSystemGroup))] // PhysicsSystemGroup 내에서 실행됨
    [UpdateAfter(typeof(PhysicsSimulationGroup))] // PhysicsSimulationGroup 이후에 실행됨
    public partial struct StatefulTriggerEventBufferSystem : ISystem
    {
        // 현재 및 이전 프레임의 Trigger 이벤트를 저장하는 버퍼
        private StatefulSimulationEventBuffers<StatefulTriggerEvent> m_StateFulEventBuffers;
        // 이벤트 처리에 필요한 컴포넌트 조회 핸들
        private ComponentHandles m_ComponentHandles;
        // 트리거 이벤트를 처리할 엔티티 쿼리
        private EntityQuery m_TriggerEventQuery;

        // 이벤트를 저장할 컴포넌트 핸들 구조체
        struct ComponentHandles
        {
            // 특정 이벤트를 제외할 엔티티 조회
            public ComponentLookup<StatefulTriggerEventExclude> EventExcludes;
            // StatefulTriggerEvent를 저장할 Dynamic Buffer 조회
            public BufferLookup<StatefulTriggerEvent> EventBuffers;

            // 컴포넌트 핸들 초기화
            public ComponentHandles(ref SystemState systemState)
            {
                EventExcludes = systemState.GetComponentLookup<StatefulTriggerEventExclude>(true);
                EventBuffers = systemState.GetBufferLookup<StatefulTriggerEvent>();
            }

            // 최신 상태로 업데이트
            public void Update(ref SystemState systemState)
            {
                EventExcludes.Update(ref systemState);
                EventBuffers.Update(ref systemState);
            }
        }

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            // 트리거 이벤트를 처리할 엔티티 쿼리 생성
            // (StatefulTriggerEvent가 있으며, StatefulTriggerEventExclude가 없는 엔티티)
            EntityQueryBuilder builder = new EntityQueryBuilder(Allocator.Temp)
                .WithAllRW<StatefulTriggerEvent>() // StatefulTriggerEvent를 읽고 쓸 수 있는 엔티티 선택
                .WithNone<StatefulTriggerEventExclude>(); // StatefulTriggerEventExclude가 없는 엔티티만 선택

            // Stateful 이벤트 버퍼를 생성하고 할당
            m_StateFulEventBuffers = new StatefulSimulationEventBuffers<StatefulTriggerEvent>();
            m_StateFulEventBuffers.AllocateBuffers();

            // 쿼리 설정 및 시스템 업데이트 필요성 명시
            m_TriggerEventQuery = state.GetEntityQuery(builder);
            state.RequireForUpdate(m_TriggerEventQuery);

            // 컴포넌트 핸들 초기화
            m_ComponentHandles = new ComponentHandles(ref state);
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            // Stateful 이벤트 버퍼 해제
            m_StateFulEventBuffers.Dispose();
        }

        // 트리거 이벤트 버퍼를 초기화하는 Job
        [BurstCompile]
        public partial struct ClearTriggerEventDynamicBufferJob : IJobEntity
        {
            // 이벤트 버퍼 초기화
            public void Execute(ref DynamicBuffer<StatefulTriggerEvent> eventBuffer) => eventBuffer.Clear();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // 최신 상태로 컴포넌트 핸들 업데이트
            m_ComponentHandles.Update(ref state);

            // 기존 트리거 이벤트 버퍼 초기화 (병렬 실행)
            state.Dependency = new ClearTriggerEventDynamicBufferJob() .ScheduleParallel(m_TriggerEventQuery, state.Dependency);

            // 이전 프레임과 현재 프레임의 이벤트 버퍼를 교체
            m_StateFulEventBuffers.SwapBuffers();

            var currentEvents = m_StateFulEventBuffers.Current; // 현재 프레임의 이벤트
            var previousEvents = m_StateFulEventBuffers.Previous; // 이전 프레임의 이벤트

            // 새로운 Trigger 이벤트를 수집하는 Job 실행
            state.Dependency = new StatefulEventCollectionJobs.CollectTriggerEvents
            {
                TriggerEvents = currentEvents
            }.Schedule(SystemAPI.GetSingleton<SimulationSingleton>(), state.Dependency);

            // 트리거 이벤트를 StatefulTriggerEvent로 변환하여 Dynamic Buffer에 저장하는 Job 실행
            state.Dependency = new StatefulEventCollectionJobs.ConvertEventStreamToDynamicBufferJob<StatefulTriggerEvent, StatefulTriggerEventExclude>
            {
                CurrentEvents = currentEvents, // 현재 프레임의 이벤트
                PreviousEvents = previousEvents, // 이전 프레임의 이벤트
                EventLookup = m_ComponentHandles.EventBuffers, // 이벤트 버퍼 조회

                UseExcludeComponent = true, // 제외할 컴포넌트(StatefulTriggerEventExclude) 사용 여부
                EventExcludeLookup = m_ComponentHandles.EventExcludes // 제외할 이벤트 조회
            }.Schedule(state.Dependency);
        }
    }
}