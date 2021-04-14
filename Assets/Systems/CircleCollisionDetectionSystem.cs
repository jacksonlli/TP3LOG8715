using UnityEngine;

public class CircleCollisionDetectionSystem : ISystem
{
    public string Name
    {
        get
        {
            return GetType().Name;
        }
    }

    public void UpdateSystem()
    {
        ComponentsManager.Instance.ForEach<ShapeComponent>((entity1, shapeComponent1) =>
        {
            ComponentsManager.Instance.ForEach<ShapeComponent, PlayerComponent>((entity2, shapeComponent2, playerComponent) =>
            {

                if (CircleCollisionDetection(entity1, shapeComponent1, entity2, shapeComponent2))
                {
                    ComponentsManager.Instance.SetComponent<CollisionEventComponent>(entity1, new CollisionEventComponent(entity1, entity2));
                    ComponentsManager.Instance.SetComponent<CollisionEventComponent>(entity2, new CollisionEventComponent(entity2, entity1));
                }
            });
        });
    }

    public static bool CircleCollisionDetection(uint entity1, ShapeComponent shapeComponent1, uint entity2, ShapeComponent shapeComponent2)
    {
        var pos1 = shapeComponent1.pos;
        var radius1 = shapeComponent1.size / 2;
        var pos2 = shapeComponent2.pos;
        var radius2 = shapeComponent2.size / 2;

        if (entity1 == entity2)
        {
            //early return, no need to check self
            return false; ;
        }
        if (Vector3.Distance(pos1, pos2) <= radius1 + radius2)
        {
            return true;
        }
        else
        {
            return false;
        }
    }
}
