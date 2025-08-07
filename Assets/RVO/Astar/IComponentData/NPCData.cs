using Unity.Entities;
using Unity.Mathematics;

public enum NPCState : byte
{
    None = 0,
    Idle = 1 << 0,
    Move = 1 << 1,
    Talk = 1 << 2,
    WaitTraffic = 1 << 3,
    Smoke = 1 << 4,
}

public struct MoveNode : IBufferElementData
{
    public float3 WorldPos;
    public int2 GridPos;
}
 
public struct NPCData : IComponentData
{
    /// <summary> 현재 진행도 </summary>
    public int CurrentPathIndex;
    /// <summary> NPC 이동 속도 등 기본 정보 </summary>
    public MoveData NPCMoveData;
    /// <summary> 현재 NPC 상태 </summary>
    public NPCState E_NPCState;
}

public struct MoveData : IComponentData
{
    public float3 TargetPosition;
    
    public float MoveSpeed;
    public float RotateSpeed;
    public float3 MoveVelocity;
    public int MoveWeight;
    public int neightborDistance;
    public float Radius;
}