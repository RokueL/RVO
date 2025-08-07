using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public enum MoveType
{
    JustMove,
    RVOMove,
    ORCAMove
}

public class ORVODebuggerManager : MonoBehaviour
{
    public RVoDebugAgent agentA;
    public RVoDebugAgent agentB;
    public MoveType MoveType;

    public void Update()
    {
        switch (MoveType)
        {
            case MoveType.JustMove:
                agentA.Transform.position += (agentA.Target - agentA.Transform.position).normalized * agentA.MoveSpeed * Time.deltaTime;
                agentB.Transform.position += (agentB.Target - agentB.Transform.position).normalized * agentB.MoveSpeed * Time.deltaTime;
                break;
            case MoveType.RVOMove:
                agentA.Transform.position += ComputeRVO(agentA,agentB) * agentA.MoveSpeed * Time.deltaTime;
                Debug.Log($"{ComputeRVO(agentA,agentB)}");
                agentB.Transform.position += ComputeRVO(agentB,agentA) * agentB.MoveSpeed * Time.deltaTime;
                break;
            case MoveType.ORCAMove:        
                Debug.DrawLine(agentA.Transform.position,agentA.Transform.position+new Vector3(0,0,0.01f),Color.red,1000f);
                Debug.DrawLine(agentB.Transform.position,agentB.Transform.position+new Vector3(0,0,0.01f),Color.blue,1000f);
                Debug.Log($"{ComputeORVO(agentA,agentB)}");
                agentA.Transform.position += ComputeORVO(agentA,agentB) * agentA.MoveSpeed * Time.deltaTime;
                agentB.Transform.position += ComputeORVO(agentB,agentA) * agentB.MoveSpeed * Time.deltaTime;
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
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
        float combinedRadius = agentA.Radius + agentB.Radius * 1.5f;
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
    
    public Vector3 ComputeORVO(RVoDebugAgent agentA, RVoDebugAgent agentB)
    {
        var posA = agentA.Transform.position;
        var posB = agentB.Transform.position;
        var targetA = agentA.Target;
        var targetB = agentB.Target;

        // 속도 벡터
        var velocityA = (agentA.Target - posA).normalized * agentA.MoveSpeed;
        var velocityB = (agentB.Target - posB).normalized * agentB.MoveSpeed;

        // apex
        var apex = (velocityA + velocityB) / 2f;
        var apexA = apex + posA;

        // VO 콘 그리기 (posA 기준)
        Vector3 relPos = posB - posA;
        float combinedRadius = agentA.Radius + agentB.Radius;
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
        

        // 1️⃣ u 벡터 구하기
        Vector3 v_rel = velocityA - velocityB; // 상대속도
        Vector3 relPoss = posB - posA;
        float dists = relPoss.magnitude;
        float combinedRadiuss = agentA.Radius + agentB.Radius;

        // VO 콘 각 (theta)
        float thetas = Mathf.Asin(Mathf.Clamp(combinedRadiuss / dists, -1f, 1f));

        // pref 속도가 콘 내부인지 판단
        float angles = Vector3.Angle(v_rel, relPoss);
        bool insideVO = (angles < thetas * Mathf.Rad2Deg);

        // VO 경계에서 (vA-vB)로 가장 가까운 점 projection
        Vector3 dirs = relPoss.normalized;
        Vector3 boundaryDirs;
        float signs = Mathf.Sign(Vector3.Dot(Vector3.Cross(dir, v_rel), Vector3.up));
        if (signs < 0)
            boundaryDirs = Quaternion.AngleAxis(-thetas * Mathf.Rad2Deg, Vector3.up) * dir;
        else
            boundaryDirs = Quaternion.AngleAxis(thetas * Mathf.Rad2Deg, Vector3.up) * dir;

        Vector3 boundaryVel = boundaryDirs * v_rel.magnitude; // pref와 같은 크기로

        // u 계산
        Vector3 u = Vector3.zero;
        if (insideVO)
            u = boundaryVel - v_rel; // 내부면 projection
        // 바깥이면 u=0 (ORCA에서는 보통 0벡터 처리)

        // 2️⃣ ORCA half-plane 경계 구하기
        // ORCA 경계의 기준점: vA(opt) + 0.5u
        Vector3 orcaPoint = velocityA + 0.5f * u; // 원점 기준이면 apexA+0.5u, 여기선 vA(opt) 기준

        // ORCA 경계선 방향: u에 수직 (수평면 기준이면 Vector3.Cross(u, Vector3.up))
        Vector3 n = u.normalized;
        Vector3 perp = Vector3.Cross(n, Vector3.up); // u가 0이면 별 의미 없음

        // 3️⃣ Gizmo로 ORCA 경계 half-plane 그리기
        float lineLen = agentA.MoveSpeed * 3f; // 보기 좋게 충분히 길게

        return orcaPoint;
    }


}