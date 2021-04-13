using System;
using System.Collections.Generic;
using UnityEngine;


public class HistorySystem : ISystem
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
        if (ECSManager.Instance.NetworkManager.isClient)
        {
            SaveClientHistory();
        }
    }

    public static void SaveClientHistory()
    {
        // save shapes
        ComponentsManager.Instance.ForEach<ShapeComponent>((entityID, shapeComponent) =>
        {
            if (!ComponentsManager.Instance.TryGetComponent<ClientHistory>(entityID, out var _))
            {
                ClientHistory newHistory = new ClientHistory()
                {
                    timeCreated = new List<int>(),
                    entityId = entityID.id,
                    shapeComponents = new List<ShapeComponent>()

                };
                ComponentsManager.Instance.SetComponent<ClientHistory>(entityID, newHistory);
            }
            ComponentsManager.Instance.GetComponent<ClientHistory>(entityID).timeCreated.Add(Utils.SystemTime);//in milliseconds
            ComponentsManager.Instance.GetComponent<ClientHistory>(entityID).shapeComponents.Add(shapeComponent);
        });

    }
}
