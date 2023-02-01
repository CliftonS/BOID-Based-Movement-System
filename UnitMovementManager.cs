using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Jobs;
using Unity.Transforms;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Collections;

public struct Unit : IComponentData
{
    public ArmyEnum armyEnum;
    public SizeEnum sizeEnum;
    public int squad;
    public int id;

    public enum ArmyEnum
    {
        PlayerArmy,
        AIArmy
    }

    public enum SizeEnum
    {
        Small,
        Medium,
        Large,
        Huge
    }
}

public struct AdditionalMoveData : IComponentData
{
    //public Quaternion rotation; - Now part of localtoworld instead of using just transform
    public float lastVelocity;
    public float3 target;
    public int pathNode;
}

public struct GridLocationSystem : IComponentData
{
    public int gridX;
    public int gridY;
    public float gridHeight;
}

public struct EntityWithPosition : IComponentData
{
    public Entity entity;
    public float3 position;
}

public struct QuadrantData : IComponentData
{
    public Entity entity;
    public float3 position;
    public Unit unit;
}

public struct MoveData : IComponentData
{
    public float speed;
    public float angle;
}

[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
public class UnitMovementManagerSystem : ComponentSystem
{

    public static void Init()
    {
        //quadrantDataHashMap = new NativeMultiHashMap<int, QuadrantData>(0, Allocator.Persistent);
        //moveDataArray = new NativeArray<MovementWeightData>(2000, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
    }

    private const int quadrantZMultiplier = 1000;
    private const int quadrantCellSize = 10;
    private const int maxUnitNumber = 12000;

    private static int GetPositionHashMapKey(float3 position)
    {
        return (int)(math.floor(position.x / quadrantCellSize) + (quadrantZMultiplier * math.floor(position.z / quadrantCellSize)));
    }

    private static int GetNearestPositionHashMapKey(float3 position)
    {
        float modx = position.x - (int)position.x;
        float modz = position.z - (int)position.z;
        return GetPositionHashMapKey(position) + (modx < modz ? ((1 - modx) > modz ? -1 : 1000) : ((1 - modx) < modz ? 1 : -1000));
    }

    private struct SetQuadrantDataHashMapJob : IJobForEachWithEntity<LocalToWorld, Unit>
    {
        public NativeMultiHashMap<int, QuadrantData>.ParallelWriter nativeMultiHashMap;
        public NativeArray<int> unitKeyArray;
        public NativeArray<int> unitNearKeyArray;
        public void Execute(Entity entity, int index, ref LocalToWorld localToWorld, ref Unit unit)
        {
            int hashMapKey = GetPositionHashMapKey(localToWorld.Position);
            nativeMultiHashMap.Add(hashMapKey, new QuadrantData
            {
                entity = entity,
                position = localToWorld.Position,
                unit = unit
            });
            unitKeyArray[index] = hashMapKey;

            hashMapKey = GetNearestPositionHashMapKey(localToWorld.Position);
            unitNearKeyArray[index] = hashMapKey;
        }

    }

    private static int GetEntityCountInHashMap(NativeMultiHashMap<int, QuadrantData> quadrantDataHashMap, int quadrantHashMapKey)
    {
        QuadrantData quadrantData;
        NativeMultiHashMapIterator<int> nativeMultiHashMapIterator;
        int count = 0;
        if (quadrantDataHashMap.TryGetFirstValue(quadrantHashMapKey, out quadrantData, out nativeMultiHashMapIterator))
        {
            do
            {
                count++;
            } while (quadrantDataHashMap.TryGetNextValue(out quadrantData, ref nativeMultiHashMapIterator));
        }
        return count;
    }

    private struct WriteDataToBufferJob : IJobForEachWithEntity<LocalToWorld, Unit>
    {
        public NativeArray<float3> unitPositionBuffer;
        public NativeArray<Unit> unitInfoBuffer;

        public void Execute(Entity entity, int index, [ReadOnly] ref LocalToWorld localToWorld, [ReadOnly] ref Unit unit)
        {
            unitPositionBuffer[index] = localToWorld.Position;
            unitInfoBuffer[index] = unit;
        }
    }

    private struct SumSquadPositionAndNumberJob : IJobForEachWithEntity<LocalToWorld, Unit>
    {
        public NativeArray<float3> squadPositionSum;
        public NativeArray<int> squadNumber;

        public void Execute(Entity boid, int boidIndex, [ReadOnly] ref LocalToWorld localToWorld, [ReadOnly] ref Unit unit)
        {
            squadPositionSum[unit.squad] += localToWorld.Position;
            squadNumber[unit.squad] += 1;
        }
    }

    [BurstCompile]
    private struct SetAvoidFromHashMapJob : IJobForEachWithEntity<LocalToWorld, Unit>
    {
        [ReadOnly] public NativeMultiHashMap<int, QuadrantData> quadrantDataHashMap;
        public NativeArray<float3> moveAvoidArray;
        public NativeArray<int> cellAvoidWeight;
        public NativeArray<int> unitKeyArray;
        public NativeArray<int> unitNearKeyArray;

        public void Execute(Entity entity, int index, [ReadOnly] ref LocalToWorld localToWorld, [ReadOnly] ref Unit unit)
        {
            float3 avoid = float3.zero;
            int avoidWeight = 0;
            QuadrantData targetQuadrantData;
            NativeMultiHashMapIterator<int> nativeMultiHashMapIterator;
            if (quadrantDataHashMap.TryGetFirstValue(unitKeyArray[index], out targetQuadrantData, out nativeMultiHashMapIterator))
            {
                do
                {
                    if (unit.id != targetQuadrantData.unit.id)
                    {
                        float distance = Vector3.Distance(targetQuadrantData.position, localToWorld.Position);
                        if (distance < 1f)
                        {
                            avoid += targetQuadrantData.position * ((int)targetQuadrantData.unit.sizeEnum * 2);
                            avoidWeight += (int)targetQuadrantData.unit.sizeEnum * 2;
                        }
                        else if (distance < 2f && (int)targetQuadrantData.unit.sizeEnum > 1)
                        {
                            avoid += targetQuadrantData.position * ((int)targetQuadrantData.unit.sizeEnum * 4);
                            avoidWeight += (int)targetQuadrantData.unit.sizeEnum * 4;
                        }
                    }
                } while (quadrantDataHashMap.TryGetNextValue(out targetQuadrantData, ref nativeMultiHashMapIterator));
            }

            if (quadrantDataHashMap.TryGetFirstValue(unitNearKeyArray[index], out targetQuadrantData, out nativeMultiHashMapIterator))
            {
                do
                {
                    float distance = Vector3.Distance(targetQuadrantData.position, localToWorld.Position);
                    if (distance < 1f)
                    {
                        avoid += targetQuadrantData.position * ((int)targetQuadrantData.unit.sizeEnum * 2);
                        avoidWeight += (int)targetQuadrantData.unit.sizeEnum * 2;
                    }
                    else if (distance < 2f && (int)targetQuadrantData.unit.sizeEnum > 1)
                    {
                        avoid += targetQuadrantData.position * ((int)targetQuadrantData.unit.sizeEnum * 4);
                        avoidWeight += (int)targetQuadrantData.unit.sizeEnum * 4;
                    }
                } while (quadrantDataHashMap.TryGetNextValue(out targetQuadrantData, ref nativeMultiHashMapIterator));
            }
            avoid.y = 0;
            moveAvoidArray[index] = avoid;
            cellAvoidWeight[index] = avoidWeight;
        }
    }

    [BurstCompile]
    private struct MoveUnits : IJobForEachWithEntity<LocalToWorld, Unit, AdditionalMoveData>
    {

        [ReadOnly] public float deltaTime;
        [ReadOnly] public float unitMaxSpeed;

        [DeallocateOnJobCompletion][ReadOnly] public NativeArray<float3> positionAvoidSumOfCells;
        [DeallocateOnJobCompletion][ReadOnly] public NativeArray<float3> positionCohesionSumOfCells;

        [DeallocateOnJobCompletion][ReadOnly] public NativeArray<int> cellAvoidWeight;
        [DeallocateOnJobCompletion][ReadOnly] public NativeArray<int> cellCohesionWeight;

        [ReadOnly] public NativeArray<GameHandler.SquadPath> squadPaths;

        //TODO: update to local to world from translation, keep current direction, have direction + rotation, min of speed, adjusted direction speed
        public void Execute(Entity boid, int unitIndex, ref LocalToWorld localToWorld, ref Unit unit, ref AdditionalMoveData additionalMoveData)
        {

            float3 unitPosition = localToWorld.Position;
            //int cellIndex = unit.id;//cellIndices[boidIndex];

            //int nearbyBoidCount = 1;//cellBoidCount[cellIndex] - 1;
            float3 positionAvoidSum = positionAvoidSumOfCells[unitIndex];
            float3 positionCohesionSum = positionCohesionSumOfCells[unit.squad];
            //float3 headingSum = headingSumsOfCells[cellIndex] - localToWorld.Forward;

            int avoidWeight = cellAvoidWeight[unitIndex];
            int cohesionWeight = 1;//cellCohesionWeight[unit.squad];

            float3 force = float3.zero;

            if (avoidWeight > 0)
            {
                float3 averagePosition = positionAvoidSum / avoidWeight;

                float distToAveragePositionSq = math.lengthsq(averagePosition - unitPosition);
                float maxDistToAveragePositionSq = 2;

                float distanceNormalized = distToAveragePositionSq / maxDistToAveragePositionSq;
                float needToLeave = math.max(1 - distanceNormalized, 0f);

                float3 toAveragePosition = math.normalizesafe(averagePosition - unitPosition);

                force += -toAveragePosition * needToLeave * 10;
            }

            /*if (cohesionWeight > 0 && additionalMoveData.lastVelocity != 0)
            {
                float3 averagePosition = positionCohesionSum / cohesionWeight;
                // float3 toAveragePosition = math.normalizesafe(averagePosition - unitPosition);
                //force += toAveragePosition; not yet implemented
            }*/

            force.y = 0;


            //TODO: assign new target at destination to array squad nicely
            GameHandler.SquadPath squadPath = squadPaths[unit.squad];

            if (squadPath.num != 0)
            {
                int pathNode = additionalMoveData.pathNode;
                if (pathNode != -1)
                {
                    float3 dist = additionalMoveData.target - unitPosition;
                    if (Vector3.Magnitude(dist) > 1)
                    {
                        force += math.normalizesafe(dist) * 10;
                    } else
                    {
                        pathNode++;
                        if (pathNode == squadPath.num)
                        {
                            pathNode = -1;
                            additionalMoveData.target = float3.zero;
                        }
                        else if (pathNode == 0)
                        {
                            additionalMoveData.target = squadPath.target1;
                        }
                        else if (pathNode == 1)
                        {
                            additionalMoveData.target = squadPath.target2;
                        }
                        else if (pathNode == 2)
                        {
                            additionalMoveData.target = squadPath.target3;
                        }
                        else if (pathNode == 3)
                        {
                            additionalMoveData.target = squadPath.target4;
                        }
                        else if (pathNode == 4)
                        {
                            additionalMoveData.target = squadPath.target5;
                        }

                        additionalMoveData.pathNode = pathNode;
                    }
                }
            }

            float3 velocity = localToWorld.Forward * (additionalMoveData.lastVelocity * 0.99f);

            velocity += force * deltaTime;
            float angle = Vector2.SignedAngle(new float2(velocity.x, velocity.z), new float2(localToWorld.Forward.x, localToWorld.Forward.z));
            if (Mathf.Abs(angle) > 10)
            {
                velocity = Quaternion.Euler(0, Mathf.Clamp(angle, -10, 10), 0) * localToWorld.Forward;
            }
            
            if (velocity.x != 0 && velocity.z != 0)
            {
                float unitSpeed = math.min(Vector3.Magnitude(velocity), unitMaxSpeed);
                velocity = math.normalize(velocity) * unitSpeed;
                
                additionalMoveData.lastVelocity = unitSpeed;

                if (unitSpeed < 0.1f)
                {
                    additionalMoveData.lastVelocity = 0;
                }

                localToWorld.Value = float4x4.TRS(unitPosition + velocity * deltaTime,
                    quaternion.LookRotationSafe(velocity, new float3(0, 1, 0)),
                    Vector3.one);
            }
            
            /*localToWorld.Value = float4x4.TRS(
                localToWorld.Position + velocity * deltaTime,
                quaternion.LookRotationSafe(velocity, localToWorld.Up),
                new float3(1f)
            );*/
        }
    }

    protected override void OnUpdate()
    {
        EntityQuery entityQuery = Entities.WithAll<Unit, LocalToWorld, AdditionalMoveData>().ToEntityQuery();
        NativeMultiHashMap<int, QuadrantData> quadrantDataHashMap = new NativeMultiHashMap<int, QuadrantData>(entityQuery.CalculateEntityCount(), Allocator.TempJob);
        NativeArray<float3> moveAvoidArray = new NativeArray<float3>(maxUnitNumber, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        var cellAvoidWeight = new NativeArray<int>(maxUnitNumber, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        NativeArray<int> unitKeyArray = new NativeArray<int>(maxUnitNumber, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        NativeArray<int> unitNearKeyArray = new NativeArray<int>(maxUnitNumber, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);



        // SUM CELLS BY SQUAD TO DETERMINE AVERAGE POSITION FOR GENERAl COHESION
        NativeArray<float3> moveCohesionArray = new NativeArray<float3>(2, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        var cellCohesionWeight = new NativeArray<int>(2, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

        /*SumSquadPositionAndNumberJob sumSquadPositionAndNumber = new SumSquadPositionAndNumberJob
        {
            squadPositionSum = moveCohesionArray,
            squadNumber = cellCohesionWeight
        };
        JobHandle sumSquadJobHandle = JobForEachExtensions.Schedule(sumSquadPositionAndNumber, entityQuery);*/

        //NativeHashMap<Entity, QuadrantData> entityTargetHashMap = new NativeHashMap<Entity, QuadrantData>(entityQuery.CalculateEntityCount(), Allocator.TempJob);

        SetQuadrantDataHashMapJob setQuadrantDataHashMapJob = new SetQuadrantDataHashMapJob
        {
            nativeMultiHashMap = quadrantDataHashMap.AsParallelWriter(),
            unitKeyArray = unitKeyArray,
            unitNearKeyArray = unitNearKeyArray
        };
        JobHandle setEntityHashMapJobHandle = JobForEachExtensions.Schedule(setQuadrantDataHashMapJob, entityQuery);
        
        Entities.WithAll<Unit, GridLocationSystem>().ForEach((ref LocalToWorld localToWorld) =>
        {
            //localToWorld. = 0.5f; //TODO make height equal to that on grid plus half scale
        });

        setEntityHashMapJobHandle.Complete();

        SetAvoidFromHashMapJob setAvoidFromHashMapJob = new SetAvoidFromHashMapJob
        {
            quadrantDataHashMap = quadrantDataHashMap,
            moveAvoidArray = moveAvoidArray,
            cellAvoidWeight = cellAvoidWeight,
            unitKeyArray = unitKeyArray,
            unitNearKeyArray = unitNearKeyArray
        };
        JobHandle avoidJobHandle = setAvoidFromHashMapJob.Schedule(entityQuery);

        var moveJob = new MoveUnits
        {
            deltaTime = Time.DeltaTime,
            unitMaxSpeed = 1f,

            positionAvoidSumOfCells = moveAvoidArray,
            positionCohesionSumOfCells = moveCohesionArray,
            cellAvoidWeight = cellAvoidWeight,
            cellCohesionWeight = cellCohesionWeight,
            squadPaths = GameHandler.GetInstance().squadPaths,
        };
        JobHandle moveJobHandle = moveJob.Schedule(entityQuery, avoidJobHandle);//, sumSquadJobHandle);
        moveJobHandle.Complete();

        quadrantDataHashMap.Dispose();
        unitKeyArray.Dispose();
        unitNearKeyArray.Dispose();
    }
}
