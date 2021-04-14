using System.Collections.Generic;
using UnityEngine;
using System;

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

                  idTimeStruct clientIdTimeCreated = new idTimeStruct(msgReplication.clientTimeCreated, msgReplication.entityId);


                  if (ClientTimeCreateComponent.timedClientComponent.ContainsKey(clientIdTimeCreated))
                  {
                      ShapeComponent clientPlayerComponentAfterInput = ClientTimeCreateComponent.timedClientComponent[clientIdTimeCreated];

                      ShapeComponent serverComponentAfterInput = new ShapeComponent(msgReplication.pos, msgReplication.size, msgReplication.speed, msgReplication.shape);


                      if (clientPlayerComponentAfterInput.isCloseEnough(serverComponentAfterInput)) {
                        Debug.Log("position ok");
                      }

                      else
                      {
                        Debug.Log("pas cool");
                        // Debug.Log("calcul client :"+msgReplication.clientTimeCreated);
                        // clientPlayerComponentAfterInput.LogInfo();
                        // Debug.Log("côté serv :" );
                        // serverComponentAfterInput.LogInfo();

                        ClientHistory playerHistory = ComponentsManager.Instance.GetComponent<ClientHistory>(msgReplication.entityId);
                        List<ClientHistory> playerHistories = new List<ClientHistory>();
                        ////get history of all players
                        ComponentsManager.Instance.ForEach<ClientHistory, PlayerComponent>((playerEntityID, playerClientHistory, playerComponent) =>
                        {
                            //get history of this player
                            playerHistories.Add(playerClientHistory);
                        });

                        Debug.Log("pas cool");
                        ShapeComponent currentComponent = SimulateInputUpdates(serverComponentAfterInput, msgReplication.entityId, playerHistory, msgReplication.clientTimeCreated, playerHistories);
                        ComponentsManager.Instance.SetComponent<ShapeComponent>(msgReplication.entityId, currentComponent);

                      }


                  }
               }
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


    private static ShapeComponent SimulateInputUpdates(ShapeComponent serverComponent, uint entityID, ClientHistory playerHistory, int originalTime, List<ClientHistory> playerHistories)
    {

        List<int> timeList = playerHistory.timeCreated;
        List<ShapeComponent> shapeList = playerHistory.shapeComponents;
        int originalTimeIndex = playerHistory.timeCreated.IndexOf(originalTime);

        if (originalTimeIndex < 0)
        {
          int i = 0;
          while (i < playerHistory.timeCreated.Count && Math.Abs(originalTime - playerHistory.timeCreated[i]) > Time.deltaTime)
          {
            i++;
          }
          if (i == playerHistory.timeCreated.Count)
          {
            originalTimeIndex = i-1;
          }
          else
          {
            originalTimeIndex = i;
          }
        }
        Debug.Log("original time " + originalTime);
        Debug.Log("index "+originalTimeIndex);
        Debug.Log("shapeList count "+shapeList.Count);
        // Debug.Log("shapelist size "+shapeList.Count);
        // Debug.Log("timelist size "+timeList.Count);

        shapeList[originalTimeIndex] = serverComponent;
        ShapeComponent currentComponent = serverComponent;
        ShapeComponent previousComponent;

        currentComponent = WallCollisionDetectionSystem.WallCollisionDetection(currentComponent);
        bool bounceBackBool = false;
        foreach (var playerClientHistory in playerHistories)//for each player
        {
            if (CircleCollisionDetectionSystem.CircleCollisionDetection(entityID, currentComponent, playerClientHistory.entityId, playerClientHistory.shapeComponents[originalTimeIndex]))
            {
                bounceBackBool = true;
            }
        }
        if (bounceBackBool)
        {
            currentComponent = BounceBackSystem.BounceBack(currentComponent);
        }

        shapeList[originalTimeIndex] = currentComponent;


        for (int i = originalTimeIndex + 1; i < shapeList.Count - 1; i++)
        {
          previousComponent = currentComponent;
          currentComponent = shapeList[i];

          currentComponent.pos = PositionUpdateSystem.GetNewPosition(previousComponent.pos, previousComponent.speed, Time.deltaTime);
          shapeList[i] = currentComponent;

        }

        playerHistory.shapeComponents = shapeList;

        // for (int j = 0; j < originalTimeIndex; j++)
        // {
        //   playerHistory.shapeComponents.RemoveAt(j); //test
        // }

        return currentComponent;

    }



    private static ShapeComponent SimulateUpdates(uint entityID, ShapeComponent shapeComponent, List<ClientHistory> playerHistories)
    {
        //calculate new values after one frame of updates
        shapeComponent = WallCollisionDetectionSystem.WallCollisionDetection(shapeComponent);
        bool bounceBackBool = false;
        foreach (var playerClientHistory in playerHistories)//for each player
        {
            if (CircleCollisionDetectionSystem.CircleCollisionDetection(entityID, shapeComponent, playerClientHistory.entityId, playerClientHistory.shapeComponents[0]))
            {
                bounceBackBool = true;
            }
        }
        if (bounceBackBool)
        {
            shapeComponent = BounceBackSystem.BounceBack(shapeComponent);
        }
        shapeComponent.pos = PositionUpdateSystem.GetNewPosition(shapeComponent.pos, shapeComponent.speed, Time.deltaTime);
        return shapeComponent;
    }




}
