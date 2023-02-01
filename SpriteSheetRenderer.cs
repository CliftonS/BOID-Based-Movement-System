using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Rendering;
using Unity.Transforms;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Jobs;
using Unity.Burst;

[UpdateAfter(typeof(SpriteSheetAnimation_System))]
public class SpriteSheetRenderer : ComponentSystem
{

    private struct RenderData
    {
        public Entity entity;
        public float3 position;
        public Matrix4x4 matrix;
        public Vector4 uv;
    }

    [BurstCompile]
    private struct CullAndSortJob: IJob
    {

        public NativeArray<SpriteSheetAnimation_Data> animationDataArray;
        public NativeArray<LocalToWorld> localToWorldArray;
        public NativeArray<Unit> unitDataArray;
        public NativeArray<float> distances;

        private void swap(int i, int j)
        {
            LocalToWorld tmp = localToWorldArray[i];
            localToWorldArray[i] = localToWorldArray[j];
            localToWorldArray[j] = tmp;

            SpriteSheetAnimation_Data tmpAnim = animationDataArray[i];
            animationDataArray[i] = animationDataArray[j];
            animationDataArray[j] = tmpAnim;

            Unit tmpUnit = unitDataArray[i];
            unitDataArray[i] = unitDataArray[j];
            unitDataArray[j] = tmpUnit;
            
            float distanceToIndexTmp = distances[i];
            distances[i] = distances[j];
            distances[j] = distanceToIndexTmp;
        }

        private int partition(int low, int high)
        {

            // pivot
            float pivot = distances[high];

            // Index of smaller element and
            // indicates the right position
            // of pivot found so far
            int i = (low - 1);

            for (int j = low; j <= high - 1; j++)
            {

                // If current element is smaller
                // than the pivot
                if (distances[j] < pivot)
                {

                    // Increment index of
                    // smaller element
                    i++;
                    swap(i, j);
                }
            }
            swap(i + 1, high);
            return (i + 1);
        }

        private void quicksort(int lowIndex, int highIndex)
        {
            if (lowIndex < highIndex)
            {
                int pi = partition(lowIndex, highIndex);
                quicksort(lowIndex, pi - 1);
                quicksort(pi + 1, highIndex);
            }
        }

        public void Execute()
        {
            quicksort(0, distances.Length - 1);
        }
    }

    [BurstCompile]
    private struct CalculateProjectedDistance : IJobParallelFor
    {

        public NativeArray<LocalToWorld> localToWorldArray;
        public NativeArray<float> distances;
        public float3 cameraPosition;
        public float3 cameraForward;

        private float3 ProjectOntoForward(float3 adjustedPosition)
        {
            return Vector3.Dot(adjustedPosition, cameraForward) * cameraForward;
        }

        public void Execute(int i)
        {
            distances[i] = -Vector3.Distance(ProjectOntoForward(localToWorldArray[i].Position - cameraPosition), cameraPosition);
        }
    }

    protected override void OnUpdate()
    {
        MaterialPropertyBlock materialPropertyBlock = new MaterialPropertyBlock();
        Camera camera = Camera.main;
        Quaternion rotation = camera.transform.parent.rotation;
        Vector4[] uv = new Vector4[1];
        Mesh quadMesh = GameHandler.GetInstance().quadMesh;
        List<Material> materials = new List<Material>();
        materials.Add(GameHandler.GetInstance().spriteSheetMaterial);
        materials.Add(GameHandler.GetInstance().mechaKnightMaterial);
        //Material material = GameHandler.GetInstance().spriteSheetMaterial;
        int shaderPropertyId = Shader.PropertyToID("_MainTex_UV");
        int colorPropertyId = Shader.PropertyToID("_Color_Tint");

        EntityQuery entityQuery = GetEntityQuery(typeof(LocalToWorld), typeof(SpriteSheetAnimation_Data), typeof(Unit), typeof(AdditionalMoveData));

        NativeArray<SpriteSheetAnimation_Data> animationDataArray = entityQuery.ToComponentDataArray<SpriteSheetAnimation_Data>(Allocator.TempJob);
        NativeArray<Unit> unitDataArray = entityQuery.ToComponentDataArray<Unit>(Allocator.TempJob);
        NativeArray<LocalToWorld> localToWorldArray = entityQuery.ToComponentDataArray<LocalToWorld>(Allocator.TempJob);
        NativeArray<AdditionalMoveData> moveDataArray = entityQuery.ToComponentDataArray<AdditionalMoveData>(Allocator.TempJob);
        NativeArray<float> distances = new NativeArray<float>(localToWorldArray.Length, Allocator.TempJob);

        CalculateProjectedDistance calculateProjectedDistanceJob = new CalculateProjectedDistance
        {
            localToWorldArray = localToWorldArray,
            distances = distances,
            cameraForward = camera.transform.forward,
            cameraPosition = camera.transform.position,
        };

        JobHandle jobHandle = calculateProjectedDistanceJob.Schedule(distances.Length, 32);
        jobHandle.Complete();

        CullAndSortJob cullAndSortJob = new CullAndSortJob
        {
            animationDataArray = animationDataArray,
            localToWorldArray = localToWorldArray,
            unitDataArray = unitDataArray,
            distances = distances,
        };

        jobHandle = cullAndSortJob.Schedule();
        jobHandle.Complete();

        List<List<Matrix4x4>> majorMatrixList = new List<List<Matrix4x4>>();
        List<List<Vector4>> majorUvList = new List<List<Vector4>>();
        List<List<int>> majorSelectedList = new List<List<int>>();

        for (int i = 0; i < materials.Count; i++)
        {
            majorMatrixList.Add(new List<Matrix4x4>());
            majorUvList.Add(new List<Vector4>());
            majorSelectedList.Add(new List<int>());
        }

        for (int i = 0; i < animationDataArray.Length; i++)
        {
            SpriteSheetAnimation_Data spriteSheetAnimation_Data = animationDataArray[i];
            majorMatrixList[spriteSheetAnimation_Data.sprite].Add(spriteSheetAnimation_Data.matrix);
            majorUvList[spriteSheetAnimation_Data.sprite].Add(spriteSheetAnimation_Data.uv);
            majorSelectedList[spriteSheetAnimation_Data.sprite].Add(unitDataArray[i].squad);
        }

        int sliceCount = 1023;
        for (int j = 0; j < materials.Count; j++)
        {
            for (int i = 0; i < majorMatrixList[j].Count; i += sliceCount)
            {
                int sliceSize = math.min(majorMatrixList[j].Count - 1 - i, sliceCount);

                List<Matrix4x4> matrixList = new List<Matrix4x4>();
                List<Vector4> uvList = new List<Vector4>();
                List<Vector4> colorList = new List<Vector4>();

                for (int t = 0; t < sliceSize; t++)
                {
                    matrixList.Add(majorMatrixList[j][i + t]);
                    uvList.Add(majorUvList[j][i + t]);
                    if (GameHandler.GetInstance().IsSelected(majorSelectedList[j][i + t]))
                    {
                        colorList.Add(Color.green);
                    } 
                    else
                    {
                        colorList.Add(Color.white);
                    }
                }

                materialPropertyBlock.SetVectorArray(shaderPropertyId, uvList);
                materialPropertyBlock.SetVectorArray(colorPropertyId, colorList);

                Graphics.DrawMeshInstanced(
                    quadMesh,
                    0,
                    materials[j],
                    matrixList,
                    materialPropertyBlock);
            }
        }

        animationDataArray.Dispose();
        localToWorldArray.Dispose();
        unitDataArray.Dispose();
        moveDataArray.Dispose();
        distances.Dispose();
    }
}
