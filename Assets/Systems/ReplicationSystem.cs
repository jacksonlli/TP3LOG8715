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

        //this loop is for reshaping entities into players
        ComponentsManager.Instance.ForEach<ReplicationMessage>((entityID, msgReplication) =>
        {
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
        });

        //this loop is for updating the entity client histories to only contain states with time equal or larger than the timeCreated of the server replication message
        ComponentsManager.Instance.ForEach<ReplicationMessage>((entityID, msgReplication) =>
        {
            //get history of current entity in loop
            ClientHistory entityClientHistory = ComponentsManager.Instance.GetComponent<ClientHistory>(msgReplication.entityId);

            //update entity history to only contain states with time equal or greater than timestamp in the server replication message
            while (msgReplication.timeCreated - entityClientHistory.timeCreated[0] > Time.deltaTime)
            {
                entityClientHistory.timeCreated.RemoveAt(0);
                entityClientHistory.shapeComponents.RemoveAt(0);

            }
        });

        //this loop is for verifying if reconcialiation is needed for the entities
        bool reconciliationBool = false;
        if (ECSManager.Instance.Config.enableDeadReckoning | ECSManager.Instance.Config.enableInputPrediction)
        {
            ComponentsManager.Instance.ForEach<ReplicationMessage>((entityID, msgReplication) =>
            {
                //get history of current entity in loop
                ClientHistory entityClientHistory = ComponentsManager.Instance.GetComponent<ClientHistory>(msgReplication.entityId);

                //get entity client state at time t
                ShapeComponent pastShapeComponent = entityClientHistory.shapeComponents[0];

                int threshold = 1;
                if ((pastShapeComponent.pos - msgReplication.pos).sqrMagnitude > threshold)//if the position difference is larger than a threshold, apply reconciliation
                {
                    reconciliationBool = true;
                }
            });
        }
        //the loops in this section are for applying simulations
        if (reconciliationBool)
        {
            ClientHistory entityClientHistory;
            //overwrite history at time t with the server state
            ComponentsManager.Instance.ForEach<ReplicationMessage>((entityID, msgReplication) =>
            {
                entityClientHistory = ComponentsManager.Instance.GetComponent<ClientHistory>(msgReplication.entityId);
                ShapeComponent component = ComponentsManager.Instance.GetComponent<ShapeComponent>(msgReplication.entityId);
                component.pos = msgReplication.pos;
                component.speed = msgReplication.speed;
                component.size = msgReplication.size;
                entityClientHistory.shapeComponents[0] = component;

            });

            //simulate and overwrite histories from time t to current time
            ComponentsManager.Instance.ForEach<ClientHistory>((entityID, entityClientHistory) =>
            {
                //simulate wall collisions
                //ComponentsManager.Instance.ForEach<ClientHistory>((entityID, history) =>
                //{
                //WallCollisionDetectionSystem.WallCollisionDetection();
                //});
                //    //simulate entity collisions
                //    ComponentsManager.Instance.ForEach<ClientHistory>((entityID, history) =>
                //    {

                //    });
                //    //simulate bounce
                //    ComponentsManager.Instance.ForEach<ClientHistory>((entityID, history) =>
                //    {

                //    });
                //    //simulate position updates and overwrite history
                //    ComponentsManager.Instance.ForEach<ClientHistory>((entityID, history) =>
                //    {

                //    });
            });
            
        }
        

        //this loop is for applying the corrected/simulated state to the entities
        ComponentsManager.Instance.ForEach<ReplicationMessage>((entityID, msgReplication) =>
        {
            //if is client entity
            if ((uint)entityID == (uint)ECSManager.Instance.NetworkManager.LocalClientId)
            {
                if (ECSManager.Instance.Config.enableInputPrediction)
                {
                    //Debug.Log("InputPred enabled");

                }
                else
                {
                    //Debug.Log("InputPred not enabled");
                    ShapeComponent component = ComponentsManager.Instance.GetComponent<ShapeComponent>(msgReplication.entityId);
                    component.pos = msgReplication.pos;
                    component.speed = msgReplication.speed;
                    component.size = msgReplication.size;
                    ComponentsManager.Instance.SetComponent<ShapeComponent>(msgReplication.entityId, component);
                }
            }
            else//if is not the client
            {
                if (ECSManager.Instance.Config.enableDeadReckoning)
                {
                    //Debug.Log("DeadReckoning enabled");

                    ShapeComponent component = ComponentsManager.Instance.GetComponent<ClientHistory>(msgReplication.entityId).shapeComponents[0];
                    ComponentsManager.Instance.SetComponent<ShapeComponent>(msgReplication.entityId, component);


                }
                else//without dead-reckoning activated, simply replicate non-player entities 
                {
                    //Debug.Log("DeadReackoning not enabled");
                    ShapeComponent component = ComponentsManager.Instance.GetComponent<ShapeComponent>(msgReplication.entityId);
                    component.pos = msgReplication.pos;
                    component.speed = msgReplication.speed;
                    component.size = msgReplication.size;
                    ComponentsManager.Instance.SetComponent<ShapeComponent>(msgReplication.entityId, component);
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
