using System.Collections.Generic;
using UnityEngine;


public class ReplicationSystem : ISystem
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
        if (ECSManager.Instance.NetworkManager.isServer)
        {
            UpdateSystemServer();
        }
        else if (ECSManager.Instance.NetworkManager.isClient)
        {
            UpdateSystemClient();
        }
    }

    public static void UpdateSystemServer()
    {
        // creates messages from current state
        ComponentsManager.Instance.ForEach<ShapeComponent>((entityID, shapeComponent) => {
            ReplicationMessage msg = new ReplicationMessage() {
                messageID = 0,
                timeCreated = Utils.SystemTime,
                entityId = entityID.id,
                shape = shapeComponent.shape,
                pos = shapeComponent.pos,
                speed = shapeComponent.speed,
                size = shapeComponent.size

            };

          //   if (ECSManager.Instance.Config.enableInputPrediction)
          //   {
          //
          //     if (ClientTimeCreateComponent.idTime.ContainsKey(entityID.id))
          //     {
          //       int clientTimeCreated = ClientTimeCreateComponent.idTime[entityID.id];
          //       idTimeStruct clientIdTimeCreated = new idTimeStruct(clientTimeCreated, entityID.id);
          //       ShapeComponent clientPlayerComponentAfterInput = ClientTimeCreateComponent.timedClientComponent[clientIdTimeCreated];
          //
          //       //comparaison, algo prediction/reconciliation
          //       if (clientPlayerComponentAfterInput == shapeComponent) {
          //         // Debug.Log("position ok");
          //       }
          //
          //       else {
          //         Debug.Log("pas cool");
          //         Debug.Log("calcul client :");
          //         clientPlayerComponentAfterInput.LogInfo();
          //         Debug.Log("côté serv :" );
          //         shapeComponent.LogInfo();
          //       }
          //    }
          //       // clientTimeCreated = ComponentsManager.Instance.GetComponent<ClientTimeCreateComponent>(entityId).clientTimeCreated;
          // }
            ComponentsManager.Instance.SetComponent<ReplicationMessage>(entityID, msg);
        });
    }
    public static void UpdateSystemClient()
    {
        // apply state from server
        // can receive only one replication message per entity for simplicity
        ComponentsManager.Instance.ForEach<ReplicationMessage>((entityID, msgReplication) => {
            // Updating entity info from message's state
            var component = ComponentsManager.Instance.GetComponent<ShapeComponent>(msgReplication.entityId);

            if (component.shape != msgReplication.shape)
            {
                // needs to respawn entity to change its shape
                bool spawnFound = ComponentsManager.Instance.TryGetComponent(new EntityComponent(0), out SpawnInfo spawnInfo);

                if (!spawnFound)
                {
                    spawnInfo = new SpawnInfo(false);
                }
                spawnInfo.replicatedEntitiesToSpawn.Add(msgReplication);
                ComponentsManager.Instance.SetComponent<SpawnInfo>(new EntityComponent(0), spawnInfo);
            }
            else
            {
              if (ECSManager.Instance.Config.enableInputPrediction)
              {

                if (ClientTimeCreateComponent.idTime.ContainsKey(msgReplication.entityId))
                {
                  int clientTimeCreated = ClientTimeCreateComponent.idTime[msgReplication.entityId];
                  idTimeStruct clientIdTimeCreated = new idTimeStruct(clientTimeCreated, msgReplication.entityId);
                  ShapeComponent clientPlayerComponentAfterInput = ClientTimeCreateComponent.timedClientComponent[clientIdTimeCreated];

                  ShapeComponent serverComponentAfterInput = new ShapeComponent(msgReplication.pos, msgReplication.size, msgReplication.speed, msgReplication.shape);



                  if (clientPlayerComponentAfterInput == serverComponentAfterInput) {
                    Debug.Log("position ok");
                  }

                  else {
                    Debug.Log("pas cool");
                    Debug.Log("created : " + clientTimeCreated);
                    Debug.Log("calcul client :");
                    clientPlayerComponentAfterInput.LogInfo();
                    Debug.Log("côté serv :" );
                    serverComponentAfterInput.LogInfo();
                  }
               }
                  // clientTimeCreated = ComponentsManager.Instance.GetComponent<ClientTimeCreateComponent>(entityId).clientTimeCreated;
              }


              else
              {
                component.pos = msgReplication.pos;
                component.speed = msgReplication.speed;
                component.size = msgReplication.size;
                ComponentsManager.Instance.SetComponent<ShapeComponent>(msgReplication.entityId, component);
              }
            }
        });
    }
}
