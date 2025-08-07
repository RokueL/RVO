using UnityEditor;
using UnityEngine;

public class ORVODebugger : MonoBehaviour
{
    public RVoDebugAgent agentA;
    public RVoDebugAgent agentB;
    public bool isActive;

    void OnDrawGizmos()
    {
        if(!isActive) return;
        if (agentA == null || agentB == null) return;
        var posA = agentA.Transform.position;
        var posB = agentB.Transform.position;
        var targetA = agentA.Target;
        var targetB = agentB.Target;

        // 반지름
        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(posA, agentA.Radius);
        Gizmos.DrawWireSphere(posB, agentA.Radius);

        // 목적지
        Gizmos.color = Color.red;
        GUI.color = Color.red;
        Handles.Label(targetA, "목적지 A");
        Gizmos.DrawSphere(targetA, agentA.Radius / 2f);
        Gizmos.color = Color.blue;
        GUI.color = Color.blue;
        Handles.Label(targetB, "목적지 B");
        Gizmos.DrawSphere(targetB, agentA.Radius / 2f);

        // 속도 벡터
        var velocityA = (agentA.Target - posA).normalized * agentA.MoveSpeed;
        var velocityB = (agentB.Target - posB).normalized * agentB.MoveSpeed;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(posA, posA + velocityA);
        Gizmos.color = Color.magenta;
        Gizmos.DrawLine(posB, posB + velocityB);

        // apex
        Gizmos.color = Color.purple;
        var apex = (velocityA + velocityB) / 2f;
        var apexA = apex + posA;
        Gizmos.DrawSphere(apexA, 0.1f);
        GUI.color = Color.purple;
        Handles.Label(apexA, "RVO 콘");

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

            // 콘 양쪽 선 그리기
            Gizmos.color = Color.pink;
            Gizmos.DrawLine(apexA, apexA + leftDir * lineLength);
            Gizmos.DrawLine(apexA, apexA + rightDir * lineLength);
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

        // (기존 코드 내 OnDrawGizmos에 추가)
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(posA, posA + velocityA); // 내가 원하는 속도 (목표 방향, 예: 하늘색)

        Gizmos.color = Color.green;
        Gizmos.DrawLine(posA, posA + vOpt); // 최적 안전 속도(클램프 결과, 예: 초록색)

        // 그냥 경고 표시
        if (angle < theta)
        {
            // 충돌 경로(경고)
            Handles.color = Color.red;
            Handles.DrawSolidDisc(posA + velocityA, Vector3.up, 0.1f);
        }
        else
        {
            // 안전(경계 바깥)
            Handles.color = Color.green;
            Handles.DrawSolidDisc(posA + velocityA, Vector3.up, 0.1f);
        }
        
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

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(posA + orcaPoint - perp * lineLen, posA + orcaPoint + perp * lineLen); // half-plane 경계
        Handles.Label(posA + orcaPoint, "ORCA 경계");

        Gizmos.color = Color.green;
        Gizmos.DrawLine(posA, posA + orcaPoint); // ORCA 기준점까지
        GUI.color = Color.cyan;
        Handles.Label(posA + new Vector3(0, 0.5f, 0), $"원래 속도\n{(velocityA).normalized}");
        GUI.color = Color.green;
        Handles.Label(posA + orcaPoint + new Vector3(0, 0.5f, 0), $"ORCA 속도\n{orcaPoint.normalized}");
    }
}