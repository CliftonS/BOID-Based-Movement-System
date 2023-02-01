using Unity.Entities;

[GenerateAuthoringComponent]
public class PrefabEntityComponent : IComponentData
{
    public Entity prefabEntity;
}
