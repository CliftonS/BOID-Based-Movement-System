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

public struct SpriteSheetAnimation_Data : IComponentData
{
    public int currentFrame;
    public int frameCount;
    public float frameTimer;
    public float frameTimerMax;

    public Vector4 uv;
    public Matrix4x4 matrix;

    public int sprite;
}

public class SpriteSheetAnimation_System : JobComponentSystem
{
    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        AnimateJob job = new AnimateJob
        {
            deltaTime = Time.DeltaTime,
            rotation = Camera.main.transform.parent.rotation,
            rotationAdjusted = Camera.main.transform.parent.rotation * Quaternion.Euler(0, -90, 0)
        };
        return job.Schedule(this, inputDeps);
    }

    /*public struct CameraAngleDisplayJob : IJobForEach<AdditionalMoveData>
    {
        public Quaternion rotation;

        public void Execute([ReadOnly] ref AdditionalMoveData additionalMoveData)
        {
            
        }
    }*/

    [BurstCompile]
    public struct AnimateJob : IJobForEach<SpriteSheetAnimation_Data, LocalToWorld>
    {
        public float deltaTime;
        public Quaternion rotation;
        public Quaternion rotationAdjusted;

        public void Execute(ref SpriteSheetAnimation_Data spriteSheetAnimation_Data, [ReadOnly] ref LocalToWorld localToWorld)
        {
            spriteSheetAnimation_Data.frameTimer += deltaTime;

            float angle = Quaternion.Angle(rotationAdjusted, localToWorld.Rotation);
            float num = 1f;
            if (angle < 90)
            {
                num = -1f;
            }
            spriteSheetAnimation_Data.matrix = Matrix4x4.TRS(localToWorld.Position, rotation, new Vector3(num, 1, 1));
            while (spriteSheetAnimation_Data.frameTimer >= spriteSheetAnimation_Data.frameTimerMax)
            {
                spriteSheetAnimation_Data.frameTimer -= spriteSheetAnimation_Data.frameTimerMax;
                spriteSheetAnimation_Data.currentFrame = (spriteSheetAnimation_Data.currentFrame + 1) % spriteSheetAnimation_Data.frameCount;

                float uvWidth = 1f / spriteSheetAnimation_Data.frameCount;
                float uvHeight = 1f;
                float uvOffsetX = uvWidth * spriteSheetAnimation_Data.currentFrame;
                float uvOffsetY = 0f;
                spriteSheetAnimation_Data.uv = new Vector4(uvWidth, uvHeight, uvOffsetX, uvOffsetY);
            }
        }
    }
}
