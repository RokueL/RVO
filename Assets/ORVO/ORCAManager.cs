using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;


public class ORCAManager : MonoBehaviour
{
    public List<RVoDebugAgent> Agents;
    public float tau = 2.0f;
    public float orcaMix = 0.5f; // 0:RVO, 1:ORCA, 0.5:Hybrid
    
    public float separationDistance = 1.0f; // 원하는 최소 거리
    public float separationStrength = 2f; // 힘 세기

    private void Awake()
    {
        for (int i = 0; i < Agents.Count; i++)
        {
            Agents[i].Target = Agents[i].targets[0];
        }
    }

    void Update()
    {
        List<Vector3> velocities = new List<Vector3>();
        for(int i = 0; i < Agents.Count; i++)
        {
            Vector3 vNew = ComputeHybridRVOORCA(Agents[i], Agents, tau, orcaMix);
            
            velocities.Add(vNew);
        }

        for (int i = 0; i < Agents.Count; i++)
        {
            var target = Agents[i].targets[Agents[i].pathCount];
            if (math.distance(Agents[i].Transform.position, target) < 1f)
            {
                Agents[i].pathCount++;
                Agents[i].Target = Agents[i].targets[Agents[i].pathCount];
            }
        }
        
        for (int i = 0; i < Agents.Count; i++)
        {
            Agents[i].Velocity = velocities[i];
            Agents[i].Transform.position += velocities[i] * Time.deltaTime;
        }
        
        // foreach (var agent in Agents)
        // {
        //     Vector3 vNew = ComputeHybridRVOORCA(agent, Agents, tau, orcaMix);
        //
        //     agent.Velocity = vNew;
        //     agent.Transform.position += vNew * Time.deltaTime;
        // }
    }

    public Vector3 ComputeSeparation(RVoDebugAgent agent, List<RVoDebugAgent> allAgents, Vector3 orcaVector3)
    {
        // Separation force 추가
        Vector3 separation = Vector3.zero;
        foreach (var other in allAgents)
        {
            if (other == agent) continue;
            Vector3 diff = agent.Transform.position - other.Transform.position;
            float dist = diff.magnitude;
            if (dist > 0.01f && dist < separationDistance)
            {
                separation += diff.normalized * (separationDistance - dist) * separationStrength;
            }
        }

        Vector3 vFinal = orcaVector3 + separation;
        if (vFinal.magnitude > agent.MoveSpeed)
            vFinal = vFinal.normalized * agent.MoveSpeed;
        return vFinal;
    }

    public Vector3 ComputeHybridRVOORCA(RVoDebugAgent agent, List<RVoDebugAgent> allAgents, float tau, float orcaMix)
    {
        Vector3 vRVO = ComputeRVO(agent, allAgents);
        Vector3 vORCA = ComputeORCA(agent, allAgents, tau, vRVO);
        var result = Vector3.Lerp(vRVO, vORCA, orcaMix);
        
        return ComputeSeparation(agent,allAgents,result);
    }

    // 기존 RVO 로직 그대로
    public Vector3 ComputeRVO(RVoDebugAgent agent, List<RVoDebugAgent> allAgents)
    {
        var posA = agent.Transform.position;
        var velocityA = agent.Velocity;
        
        var vPref = (agent.Target - posA).normalized * agent.MoveSpeed;
        Vector3 vOpt = vPref;

        foreach (var other in allAgents)
        {
            if (other == agent) continue;
            var posB = other.Transform.position;
            var velocityB = other.Velocity;

            Vector3 relPos = posB - posA;
            float dist = relPos.magnitude;
            float combinedRadius = agent.Radius + other.Radius;

            if (dist < 0.001f || dist < combinedRadius) continue;

            Vector3 apex = other.isAgent ? (velocityA + velocityB) * 0.5f : Vector3.zero;

            Vector3 dir = relPos.normalized;
            float theta = Mathf.Asin(Mathf.Clamp(combinedRadius / dist, -1f, 1f));
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
            }
        }
        return vOpt;
    }
    
    // 우선순위 기반 책임 분배 비율 계산 함수
    float GetResponsibility(RVoDebugAgent self, RVoDebugAgent other)
    {
        // 우선순위 높을수록 덜 양보
        // 예: 자기 우선순위 / (자기+상대) 값 (0~1 사이)
        float pSelf = Mathf.Max(self.Priority, 1);
        float pOther = Mathf.Max(other.Priority, 1);
        return pOther / (pSelf + pOther); // 내 책임: 상대/(내+상대)
        // (반대로 하고 싶으면 return pSelf/(pSelf+pOther); 도 가능)
    }

    // ORCA 라인 분리, LinearProgram 사용
    public Vector3 ComputeORCA(RVoDebugAgent agent, List<RVoDebugAgent> allAgents, float tau, Vector3 vOpt)
    {
        var posA = agent.Transform.position;
        var velocityA = agent.Velocity;
        float maxSpeed = agent.MoveSpeed;

        List<ORCALine> orcaLines = new List<ORCALine>();

        foreach (var other in allAgents)
        {
            if (other == agent) continue;

            var posB = other.Transform.position;
            var velocityB = other.Velocity;
            float combinedRadius = agent.Radius + other.Radius;
            Vector3 relPos = posB - posA;
            Vector3 relVel = velocityA - velocityB;
            float dist = relPos.magnitude;

            Vector3 u, n;

            if (dist < combinedRadius)
            {
                // 이미 겹치면 빠져나가는 방향
                n = relPos.normalized;
                u = (combinedRadius - dist) / tau * n;
            }
            else
            {
                Vector3 dir = relPos.normalized;
                float theta = Mathf.Asin(Mathf.Clamp(combinedRadius / dist, -1f, 1f));
                Vector3 relVelProj = relVel - dir * Vector3.Dot(relVel, dir);
                n = relVelProj.sqrMagnitude > 0.001f ? relVelProj.normalized : dir;
                u = (combinedRadius / tau - relVelProj.magnitude) * n;
            }

            Vector3 planePoint = velocityA + 0.5f * u;
            orcaLines.Add(new ORCALine(planePoint, n));
        }

        // LinearProgram으로 half-plane 제약 내에서 vOpt와 가장 가까운 속도 계산
        return LinearProgram.Solve(orcaLines, vOpt, maxSpeed);
    }


}

// 🟦 ORCA Half-plane(제약) 표현
public class ORCALine
{
    public Vector3 point;    // half-plane을 만드는 점
    public Vector3 normal;   // half-plane의 법선

    public ORCALine(Vector3 point, Vector3 normal)
    {
        this.point = point;
        this.normal = normal.normalized;
    }

    // 주어진 벡터가 half-plane 내부(= ORCA 제약 만족)인지 검사
    public bool IsInside(Vector3 v)
    {
        return Vector3.Dot(v - point, normal) >= 0f;
    }

    // 점 v를 이 제약에 투영 (제약 만족하도록 보정)
    public Vector3 Project(Vector3 v)
    {
        float d = Vector3.Dot(v - point, normal);
        if (d >= 0) return v; // 이미 만족
        return v - d * normal;
    }
}

// 🟩 2D Linear Programming - half-plane 교집합 내에서 목표점에 가장 가까운 점 찾기
public static class LinearProgram
{
    // halfPlanes: 모든 ORCA 제약선, vPref: 선호속도(목표점), maxSpeed: 최대 속도
    public static Vector3 Solve(List<ORCALine> halfPlanes, Vector3 vPref, float maxSpeed)
    {
        // 속도 제한 내에서 시작
        Vector3 result = vPref.magnitude > maxSpeed ? vPref.normalized * maxSpeed : vPref;

        // 한 제약씩 검사하면서 안쪽에 있도록 보정
        foreach (var hp in halfPlanes)
        {
            if (!hp.IsInside(result))
            {
                result = hp.Project(result);
            }
        }

        // 속도 제한 한 번 더 보정
        if (result.magnitude > maxSpeed)
            result = result.normalized * maxSpeed;

        return result;
    }
}

