using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor.Experimental.GraphView;

public struct NavNode : IComponentData
{
    public Entity Entity;
    public int2 GridPos;
    public float3 WorldPos;
    public E_RoadType E_RoadType;
    public Entity exNode;
    public float DynamicWeight;

    public NavNode(NavNode nod)
    {
        this.Entity = nod.Entity;
        this.GridPos = nod.GridPos;
        this.DynamicWeight = nod.DynamicWeight;
        this.WorldPos = nod.WorldPos;
        this.E_RoadType = nod.E_RoadType;
        this.exNode = nod.exNode;
    }
}

public struct NavNodeInnerEntity : IBufferElementData
{
    public Entity Entity;
}

public struct NodeGroup : IBufferElementData
{
    public Entity Entity;
    public NavNode Node;
}

public struct NodeGroupTag : IComponentData{}
