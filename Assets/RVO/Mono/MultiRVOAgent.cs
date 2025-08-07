using UnityEngine;
using System.Collections.Generic;

public class RVOManager : MonoBehaviour
{
    [Header("Agent Settings")]
    public List<Transform> agents;
    public List<Vector3> goals;
    public float radius = 0.5f;
    public float maxSpeed = 3f;
    public float neighborDist = 3f;

    private Vector3[] velocities;

    void Start()
    {
        if (agents.Count != goals.Count)
            Debug.LogError("Agents와 Goals의 수가 일치해야 합니다.");

        velocities = new Vector3[agents.Count];
    }

    void Update()
    {
        for (int i = 0; i < agents.Count; i++)
        {
            Transform agent = agents[i];
            Vector3 toGoal = goals[i] - agent.position;
            float distToGoal = toGoal.magnitude;

            if (distToGoal < 0.1f)
            {
                velocities[i] = Vector3.zero;
                continue;
            }

            Vector3 preferredVelocity = toGoal.normalized * maxSpeed;
            preferredVelocity = AvoidCrowdingSameDirection(i, preferredVelocity);

            // ✅ 주변에 이웃이 있는지 검사
            bool hasNeighbor = false;
            for (int j = 0; j < agents.Count; j++)
            {
                if (i == j) continue;
                float dist = Vector3.Distance(agents[i].position, agents[j].position);
                if (dist < neighborDist)
                {
                    hasNeighbor = true;
                    break;
                }
            }

            // ✅ 이웃이 있으면 RVO 계산, 없으면 그냥 목표 방향
            velocities[i] = hasNeighbor
                ? ComputeRVOVelocity(i, preferredVelocity)
                : preferredVelocity;
        }

        // 이동
        for (int i = 0; i < agents.Count; i++)
            agents[i].position += velocities[i] * Time.deltaTime;
    }


    Vector3 ComputeRVOVelocity(int agentIndex, Vector3 preferredVelocity)
    {
        Vector3 myPos = agents[agentIndex].position;
        Vector3 bestVelocity = preferredVelocity;
        float minPenalty = float.MaxValue;

        int samples = 30;
        for (int s = 0; s < samples; s++)
        {
            float angle = (360f / samples) * s;
            Vector3 dir = Quaternion.Euler(0, angle, 0) * Vector3.forward;
            Vector3 sampleVel = dir * maxSpeed;

            float penalty = (sampleVel - preferredVelocity).sqrMagnitude;

            for (int j = 0; j < agents.Count; j++)
            {
                if (j == agentIndex) continue;

                Vector3 relPos = agents[j].position - myPos;
                Vector3 relVel = sampleVel - velocities[j];
                float combinedRadius = radius * 2;
                float ttc = ComputeTimeToCollision(relPos, relVel, combinedRadius);

                float priorityWeight = (agentIndex < j) ? 1.0f : 0.0f; // 내가 우선순위 높으면 가중치 줄이기

                if (ttc > 0 && ttc < 2f)
                    penalty += (2f - ttc) * 10f * priorityWeight;
            }

            if (penalty < minPenalty)
            {
                minPenalty = penalty;
                bestVelocity = sampleVel;
            }
        }

        return bestVelocity;
    }

    float ComputeTimeToCollision(Vector3 relPos, Vector3 relVel, float combinedRadius)
    {
        float a = Vector3.Dot(relVel, relVel);
        if (a == 0) return float.MaxValue;

        float b = Vector3.Dot(relPos, relVel);
        float c = Vector3.Dot(relPos, relPos) - combinedRadius * combinedRadius;
        float discr = b * b - a * c;

        if (discr < 0) return float.MaxValue;

        float t = (b - Mathf.Sqrt(discr)) / a;
        return t < 0 ? float.MaxValue : t;
    }
    
    Vector3 AvoidCrowdingSameDirection(int selfIndex, Vector3 preferredVelocity)
    {
        Vector3 myPos = agents[selfIndex].position;
        Vector3 myDir = preferredVelocity.normalized;
        float mySpeed = preferredVelocity.magnitude;

        int nearbyCount = 0;
        Vector3 avoidance = Vector3.zero;

        for (int j = 0; j < agents.Count; j++)
        {
            if (j == selfIndex) continue;

            Vector3 otherPos = agents[j].position;
            Vector3 toOther = otherPos - myPos;
            float dist = toOther.magnitude;
            if (dist > neighborDist) continue;

            Vector3 otherGoalDir = (goals[j] - otherPos).normalized;

            // 👇 같은 방향 유사도 비교 (코사인 유사도 사용)
            float alignment = Vector3.Dot(myDir, otherGoalDir); // 1 = 같은 방향
            if (alignment > 0.95f) // 거의 같은 방향
            {
                // 👉 가까우면 회피 대상
                if (dist < 1.0f)
                {
                    avoidance -= toOther.normalized / dist;
                    nearbyCount++;
                }
            }
        }

        if (nearbyCount > 0)
        {
            Vector3 offsetDir = (myDir + avoidance.normalized * 0.5f).normalized;
            float reducedSpeed = mySpeed * 0.8f; // 살짝 감속
            return offsetDir * reducedSpeed;
        }

        return preferredVelocity;
    }

    void OnDrawGizmos()
    {
        if (agents == null || goals == null) return;
        Gizmos.color = Color.yellow;

        for (int i = 0; i < agents.Count; i++)
        {
            if (agents[i] == null) continue;

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(agents[i].position, radius);

            if (Application.isPlaying)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(agents[i].position, agents[i].position + velocities[i]);

                Gizmos.color = Color.red;
                Gizmos.DrawLine(agents[i].position, goals[i]);
            }

            Gizmos.color = new Color(1, 0.5f, 0f, 0.2f);
            Gizmos.DrawWireSphere(agents[i].position, neighborDist);
        }
    }
}
