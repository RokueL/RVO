using System.Collections.Generic;
using System.IO;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(ObstacleCheckSystem))]
partial struct NavGeneratePathSystem : ISystem
{
    private DynamicBuffer<NodeGroup> NodeGroups;
    
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PathRequest>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        // Query에서 가져온 Entity가 있다고 가정
        foreach (var (buffer, entity) in SystemAPI.Query<DynamicBuffer<NodeGroup>>()
                     .WithEntityAccess())
        {
            NodeGroups = buffer;
            break;
        }
        
        
        foreach (var (req, entity) in SystemAPI.Query<PathRequest>().WithEntityAccess())
        {
            var gridArray = NodeGroups.ToNativeArray(Allocator.TempJob);

            var pathResult = new NativeList<NavNode>(Allocator.TempJob);
 
            var job = new AStarJob
            {
                grid = gridArray,
                gridSize = new int2(300, 300),
                start = req.Start,
                end = req.End,
                path = pathResult
            };
            
            job.Run();

            var resultBuffer = SystemAPI.GetBuffer<MoveNode>(entity);
            for (int i = 0; i < pathResult.Length; i++)
            {
                resultBuffer.Add(new MoveNode() { WorldPos = new float3(pathResult[i].GridPos.x, 0f, pathResult[i].GridPos.y) });
                
                int idx = Index(pathResult[i].GridPos, new int2(300, 300));
                var elem = NodeGroups[idx];
                var node = elem.Node;
                node.DynamicWeight += 5f;
                elem.Node = node;
                NodeGroups[idx] = elem;

// "노드 엔티티"에도 컴포넌트가 있다면 같이 업데이트!
                ecb.SetComponent(elem.Entity, node);
            }

            ecb.RemoveComponent<PathRequest>(entity);
            ORCAAgent agent = state.EntityManager.GetComponentData<ORCAAgent>(entity);
            LocalTransform tr = state.EntityManager.GetComponentData<LocalTransform>(entity);
            agent.TargetPosition = new float3(pathResult[0].GridPos.x, 0, pathResult[0].GridPos.y); 
            agent.CurrentPathIndex = 0;
            agent.E_State = NPCState.Move;
            ecb.SetComponent(entity, agent);
            pathResult.Dispose();
            gridArray.Dispose();
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
    public static int Index(int2 pos, int2 gridSize) => (pos.x + gridSize.x / 2) * gridSize.x + (pos.y + gridSize.y / 2);

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
    }

    [BurstCompile]
    public partial struct AStarJob : IJobEntity
    {
        [ReadOnly] public NativeArray<NodeGroup> grid;
        public int2 gridSize;
        public int2 start;
        public int2 end;
        public NativeList<NavNode> path;
        int Index(int2 pos) => (pos.x+(gridSize.x/2)) * gridSize.x + (pos.y+(gridSize.y/2));

        public void Execute()
        {
            if (path.Length != 0)
                return;
            var openSet = new NativeList<NavNode>(Allocator.Temp);
            var cameFrom = new NativeArray<int>(grid.Length, Allocator.Temp);
            var neighbors = new NativeList<NavNode>(Allocator.Temp);
            var gCost = new NativeArray<int>(grid.Length, Allocator.Temp);
            NativeList<NavNode> closedSet = new NativeList<NavNode>(Allocator.Temp);

            NavNode startNode = grid[Index(start)].Node;
            openSet.Add(startNode);
            NavNode currentNode = openSet[0];
            int currentIdx = Index(openSet[0].GridPos);
            gCost[currentIdx] = 0;
            

            while (openSet.Length > 0)
            {
                // 제일 가까운 곳을 현재 위치 지정
                currentIdx = Index(openSet[0].GridPos);
                currentNode = openSet[0];
                int costValue = gCost[currentIdx] + GetDistance(currentNode.GridPos, end);
                for (int i = 0; i < openSet.Length; i++)
                {
                    int iIdx = Index(openSet[i].GridPos);
                    int iFCost = gCost[iIdx] + GetDistance(openSet[i].GridPos, end);
                    
                    if (iFCost < costValue || (iFCost == costValue && GetDistance(openSet[i].GridPos, end) < GetDistance(currentNode.GridPos, end)))
                    {
                        currentNode = openSet[i];
                        costValue = iFCost;
                    }
                }

                currentIdx = Index(currentNode.GridPos);
                RemoveAt(ref openSet, currentNode);
                closedSet.Add(currentNode);


                if (currentNode.GridPos.Equals(end))
                {
                    while (!currentNode.GridPos.Equals(start))
                    {
                        path.Add(currentNode);
                        currentNode = grid[cameFrom[Index(currentNode.GridPos)]].Node;
                    }

                    path.Add(grid[Index(start)].Node);
                    ReverseNativeList(ref path); // 시작 → 끝 방향으로 전환
                    
                    return;
                }


                bool isEnd = false;
                int count = 0;
                neighbors.Clear();
                GetNeighbours(currentNode, gridSize, grid, ref neighbors);

                foreach (NavNode neighbour in neighbors)
                {
                    if (neighbour.E_RoadType == E_RoadType.Collider)
                        count++;
                }
                
                if (neighbors.Length == count)
                    return;
                
                // 주변 이웃 확인 후 갈 수 있는 곳 확인하기 다음 루틴에서 짧은 곳 찾을 거임
                foreach (NavNode neighbour in neighbors)
                {
                    // 갈 수 없으면 끝
                    if (neighbour.E_RoadType == E_RoadType.Collider || Contains(closedSet, neighbour))
                        continue;
                    int neighborIdx = Index(neighbour.GridPos);
                    float dynamicWeight = grid[neighborIdx].Node.DynamicWeight;
                    int tentativeGCost = gCost[currentIdx] + GetDistance(currentNode.GridPos, neighbour.GridPos) + (int)dynamicWeight;

                    // 이웃이 openSet에 없거나, 더 짧은 경로일 때만 갱신
                    bool inOpenSet = Contains(openSet, neighbour);
                    if (!inOpenSet || tentativeGCost < gCost[neighborIdx])
                    {
                        cameFrom[neighborIdx] = currentIdx;
                        gCost[neighborIdx] = tentativeGCost;

                        if (!inOpenSet)
                            openSet.Add(neighbour);
                    }
                }
            }

            openSet.Dispose();
            cameFrom.Dispose();
            neighbors.Dispose();
            closedSet.Dispose();
        }
    }
    
    static void ReverseNativeList(ref NativeList<NavNode> list)
    {
        int count = list.Length;
        for (int i = 0; i < count / 2; i++)
        {
            var temp = list[i];
            list[i] = list[count - 1 - i];
            list[count - 1 - i] = temp;
        }
    }

    static bool Contains(NativeList<NavNode> list, NavNode nodeToFind)
    {
        for (int i = 0; i < list.Length; i++)
        {
            if (list[i].GridPos.Equals(nodeToFind.GridPos))
            {
                return true;
            }
        }

        return false;
    }

    static void RemoveAt(ref NativeList<NavNode> openSet, NavNode target)
    {
        for (int i = 0; i < openSet.Length; i++)
        {
            if (openSet[i].GridPos.Equals(target.GridPos))
            {
                // 오른쪽으로 한 칸씩 밀어서 덮어쓰기
                for (int j = i; j < openSet.Length - 1; j++)
                    openSet[j] = openSet[j + 1];

                openSet.Length--;
                break;
            }
        }
    }

    /// <summary> 주변 노드 확인 후 가져오기</summary>
    /// <param name="node"> 내 노드 </param>
    /// <returns></returns>
    private static void GetNeighbours(NavNode node, int2 gridSize, NativeArray<NodeGroup> grid, ref NativeList<NavNode> path)
    {
        int2 origin = node.GridPos;
        for (int x = -1; x < 2; x++)
        {
            for (int y = -1; y < 2; y++)
            {
                int2 neighborPos = origin + new int2(x, y);

                if (neighborPos.x < -gridSize.x/2f || neighborPos.x >= gridSize.x/2f ||
                    neighborPos.y < -gridSize.x/2f || neighborPos.y >= gridSize.y/2f)
                    continue;

                int index = (neighborPos.x + gridSize.x / 2) * gridSize.x + (neighborPos.y+gridSize.x/2);
                var neighborNode = grid[index].Node;
                if (neighborNode.E_RoadType != E_RoadType.Collider)
                {
                    path.Add(neighborNode);
                }
            }
        }
    }


    /// <summary> 대각선으로도 이동 </summary>
    /// <param name="pos1"></param>
    /// <param name="pos2"></param>
    /// <returns></returns>
    static int GetDistance(int2 pos1, int2 pos2)
    {
        int distX = math.abs(pos1.x - pos2.x);
        int distY = math.abs(pos1.y - pos2.y);

        if (distX > distY)
            return 14 * distY + 10 * (distX - distY);
        return 14 * distX + 10 * (distY - distX);
    }
}