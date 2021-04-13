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
            if (ECSManager.Instance.Config.enableInputPrediction)
            {
              uint id = entityID.id;
              int clientTimeCreated_ = 0;
              if (ClientTimeCreateComponent.idTime.ContainsKey(id))
              {
                clientTimeCreated_ = ClientTimeCreateComponent.idTime[id];
              }

              ReplicationMessage msg = new ReplicationMessage() {
                  clientTimeCreated = clientTimeCreated_,
                  messageID = 0,
                  timeCreated = Utils.SystemTime,
                  entityId = entityID.id,
                  shape = shapeComponent.shape,
                  pos = shapeComponent.pos,
                  speed = shapeComponent.speed,
                  size = shapeComponent.size

                };
                ComponentsManager.Instance.SetComponent<ReplicationMessage>(entityID, msg);

            }

            else
            {
            ReplicationMessage msg = new ReplicationMessage() {
                clientTimeCreated = 0,
                messageID = 0,
                timeCreated = Utils.SystemTime,
                entityId = entityID.id,
                shape = shapeComponent.shape,
                pos = shapeComponent.pos,
                speed = shapeComponent.speed,
                size = shapeComponent.size

                };
                ComponentsManager.Instance.SetComponent<ReplicationMessage>(entityID, msg);
              }
            // ComponentsManager.Instance.SetComponent<ReplicationMessage>(entityID, msg);
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
                  // int clientTimeCreated = ClientTimeCreateComponent.idTime[msgReplication.entityId];
                  // Debug.Log("heure client : " + clientTimeCreated);
                  // Debug.Log("heure serv : " + msgReplication.clientTimeCreated);

                  idTimeStruct clientIdTimeCreated = new idTimeStruct(msgReplication.clientTimeCreated, msgReplication.entityId);


                  if (ClientTimeCreateComponent.timedClientComponent.ContainsKey(clientIdTimeCreated))
                  {
                      ShapeComponent clientPlayerComponentAfterInput = ClientTimeCreateComponent.timedClientComponent[clientIdTimeCreated];

                      ShapeComponent serverComponentAfterInput = new ShapeComponent(msgReplication.pos, msgReplication.size, msgReplication.speed, msgReplication.shape);


                      if (clientPlayerComponentAfterInput == serverComponentAfterInput) {
                        // Debug.Log("position ok");
                      }

                      else
                      {
                        Debug.Log("calcul client :"+msgReplication.clientTimeCreated);
                        clientPlayerComponentAfterInput.LogInfo();
                        Debug.Log("côté serv :" );
                        serverComponentAfterInput.LogInfo();

                        //NOTE : les logs montrent que
                        //a un input/frame(?) d'avance par rapport au serv
                        //ex : le client est arrêté et a vitesse nulle, le serv
                        //a vitesse non nulle

                        //!!!! Meme si tu reçois une réponse
                        //ça veut pas dire que le serveur a process le nouveau état !!
                        //il faut faire en sorte de savoir s'il y a eu nouvel état ou pas

                        //AF : Faire une Queue; si on a un problème
                        //se mettre à la position envoyée par le serv, et
                        //se remettre aux actions sauvegardées de la Queue à partir du temps t
                        }
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
