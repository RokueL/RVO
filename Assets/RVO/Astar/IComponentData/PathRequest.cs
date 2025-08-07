using Unity.Entities;
using Unity.Mathematics;

public struct PathRequest : IComponentData
{
    public int2 Start;
    public int2 End;
}

[InternalBufferCapacity(0)]
public struct PathHandled : IComponentData {} // 마커 컴포넌트
