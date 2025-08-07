using Unity.Entities;
using Unity.Mathematics;

public struct NavDestination : IBufferElementData
{
    public Entity Entity;
    public NavNode Node;
}

public struct NavSpawnPoint : IBufferElementData
{
    public Entity Entity;
    public NavNode Node;
}