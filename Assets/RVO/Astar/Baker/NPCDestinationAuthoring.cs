using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

class NPCDestinationAuthoring : MonoBehaviour
{
    public Transform[] Destionation;
}

class NPCDestinationAuthoringBaker : Baker<NPCDestinationAuthoring>
{
    public override void Bake(NPCDestinationAuthoring authoring)
    {
        var entity = GetEntity(TransformUsageFlags.None);

        var buffer = AddBuffer<NavDestination>(entity);
            
        for (int i = 0; i < authoring.Destionation.Length; i++)
        {
            buffer.Add( new NavDestination()
            {
                Entity = GetEntity(TransformUsageFlags.None),
                Node = new NavNode(){
                GridPos = new int2((int)authoring.Destionation[i].position.x, (int)authoring.Destionation[i].position.z),
                WorldPos = new float3(
                    (int)authoring.Destionation[i].position.x, 
                    (int)authoring.Destionation[i].position.y, 
                    (int)authoring.Destionation[i].position.z)
                }
            });
        }
    }
}
