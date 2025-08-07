using Unity.Entities;

namespace Unity.Physics.Stateful
{
    // 이벤트 상태를 설명합니다.
    // 이벤트 상태는 다음과 같이 설정됩니다:
    //    0) Undefined (정의되지 않음): 상태가 불명확하거나 필요하지 않을 때
    //    1) Enter (진입): 두 물체가 현재 프레임에서 상호작용하고 있지만, 이전 프레임에서는 상호작용하지 않았을 때
    //    2) Stay (유지): 두 물체가 현재 프레임에서 상호작용 중이고, 이전 프레임에서도 상호작용했을 때
    //    3) Exit (탈출): 두 물체가 현재 프레임에서 상호작용하지 않지만, 이전 프레임에서는 상호작용했을 때
    public enum StatefulEventState : byte
    {
        Undefined,  // 상태가 정의되지 않음
        Enter,      // 두 물체가 현재 프레임에서 상호작용 중이지만 이전 프레임에서는 상호작용하지 않았을 때
        Stay,       // 두 물체가 현재 프레임과 이전 프레임에서 모두 상호작용하고 있을 때
        Exit        // 두 물체가 현재 프레임에서 상호작용하지 않지만 이전 프레임에서는 상호작용했을 때
    }

    // 추가적인 StatefulEventState를 포함하여 ISimulationEvent를 확장합니다.
    public interface IStatefulSimulationEvent<T> : IBufferElementData, ISimulationEvent<T>
    {
        public StatefulEventState State { get; set; } // 이벤트 상태
    }
}