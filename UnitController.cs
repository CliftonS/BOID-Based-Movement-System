using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;

public class UnitController : ComponentSystem
{
    private float3 startPosition;
    private Vector3 worldPosition;
    private Camera camera;
    private Plane plane = new Plane(Vector3.down, 0);

    protected override void OnCreate()
    {
        camera = Camera.main;
    }

    protected override void OnUpdate()
    {
        GameHandler gameHandler = GameHandler.GetInstance();
        if (Input.GetMouseButtonDown(0))
        {
            gameHandler.DeselectSquads();
            camera = Camera.main;
            startPosition = getWorldMousePosition();
            Debug.Log(Grid_System.GetInstance().IsWalkable((int) math.floor(startPosition.x), (int) math.floor(startPosition.z)));
        }

        if (Input.GetMouseButtonUp(0))
        {
            float3 endposition = getWorldMousePosition();

            float3 lowerLeftPosition = new float3(math.min(startPosition.x, endposition.x), 0, math.min(startPosition.z, endposition.z));
            float3 upperRightPosition = new float3(math.max(startPosition.x, endposition.x), 0, math.max(startPosition.z, endposition.z));

            Entities.ForEach((Entity entity, ref LocalToWorld localToWorld, ref Unit unit) =>
            {
                float3 entityPosition = localToWorld.Position;
                if (entityPosition.x >= lowerLeftPosition.x && 
                    entityPosition.z >= lowerLeftPosition.z && 
                    entityPosition.x <= upperRightPosition.x && 
                    entityPosition.z <= upperRightPosition.z)
                {
                    Debug.Log(entity);
                    if (unit.armyEnum == Unit.ArmyEnum.PlayerArmy)
                    {
                        gameHandler.SelectSquad(unit.squad);
                    }
                    
                }
            });
        }

        if (Input.GetMouseButtonDown(1))
        {
            float3 targetposition = getWorldMousePosition() + new Vector3(0, 0.5f, 0);

            foreach (int squad in gameHandler.GetSelectedSquads())
            {
                GameHandler.SquadPath squadPath = gameHandler.squadPaths[squad];
                squadPath.num = 1;
                squadPath.target1 = targetposition;
                gameHandler.squadPaths[squad] = squadPath;
            }

            Entities.ForEach((Entity entity, ref LocalToWorld localToWorld, ref Unit unit, ref AdditionalMoveData additionalMoveData) =>
            {
                if (gameHandler.IsSelected(unit.squad))
                {
                    additionalMoveData.pathNode = 0;
                    additionalMoveData.target = gameHandler.squadPaths[unit.squad].target1;
                }
            });
        }
    }

    private Vector3 getWorldMousePosition()
    {
        Vector3 screenPosition = Input.mousePosition;
        screenPosition.z = camera.nearClipPlane + 1;

        Ray ray = camera.ScreenPointToRay(screenPosition);
        if (plane.Raycast(ray, out float distance))
        {
            worldPosition = ray.GetPoint(distance);
            Debug.Log("Hit: " + distance);
        }

        return worldPosition;//camera.ScreenToWorldPoint(screenPosition);
    }

}
