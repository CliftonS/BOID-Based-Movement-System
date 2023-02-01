using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Rendering;
using Unity.Transforms;
using Unity.Collections;
using Unity.Mathematics;

public class Testing : MonoBehaviour
{

    [SerializeField] private Mesh mesh;
    [SerializeField] private Material material;
    private EntityManager entityManager;

    // Start is called before the first frame update
    void Start()
    {
        entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        NativeArray<Entity> entityArray = new NativeArray<Entity>(0, Allocator.Temp);
        EntityArchetype entityArchetype = entityManager.CreateArchetype(typeof(RenderMesh),
            typeof(LocalToWorld),
            typeof(Translation),
            typeof(RenderBounds),
            typeof(Rotation),
            typeof(Scale));
        entityManager.CreateEntity(entityArchetype, entityArray);

        foreach (Entity entity in entityArray)
        {
            entityManager.SetSharedComponentData(entity, new RenderMesh
            {
                mesh = mesh,
                material = material,
            });

            entityManager.SetComponentData(entity, new Translation
            {
                Value = new float3(UnityEngine.Random.Range(-8, 8f), 1f, UnityEngine.Random.Range(-3, 3f))
            });

            entityManager.SetComponentData(entity, new Scale
            {
                Value = 2f
            });
        }

        entityArray.Dispose();
    }

    private Mesh CreateMesh(float width, float height)
    {
        Vector3[] vertices = new Vector3[4];
        Vector2[] uv = new Vector2[4];
        int[] triangles = new int[6];


        float halfWidth = width / 2f;
        float halfHeight = height / 2f;
        vertices[0] = new Vector3(-halfWidth, -halfHeight);
        vertices[1] = new Vector3(-halfWidth, +halfHeight);
        vertices[2] = new Vector3(+halfWidth, +halfHeight);
        vertices[3] = new Vector3(+halfWidth, -halfHeight);

        uv[0] = new Vector2(0, 0);
        uv[1] = new Vector2(0, 1);
        uv[2] = new Vector2(1, 1);
        uv[3] = new Vector2(1, 0);

        triangles[0] = 0;
        triangles[1] = 1;
        triangles[2] = 3;

        triangles[3] = 1;
        triangles[4] = 2;
        triangles[5] = 3;

        Mesh mesh = new Mesh();
        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.triangles = triangles;

        return mesh;
    }

}

public class MoveSystem : ComponentSystem
{
    public GameObject cameraObject;
    private Camera camera;

    protected override void OnStartRunning()
    {
        base.OnStartRunning();
        cameraObject = GameObject.Find("CameraParent");
        camera = Camera.main;
    }
    protected override void OnUpdate()
    {
        //Entities.ForEach((ref Translation translation, ref RenderMesh renderMesh) => {
        //    Graphics.DrawMesh(renderMesh.mesh, translation.Value, Quaternion.identity, renderMesh.material, 0, camera, 0, new MaterialPropertyBlock());

            /*Vector3 vec = new Vector3(translation.Value.x, translation.Value.y, translation.Value.z);
            Vector2 adj = camera.WorldToScreenPoint(vec);

            float distance = Vector3.Distance(Vector3.Project(vec, cameraObject.transform.forward), cameraObject.transform.position);
            float scales = 10.0f / distance;
            rotation.Value = cameraObject.transform.rotation;
            scale.Value = scales;*/
            //translation.Value.z += 1f * Time.DeltaTime;
        //});
    }
}