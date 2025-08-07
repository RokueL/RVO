using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

public class NavNodeDebugger : MonoBehaviour
{
    public float cubeSize = 0.2f;

    // void OnDrawGizmos()
    // {
    //     if (!World.DefaultGameObjectInjectionWorld.IsCreated) return;
    //
    //     var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
    //
    //     // NodeGroups 버퍼 먼저 가져오기
    //     EntityQuery groupQuery = entityManager.CreateEntityQuery(
    //         ComponentType.ReadOnly<NodeGroup>());
    //
    //     if (groupQuery.IsEmpty) return;
    //
    //     // 얘가 NodeGroups
    //     var groupEntities = groupQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
    //     var groupBuffer = entityManager.GetBuffer<NodeGroup>(groupEntities[0]); // 첫 번째 Entity에서 가져옴
    //     
    //     foreach (var node in groupBuffer)
    //     {
    //         var nodeEntity = node.Entity;
    //         var innerBuffer = entityManager.GetBuffer< NavNodeInnerEntity>(nodeEntity);
    //         var navNode = node.Node;
    //         if(navNode.WorldPos.z >150 || navNode.WorldPos.z < 50 || 
    //            navNode.WorldPos.x >20 || navNode.WorldPos.x < 5)
    //             continue;
    //         int length = innerBuffer.Length;
    //         Color color = Color.gray;
    //         if (length >= 1 && length <= 3)
    //             color = Color.green;
    //         else if (length >= 4 && length <= 6)
    //             color = Color.yellow;
    //         else if (length >= 7)
    //             color = Color.red;
    //
    //         Gizmos.color = color;
    //         Gizmos.DrawCube(navNode.WorldPos, Vector3.one * cubeSize);
    //     }
    //     
    //
    //     groupEntities.Dispose();
    // }
}