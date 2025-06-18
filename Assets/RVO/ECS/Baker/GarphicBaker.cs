using System.Collections.Generic;
using Unity.Entities;
using Unity.Rendering;
using UnityEngine;

class GarphicBaker : MonoBehaviour
{
    public List<Material> material;
    public List<Mesh> mesh;
}

class GarphicBakerBaker : Baker<GarphicBaker>
{
    public override void Bake(GarphicBaker authoring)
    {
        
        var entity = GetEntity(TransformUsageFlags.None);

        AddComponentObject(entity, new GraphicData()
        {
            mesh = authoring.mesh,
            material = authoring.material
        });
        
        AddComponent(entity, new GraphicTag());
        
    }
}


