using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class RVODebuggerManager : MonoBehaviour
{
    public RVoDebugAgent agentA;
    public RVoDebugAgent agentB;
    public List<RVoDebugAgent> AllAgent;
    public MoveType MoveType;

    public void Start()
    {
        for (int i = 0; i < AllAgent.Count; i++)
        {
            if(AllAgent[i].isAgent)
                AllAgent[i].Priority = i;
        }
    }

    public void Update()
    {
        switch (MoveType)
        {
            case MoveType.JustMove:
                agentA.Transform.position += ComputeRVO(agentA,agentB) * agentA.MoveSpeed * Time.deltaTime;
                Debug.Log($"{ComputeRVO(agentA,agentB).normalized}");
                agentB.Transform.position += ComputeRVO(agentB,agentA) * agentB.MoveSpeed * Time.deltaTime;
                break;
            case MoveType.RVOMove:
                foreach (var agent in AllAgent)
                {
                    if(agent.isAgent)
                        agent.Transform.position += ComputeRVO(agent, AllAgent) * agent.MoveSpeed * Time.deltaTime;
                }
                break;
            case MoveType.ORCAMove:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

    }
    
    public Vector3 ComputeRVO(RVoDebugAgent agent, List<RVoDebugAgent> allAgents)
    {
        var posA = agent.Transform.position;
        var targetA = agent.Target;
        var velocityA = (targetA - posA).normalized * agent.MoveSpeed;

        // 1. 모든 이웃에 대해 RVO cone 계산
        Vector3 vOpt = velocityA; // 기본 목표
        float desiredSeparation = agent.Radius * 3f; // 원하는 최소 거리
        foreach (var other in allAgents)
        {
            var posB = other.Transform.position;
            var targetB = other.Target;
            var velocityB = (targetB - posB).normalized * other.MoveSpeed;

            Vector3 relPos = posB - posA;
            float dist = relPos.magnitude;
            float combinedRadius = agent.Radius + other.Radius;
            
            
            // ⏩ 같은 방향 & 가까우면 separation force 적용
            // float dirSimilarity = Vector3.Dot(velocityA.normalized, velocityB.normalized);
            //
            // bool isSimilar = dirSimilarity > 0.8f;    // 같은 방향
            // bool isOpposite = dirSimilarity < -0.8f;  // 거의 정반대
            //
            // if ((isSimilar || isOpposite) && dist < desiredSeparation)
            // {
            //     // Priority 낮은 쪽만 force 적용
            //     if (agent.Priority > other.Priority)
            //     {
            //         Vector3 awayDir = (posA - posB).normalized;
            //         float force = (desiredSeparation - dist) * 0.2f;
            //         vOpt += awayDir * force;
            //     }
            // }
            

            if (dist < 0.001f || dist < combinedRadius) continue; // 너무 가까우면 skip
            
            // RVO apex
            Vector3 apex;
            // 장애물일 경우 처리
            if (other.isAgent)
            {
                apex = (velocityA + velocityB) * 0.5f;
            }
            else
            {
                apex = Vector3.zero;
            }

            Vector3 dir = relPos.normalized;
            float theta = Mathf.Asin(Mathf.Clamp(combinedRadius / dist, -1f, 1f));
            
            // 내 속도가 RVO 내부인지 검사
            Vector3 vRel = vOpt - apex;
            float cosAngle = Vector3.Dot(vRel.normalized, dir); 
            float angle = Mathf.Acos(Mathf.Clamp(cosAngle, -1f, 1f));

            if (angle < theta)
            {
                float len = vRel.magnitude;
                Vector3 left = Quaternion.AngleAxis(-theta * Mathf.Rad2Deg, Vector3.up) * dir;
                Vector3 right = Quaternion.AngleAxis(theta * Mathf.Rad2Deg, Vector3.up) * dir;

                float sign = Mathf.Sign(Vector3.Dot(Vector3.Cross(dir, vRel), Vector3.up));
                Vector3 boundaryDir = (sign < 0) ? left : right;
                vOpt = apex + boundaryDir * len;
                // 만약 여러 cone에 걸치면 마지막 처리된 cone 기준으로 덮어씀 (greedy 방식)
            }
        }
        

        return vOpt;
    }

    public Vector3 ComputeRVO(RVoDebugAgent agentA, RVoDebugAgent agentB)
    {
        var posA = agentA.Transform.position;
        var posB = agentB.Transform.position;
        var targetA = agentA.Target;
        var targetB = agentB.Target;
        // 속도 벡터
        var velocityA = (targetA - posA).normalized * agentA.MoveSpeed;
        var velocityB = (targetB - posB).normalized * agentB.MoveSpeed;
        // VO 꼭짓점
        var apex = (velocityA + velocityB) / 2f;
        
        // VO 콘 그리기 (posA 기준)
        Vector3 relPos = posB - posA;
        float combinedRadius = agentA.Radius + agentB.Radius*1.5f;
        float dist = relPos.magnitude;
        if (dist > 0.001f && dist > combinedRadius)
        {
            // 각도 구하기 (asin(r / d))
            float angleRad = Mathf.Asin(combinedRadius / dist);
            float angleDeg = angleRad * Mathf.Rad2Deg;
            Vector3 voDir = relPos.normalized;

            // 콘의 좌/우 방향
            Vector3 leftDir = Quaternion.AngleAxis(-angleDeg, Vector3.up) * voDir;
            Vector3 rightDir = Quaternion.AngleAxis(angleDeg, Vector3.up) * voDir;

            float lineLength = dist * 2f; // 길이 적당히 늘려서
            
        }
        //  상대 위치/방향
        Vector3 dir = (posB - posA).normalized;
        // 세타   VO 영역에서의 각도 구하기 (반각도임)
        float theta = Mathf.Asin(Mathf.Clamp((agentA.Radius + agentB.Radius) / dist, -1f, 1f));
        //  내 속도가 RVO 내부인지 검사
        Vector3 vRel = velocityA - apex;
        // 내적
        float cosAngle = Vector3.Dot(vRel.normalized, dir);
        // 역코사인 ( 코사인 1은 최대 -1은 최소 0은 0 )
        float angle = Mathf.Acos(Mathf.Clamp(cosAngle, -1f, 1f));
        // 만약 내부라면, 경계로 projection (최적 속도 구하기)
        Vector3 vOpt = velocityA;
        if (angle < theta)
        {
            // pref 방향을 apex 기준으로 정렬
            float len = vRel.magnitude;
            // 좌/우 경계 방향 벡터
            Vector3 left = Quaternion.AngleAxis(-theta * Mathf.Rad2Deg, Vector3.up) * dir;
            Vector3 right = Quaternion.AngleAxis(theta * Mathf.Rad2Deg, Vector3.up) * dir;

            // pref와 dir의 각도가 +면 right, -면 left 경계에 붙임
            float sign = Mathf.Sign(Vector3.Dot(Vector3.Cross(dir, vRel), Vector3.up));
            Vector3 boundaryDir = (sign < 0) ? left : right;

            vOpt = apex + boundaryDir * len; // 같은 크기로 경계에 클램프
        }

        return vOpt;
    }
    
}
