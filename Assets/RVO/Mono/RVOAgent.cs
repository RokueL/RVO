using UnityEngine;

public class RVOAgent : MonoBehaviour
{
    public Transform agentA;
    public Transform agentB;
    public Vector3 goalA;
    public Vector3 goalB;

    public float speed = 2.0f;
    public float radius = 0.5f;
    public float avoidanceRadius = 2.0f; // 👈 회피 적용 범위

    private Vector3 velocityA;
    private Vector3 velocityB;

    void Update()
    {
        float dt = Time.deltaTime;

        // 목표 방향 계산
        Vector3 desiredVelocityA = (goalA - agentA.position).normalized * speed;
        Vector3 desiredVelocityB = (goalB - agentB.position).normalized * speed;

        // 상대 속도 및 위치
        Vector3 relPos = agentB.position - agentA.position;
        Vector3 relVel = velocityB - velocityA;
        Debug.Log($"relVel: {relVel} , agentB.position : {agentB.position} agentA.position {agentA.position}");

        Vector3 newVelocityA = desiredVelocityA;
        Vector3 newVelocityB = desiredVelocityB;

        // ❗ 가까이 왔을 때만 RVO 적용
        if (relPos.magnitude < avoidanceRadius)
        {
            Debug.Log($"agentB.position: {agentB.position} , agentA.position {agentA.position}");
            
            Vector3 rvoApexA = velocityA + 0.5f * relVel;
            Vector3 rvoApexB = velocityB - 0.5f * relVel;

            newVelocityA = GetSafeVelocity(rvoApexA, relPos, radius * 2, desiredVelocityA);
            newVelocityB = GetSafeVelocity(rvoApexB, -relPos, radius * 2, desiredVelocityB);
            
            //Debug.Log($"rvoApex : {rvoApexA}\n relPos : {relPos}\ndesired : {desiredVelocityA}\n relvel : {relVel}\nnewV : {newVelocityA}");
           // Debug.Log($"velocityA: {velocityA}, velocityB: {velocityB}");
        }

        // 이동
        velocityA = newVelocityA;
        velocityB = newVelocityB;
        

        agentA.position += velocityA * dt;
        agentB.position += velocityB * dt;
    }

    // RVO 충돌 회피 속도 선택
    Vector3 GetSafeVelocity(Vector3 rvoApex, Vector3 direction, float combinedRadius, Vector3 desiredVelocity)
    {
        float dist = direction.magnitude;
        if (dist <= combinedRadius) return desiredVelocity;

        Vector3 dir = direction.normalized;
        float angle = Mathf.Asin(combinedRadius / dist);
        Vector3 relDes = desiredVelocity - rvoApex;

        float angleToDesired = Vector3.SignedAngle(dir, relDes.normalized, Vector3.up) * Mathf.Deg2Rad;

        // 충돌 cone 안에 있음: 회피
        if (Mathf.Abs(angleToDesired) < angle)
        {
            Vector3 leftBound = Quaternion.AngleAxis(-angle * Mathf.Rad2Deg, Vector3.up) * dir;
            Vector3 rightBound = Quaternion.AngleAxis(angle * Mathf.Rad2Deg, Vector3.up) * dir;

            float leftDot = Vector3.Dot(relDes, leftBound);
            float rightDot = Vector3.Dot(relDes, rightBound);
            Vector3 bestDir = (leftDot > rightDot) ? leftBound : rightBound;

            return rvoApex + bestDir.normalized * relDes.magnitude;
        }
        else
        {
            return desiredVelocity;
        }
    }

    void OnDrawGizmos()
    {
        if (agentA == null || agentB == null) return;

        Gizmos.color = Color.red;
        Gizmos.DrawLine(agentA.position, agentA.position + velocityA);
        Gizmos.DrawWireSphere(goalA, 0.2f);

        Gizmos.color = Color.blue;
        Gizmos.DrawLine(agentB.position, agentB.position + velocityB);
        Gizmos.DrawWireSphere(goalB, 0.2f);

        // RVO cone (A 기준)
        Vector3 relPos = agentB.position - agentA.position;
        Vector3 relVel = velocityB - velocityA;
        Vector3 apex = velocityA + 0.5f * relVel;
        DrawVelocityObstacle(agentA.position, apex, relPos, radius * 2, Color.yellow);
    }

    void DrawVelocityObstacle(Vector3 origin, Vector3 apex, Vector3 direction, float combinedRadius, Color color)
    {
        float dist = direction.magnitude;
        if (dist <= combinedRadius) return;

        Vector3 dir = direction.normalized;
        float angle = Mathf.Asin(combinedRadius / dist) * Mathf.Rad2Deg;

        Quaternion leftRot = Quaternion.AngleAxis(-angle, Vector3.up);
        Quaternion rightRot = Quaternion.AngleAxis(angle, Vector3.up);

        Vector3 leftBound = leftRot * dir * 5f;
        Vector3 rightBound = rightRot * dir * 5f;

        Gizmos.color = color;
        Gizmos.DrawLine(origin + apex, origin + apex + leftBound);
        Gizmos.DrawLine(origin + apex, origin + apex + rightBound);
    }
}
