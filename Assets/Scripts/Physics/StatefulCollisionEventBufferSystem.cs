using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;
using Unity.Physics.Stateful;
using Unity.Physics;
using Unity.Physics.Systems;

// 이 시스템은 CollisionEvent 스트림을 StatefulCollisionEvent로 변환하여 Dynamic Buffer에 저장할 수 있도록 합니다.
// 변환을 위해서는 아래의 조건을 만족해야 합니다:
//    1) PhysicsShapeAuthoring 컴포넌트의 'Collision Response' 속성에서 'Collide Raise Collision Events' 옵션을 활성화하고,
//    2) StatefulCollisionEventBufferAuthoring 컴포넌트를 엔티티에 추가하여 (세부 사항 계산 여부도 선택),
//    또는, 만약 Character Controller에서 이를 원한다면:
//    1) CharacterControllerAuthoring 컴포넌트에서 'Raise Collision Events' 플래그를 활성화합니다.
[UpdateInGroup(typeof(PhysicsSystemGroup))] // 물리 시스템 그룹에 포함
[UpdateAfter(typeof(PhysicsSimulationGroup))] // 물리 시뮬레이션 그룹 후에 실행
public partial struct StatefulCollisionEventBufferSystem : ISystem
{
    private StatefulSimulationEventBuffers<StatefulCollisionEvent> m_StateFulEventBuffers; // StatefulCollisionEvent 버퍼
    private ComponentHandles m_Handles; // 컴포넌트 핸들

    // 아무런 작업을 하지 않는 컴포넌트. 일반적인 job을 사용하기 위해 만들어졌습니다. OnUpdate() 메서드에서 설명
    internal struct DummyExcludeComponent : IComponentData { };

    // 컴포넌트 핸들 구조체
    struct ComponentHandles
    {
        public ComponentLookup<DummyExcludeComponent> EventExcludes; // 이벤트 제외를 위한 컴포넌트 조회
        public ComponentLookup<StatefulCollisionEventDetails> EventDetails; // 이벤트 세부 사항을 위한 컴포넌트 조회
        public BufferLookup<StatefulCollisionEvent> EventBuffers; // 이벤트 버퍼 조회

        // 시스템 상태에 맞는 컴포넌트 핸들을 초기화합니다.
        public ComponentHandles(ref SystemState systemState)
        {
            EventExcludes = systemState.GetComponentLookup<DummyExcludeComponent>(true);
            EventDetails = systemState.GetComponentLookup<StatefulCollisionEventDetails>(true);
            EventBuffers = systemState.GetBufferLookup<StatefulCollisionEvent>(false);
        }

        // 시스템 상태에 맞는 컴포넌트 핸들을 업데이트합니다.
        public void Update(ref SystemState systemState)
        {
            EventExcludes.Update(ref systemState);
            EventBuffers.Update(ref systemState);
            EventDetails.Update(ref systemState);
        }
    }

    // 시스템 초기화
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        m_StateFulEventBuffers = new StatefulSimulationEventBuffers<StatefulCollisionEvent>(); // StatefulCollisionEvent 버퍼 할당
        m_StateFulEventBuffers.AllocateBuffers(); // 버퍼 할당
        state.RequireForUpdate<StatefulCollisionEvent>(); // 시스템이 업데이트 되도록 설정

        m_Handles = new ComponentHandles(ref state); // 컴포넌트 핸들 초기화
    }

    // 시스템 종료
    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        m_StateFulEventBuffers.Dispose(); // 버퍼 해제
    }

    // 충돌 이벤트 동적 버퍼를 지우는 작업을 수행하는 Job
    [BurstCompile]
    public partial struct ClearCollisionEventDynamicBufferJob : IJobEntity
    {
        public void Execute(ref DynamicBuffer<StatefulCollisionEvent> eventBuffer) => eventBuffer.Clear(); // 버퍼 초기화
    }

    // 시스템 업데이트 메서드
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        m_Handles.Update(ref state); // 컴포넌트 핸들 업데이트

        // 충돌 이벤트 동적 버퍼를 지우는 작업을 병렬로 스케줄
        state.Dependency = new ClearCollisionEventDynamicBufferJob()
            .ScheduleParallel(state.Dependency);

        // 이전/현재 버퍼를 교환
        m_StateFulEventBuffers.SwapBuffers();

        var currentEvents = m_StateFulEventBuffers.Current; // 현재 이벤트
        var previousEvents = m_StateFulEventBuffers.Previous; // 이전 이벤트

        // 충돌 이벤트와 세부 사항을 수집하는 작업을 스케줄
        state.Dependency = new StatefulEventCollectionJobs.
            CollectCollisionEventsWithDetails
        {
            CollisionEvents = currentEvents,
            PhysicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld, // 물리 월드
            EventDetails = m_Handles.EventDetails // 이벤트 세부 사항
        }.Schedule(SystemAPI.GetSingleton<SimulationSingleton>(), state.Dependency);

        // 이벤트 스트림을 동적 버퍼로 변환하는 작업을 스케줄
        state.Dependency = new StatefulEventCollectionJobs.
            ConvertEventStreamToDynamicBufferJob<StatefulCollisionEvent, DummyExcludeComponent>
        {
            CurrentEvents = currentEvents,
            PreviousEvents = previousEvents,
            EventLookup = m_Handles.EventBuffers, // 이벤트 버퍼 조회
            UseExcludeComponent = false, // 제외 컴포넌트 사용 안함
            EventExcludeLookup = m_Handles.EventExcludes // 이벤트 제외 조회
        }.Schedule(state.Dependency);
    }
}