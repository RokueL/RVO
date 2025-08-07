using System.Collections.Generic;
using Unity.Entities;
using Unity.Rendering;
using UnityEngine;


public class GraphicData : IComponentData
{
    public List<Material> material;
    public List<Mesh> mesh;
}

public struct GraphicTag : IComponentData{}