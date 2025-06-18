using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

public struct Agent : IComponentData
{
    public float3 Target;
    public float MoveSpeed;
    public float Radius;
}

public struct Velocity : IComponentData
{
    public float3 Value;
}

public struct ORCALine
{
    public float3 point;
    public float3 normal;
}

[BurstCompile]
[UpdateAfter(typeof(b))]
public partial struct ORCAAgentSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        var entityQuery = SystemAPI.QueryBuilder()
            .WithAll<Agent, Velocity, LocalTransform>()
            .Build();

        var units = entityQuery.ToComponentDataArray<Agent>(Allocator.TempJob);
        var transforms = entityQuery.ToComponentDataArray<LocalTransform>(Allocator.TempJob);
        var velocities = entityQuery.ToComponentDataArray<Velocity>(Allocator.TempJob);
        var entities = entityQuery.ToEntityArray(Allocator.TempJob);

        var outputVelocities = new NativeArray<float3>(entities.Length, Allocator.TempJob);

        var job = new ORCAParallelJob
        {
            Units = units,
            Transforms = transforms,
            Velocities = velocities,
            OutputVelocities = outputVelocities
        };

        var handle = job.Schedule(entities.Length, 32);

        handle.Complete();

        for (int i = 0; i < entities.Length; i++)
        {
            state.EntityManager.SetComponentData(entities[i], new Velocity { Value = outputVelocities[i] });
        }

        units.Dispose();
        transforms.Dispose();
        velocities.Dispose();
        entities.Dispose();
        outputVelocities.Dispose();
    }

    [BurstCompile]
    public struct ORCAParallelJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<Agent> Units;
        [ReadOnly] public NativeArray<LocalTransform> Transforms;
        [ReadOnly] public NativeArray<Velocity> Velocities;
        [WriteOnly] public NativeArray<float3> OutputVelocities;

        public void Execute(int index)
        {
            var myUnit = Units[index];
            var myTransform = Transforms[index].Position;
            var myVelocity = Velocities[index].Value;

            var prefVel = math.normalize(myUnit.Target - myTransform) * myUnit.MoveSpeed;

            var orcaLines = new NativeList<ORCALine>(Allocator.Temp);

            for (int i = 0; i < Units.Length; i++)
            {
                if (i == index) continue;
                ComputeORCALine(myTransform, myVelocity, Transforms[i].Position, Velocities[i].Value, myUnit.Radius, 3f, ref orcaLines);
            }

            float3 result = SolveORCA(orcaLines, prefVel, myUnit.MoveSpeed);
            OutputVelocities[index] = result;

            orcaLines.Dispose();
        }

        float3 ProjectPointToLine(ORCALine line, float3 point, float maxSpeed)
        {
            float3 proj = point - math.dot(point - line.point, line.normal) * line.normal;
            proj.y = 0;
            if (math.length(proj) > maxSpeed)
                return math.normalize(proj) * maxSpeed;
            return proj;
        }

        float3 SolveORCA(NativeList<ORCALine> lines, float3 preferred, float maxSpeed)
        {
            float3 result = preferred;
            if (math.length(result) > maxSpeed)
                result = math.normalize(result) * maxSpeed;

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                float dist = math.dot(line.normal, result - line.point);
                if (dist < 0f) continue;
                result = ProjectPointToLine(line, result, maxSpeed);
            }

            return result;
        }

        void ComputeORCALine(
            float3 myPos, float3 myVel,
            float3 otherPos, float3 otherVel,
            float radius, float timeHorizon,
            ref NativeList<ORCALine> output)
        {
            float3 relPos = otherPos - myPos;
            float3 relVel = myVel - otherVel;

            float distSq = math.lengthsq(relPos);
            float combinedRadius = radius * 2;
            float combinedRadiusSq = combinedRadius * combinedRadius;

            float3 u;
            float3 normal;

            if (distSq > combinedRadiusSq)
            {
                float dist = math.sqrt(distSq);
                float3 w = relVel - (relPos / timeHorizon);
                normal = math.normalizesafe(new float3(-relPos.z, 0, relPos.x));
                if (math.dot(w, relPos) < 0 || math.dot(w, w) < 1e-6f)
                    normal = math.normalizesafe(new float3(-w.z, 0, w.x));

                u = math.dot(w, normal) * normal;
            }
            else
            {
                normal = math.normalizesafe(new float3(-relPos.z, 0, relPos.x));
                u = (combinedRadius - math.length(relPos)) * normal;
            }

            ORCALine line;
            line.point = myVel + 0.5f * u;
            line.normal = normal;
            output.Add(line);
        }
    }
}
