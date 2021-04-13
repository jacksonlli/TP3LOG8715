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

            if (ECSManager.Instance.Config.enableInputPrediction)
            {
              //Debug.Log("test-1");
              uint clientId = (uint)ECSManager.Instance.NetworkManager.LocalClientId;
              //Debug.Log(clientId);
              if (entityID.id == clientId)
              {
                //Debug.Log("test0");
                int clientTimeCreated = ClientTimeCreateComponent.idTime[entityID.id];
                //Debug.Log("test1");
                idTimeStruct clientIdTimeCreated = new idTimeStruct(clientTimeCreated, entityID.id);
                ///Debug.Log("test2");

                ShapeComponent clientPlayerComponentAfterInput = ClientTimeCreateComponent.timedClientComponent[clientIdTimeCreated];
                //Debug.Log("test3");

                //comparaison, algo prediction/reconciliation
                if (clientPlayerComponentAfterInput == shapeComponent) {
                  //Debug.Log("position ok");
                }

                else {
                  //Debug.Log("pas cool");
                }
             }
                // clientTimeCreated = ComponentsManager.Instance.GetComponent<ClientTimeCreateComponent>(entityId).clientTimeCreated;
          }
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


                if ((uint)entityID == (uint)ECSManager.Instance.NetworkManager.LocalClientId)//if is client entity
                {
                    if (ECSManager.Instance.Config.enableInputPrediction)
                    {
                        //Debug.Log("InputPred enabled");
                    }
                    else
                    {
                        component.pos = msgReplication.pos;
                        component.speed = msgReplication.speed;
                        component.size = msgReplication.size;
                        ComponentsManager.Instance.SetComponent<ShapeComponent>(msgReplication.entityId, component);
                        //Debug.Log("InputPred not enabled");
                    }
                }
                else//if is any other entity than client
                {
                    if (ECSManager.Instance.Config.enableDeadReckoning)
                    {
                        //Debug.Log("DeadReckoning enabled");
                        int timeCreated;
                        ShapeComponent shapeComponent;
                        List<ClientHistory> playerHistories = new List<ClientHistory>();

                        ////get history of all players
                        ComponentsManager.Instance.ForEach<ClientHistory, PlayerComponent>((playerEntityID, playerClientHistory, playerComponent) =>
                        {
                            //update player history to only contain states with time equal or greater than timestamp in the server replication message
                            while(msgReplication.timeCreated - playerClientHistory.timeCreated[0] > Time.deltaTime)
                            {
                                playerClientHistory.timeCreated.RemoveAt(0);
                                playerClientHistory.shapeComponents.RemoveAt(0);

                            }
                            //get history of this player
                            playerHistories.Add(playerClientHistory);
                        });

                        //get history of current entity in loop
                        ClientHistory entityClientHistory = ComponentsManager.Instance.GetComponent<ClientHistory>(msgReplication.entityId);
                        //update entity history to only contain states with time equal or greater than timestamp in the server replication message
                        while (msgReplication.timeCreated - entityClientHistory.timeCreated[0] > Time.deltaTime)
                        {
                            entityClientHistory.timeCreated.RemoveAt(0);
                            entityClientHistory.shapeComponents.RemoveAt(0);

                        }


                        //get entity client state at time t
                        timeCreated = entityClientHistory.timeCreated[0];
                        shapeComponent = entityClientHistory.shapeComponents[0];

                        //    //get player client states at time t
                        //    List<ShapeComponent> playerCurrentShapeComponents = new List<ShapeComponent>();
                        //    List<uint> playerIDs = new List<uint>();
                        //    if (msgReplication.timeCreated - timeCreated < Time.deltaTime & msgReplication.timeCreated - timeCreated > 0)//found the corresponding frame
                        //    {
                        //        //compare entity position from client and server sides
                        //        if ((shapeComponent.pos - msgReplication.pos).sqrMagnitude > 0.01)//if the position difference is larger than a threshold, apply reconciliation
                        //        {
                        //            //apply server state from time t
                        //            shapeComponent = ComponentsManager.Instance.GetComponent<ShapeComponent>(msgReplication.entityId);
                        //            shapeComponent.pos = msgReplication.pos;
                        //            shapeComponent.speed = msgReplication.speed;
                        //            //simulate through history to where that position and speed would be now
                        //            for (int i = 0; i < history.timeCreated.Count; i++)
                        //            {
                        //                //entity values at time t+dt before reconcialiation
                        //                timeCreated = history.timeCreated.Dequeue();
                        //                history.shapeComponents.Dequeue();//throw away old history values
                        //                shapeComponent = SimulateUpdates(msgReplication.entityId, shapeComponent, playerHistories);
                        //                history.timeCreated.Enqueue(timeCreated);
                        //                history.shapeComponents.Enqueue(shapeComponent);
                        //            }
                        //            //set dead reckoning of final frame as current client state
                        //            ComponentsManager.Instance.SetComponent<ShapeComponent>(msgReplication.entityId, shapeComponent);
                        //        }
                        //        break;//exit while loop

                        //    }
                        //    else
                        //    {
                        //        foreach (var playerHistory in playerHistories)
                        //        {
                        //            playerHistory.shapeComponents.Dequeue();//to make sure the player state to be dequeued next corresponds to the next frame's
                        //        }
                        //    }
                        //}

                        foreach (var playerClientHistory in playerHistories)
                        {
                        component.pos = playerClientHistory.shapeComponents[0].pos;
                        component.speed = new Vector2(0,0);//entityClientHistory.shapeComponents[0].speed;
                        component.size = msgReplication.size;
                        }
                        
                        ComponentsManager.Instance.SetComponent<ShapeComponent>(msgReplication.entityId, component);
                    }
                    else//without dead-reckoning activated, simply replicate non-player entities 
                    {
                        component.pos = msgReplication.pos;
                        component.speed = msgReplication.speed;
                        component.size = msgReplication.size;
                        ComponentsManager.Instance.SetComponent<ShapeComponent>(msgReplication.entityId, component);
                        //Debug.Log("DeadReackoning not enabled");
                    }
                }
            }
        });
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
