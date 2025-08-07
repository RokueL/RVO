using Unity.Entities;
using UnityEngine;

public class NavNodeGizmoDrawer : MonoBehaviour
{
    void OnDrawGizmos()
    {
        if (!World.DefaultGameObjectInjectionWorld.IsCreated) return;
        
        var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        
        var query = entityManager.CreateEntityQuery(typeof(NavNode));
        var nodes = query.ToComponentDataArray<NavNode>(Unity.Collections.Allocator.Temp);
        
        foreach (var node in nodes)
        {
            //Gizmos.color = node.Walkable ? Color.green : Color.red;
            Gizmos.DrawCube(node.WorldPos, new Vector3(0.9f, 0.1f, 0.9f));
        }
    }
}