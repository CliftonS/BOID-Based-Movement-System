using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Transforms;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Random = UnityEngine.Random;

public class GameHandler : MonoBehaviour
{
    private static GameHandler instance;

    public static GameHandler GetInstance()
    {
        return instance;
    }

    // -1 no target, 0 at final target (still or in combat), 1+ still need to reach final target, moving
    //public Dictionary<int, Dictionary<int, int>> unitDictionary = new Dictionary<int, Dictionary<int, int>>();
    //public NativeArray<NativeHashMap<int, int>> unitDictionaries = new NativeArray<NativeHashMap<int, int>>(100, Allocator.Persistent);
    //public NativeHashMap<int, int> unitHashMap;
    //public Dictionary<int, SquadPath> squadPathDictionary = new Dictionary<int, SquadPath>();
    public NativeArray<SquadPath> squadPaths;
    public NativeArray<Squad> squads;

    /*public class SquadPath
    {
        List<float3> targets = new List<float3>();

        bool chargeUnit = false;

        public List<float3> GetPath()
        {
            return targets;
        }

        public void AddTarget(float3 target)
        {
            targets.Add(target);
        }

        public void ResetPath()
        {
            targets.Clear();
        }
    }*/

    public struct SquadPath
    {
        public float3 target1;
        public float3 target2;
        public float3 target3;
        public float3 target4;
        public float3 target5;
        public int num;
        public bool targetUnit;
    }

    public struct Squad
    {
        int squadNum;

        int startingUnits;
        int currentUnits;

        int kills;

        Stats stats;
    }

    public struct Stats
    {
        int maxHP;
        int attackSkill;
        int defenceSkill;
        bool shield;
        int damage;
    }

    public Mesh quadMesh;
    public Material spriteSheetMaterial;
    public Material mechaKnightMaterial;

    public HashSet<int> selectedSquads = new HashSet<int>();

    public void DeselectSquads()
    {
        selectedSquads.Clear();
    }

    public void SelectSquad(int squadNum)
    {
        selectedSquads.Add(squadNum);
    }

    public bool IsSelected(int squadNum)
    {
        return selectedSquads.Contains(squadNum);
    }

    public HashSet<int> GetSelectedSquads()
    {
        return selectedSquads;
    }

    int i = 0;
    int squad = 0;

    public void InstantiateSquad(EntityManager entityManager, EntityArchetype entityArchetype, int numUnits, 
        float3 position, Unit.ArmyEnum army, Unit.SizeEnum size,SpriteSheetAnimation_Data spriteSheetAnimation_Data)
    {

        NativeArray<Entity> entityArray = new NativeArray<Entity>(numUnits, Allocator.Temp);

        entityManager.CreateEntity(entityArchetype, entityArray);
        
        foreach (Entity entity in entityArray)
        {
            /*entityManager.SetComponentData(entity,
            new Translation
            {
                Value = position + new float3(Random.Range(-20f, 20f), 0.5f, Random.Range(-20f, 20f))
            });*/
            entityManager.SetComponentData(entity,
            new LocalToWorld
            {
                Value = float4x4.TRS(
                    position + new float3(Random.Range(-20f, 20f), 0.5f, Random.Range(-20f, 20f)),
                    quaternion.LookRotationSafe(new float3(1, 0, 0), new float3(0, 1, 0)),
                    Vector3.one)
            });
            entityManager.SetComponentData(entity,
            new Unit
            {
                armyEnum = army,
                sizeEnum = size,
                squad = squad,
                id = i
            });
            entityManager.SetComponentData(entity,
            spriteSheetAnimation_Data);
            entityManager.AddComponentData(entity,
            new AdditionalMoveData
            {
                //rotation = quaternion.LookRotationSafe(new float3(-1, 0, 0), new float3(0, 1, 0)),
                lastVelocity = 0,
                target = float3.zero,
                pathNode = -1
            });
            i++;
        }

        squad++;
        entityArray.Dispose();
    }

    private void Awake()
    {
        instance = this;

        squadPaths = new NativeArray<SquadPath>(100, Allocator.Persistent);
        squads = new NativeArray<Squad>(100, Allocator.Persistent);

        EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        EntityArchetype entityArchetype = entityManager.CreateArchetype(
            typeof(LocalToWorld),
            typeof(Unit),
            typeof(SpriteSheetAnimation_Data)
        );

        InstantiateSquad(entityManager, entityArchetype, 2000, new float3(-20, 0, 20), Unit.ArmyEnum.PlayerArmy, Unit.SizeEnum.Medium, 
            new SpriteSheetAnimation_Data
        {
            currentFrame = 0,
            frameCount = 5,
            frameTimer = 0f,
            frameTimerMax = 1f,
            uv = new Vector4(1, 5, 0, 0),
            sprite = 0
        });

        InstantiateSquad(entityManager, entityArchetype, 1000, new float3(20, 0, -20), Unit.ArmyEnum.AIArmy, Unit.SizeEnum.Large,
            new SpriteSheetAnimation_Data
            {
                currentFrame = 0,
                frameCount = 1,
                frameTimer = 0f,
                frameTimerMax = 100000f,
                uv = new Vector4(1, 1, 0, 0),
                sprite = 1
            });
    }

    private void OnDestroy()
    {
        squadPaths.Dispose();
        squads.Dispose();
    }
}
