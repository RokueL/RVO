using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

class NPCSpawnPointAuthoring : MonoBehaviour
{
    public Transform[] SpawnPoint;
}

class NPCSpawnPointAuthoringBaker : Baker<NPCSpawnPointAuthoring>
{
    public override void Bake(NPCSpawnPointAuthoring authoring)
    {
        var entity = GetEntity(TransformUsageFlags.None);

        var buffer = AddBuffer<NavSpawnPoint>(entity);
            
        for (int i = 0; i < authoring.SpawnPoint.Length; i++)
        {
            buffer.Add( new NavSpawnPoint()
            {
                Entity = GetEntity(TransformUsageFlags.None),
                Node = new NavNode(){
                    GridPos = new int2((int)authoring.SpawnPoint[i].position.x, (int)authoring.SpawnPoint[i].position.z),
                    WorldPos = new float3(
                        (int)authoring.SpawnPoint[i].position.x, 
                        (int)authoring.SpawnPoint[i].position.y, 
                        (int)authoring.SpawnPoint[i].position.z)
                }
            });
        }
    }
}