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

        ShapeComponent component;

        //this loop is for reshaping entities into players
        ComponentsManager.Instance.ForEach<ReplicationMessage>((entityID, msgReplication) =>
        {
            component = ComponentsManager.Instance.GetComponent<ShapeComponent>(msgReplication.entityId);
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
            //overwrite history at time t with the server state
            ComponentsManager.Instance.ForEach<ReplicationMessage>((entityID, msgReplication) =>
            {
                ClientHistory entityHistory = ComponentsManager.Instance.GetComponent<ClientHistory>(msgReplication.entityId);
                component = ComponentsManager.Instance.GetComponent<ShapeComponent>(msgReplication.entityId);
                component.pos = msgReplication.pos;
                component.speed = msgReplication.speed;
                component.size = msgReplication.size;
                entityHistory.shapeComponents[0] = component;

            });

            //simulate and overwrite histories from time t+1 to current time
            for (int i = 1; i < ComponentsManager.Instance.GetComponent<ClientHistory>(1).shapeComponents.Count; i++)
            {
                ShapeComponent prevShapeComponent;
                ShapeComponent newShapeComponent;
                //simulate wall collisions
                ComponentsManager.Instance.ForEach<ClientHistory>((entityID, entityClientHistory) =>
                {
                    prevShapeComponent = entityClientHistory.shapeComponents[i-1];
                    newShapeComponent = WallCollisionDetectionSystem.WallCollisionDetection(prevShapeComponent);
                    entityClientHistory.shapeComponents[i] = newShapeComponent;//this is an intermediate value that will be modified by the simulations below.
                });

                ShapeComponent playerShapeComponent;
                Dictionary<uint, bool> entityCollisionBool = new Dictionary<uint, bool>();
                bool isCollision;

                //simulate entity collisions
                ComponentsManager.Instance.ForEach<ClientHistory>((entityID, entityClientHistory) =>
                {
                    //init collision bool to false
                    entityCollisionBool.Add(entityID, false);

                    ComponentsManager.Instance.ForEach<ClientHistory, PlayerComponent>((playerEntityID, playerClientHistory, playerComponent) =>
                    {
                        prevShapeComponent = entityClientHistory.shapeComponents[i];//continuer la simulation de cet entité
                        playerShapeComponent = ComponentsManager.Instance.GetComponent<ClientHistory>(playerEntityID).shapeComponents[i];
                        isCollision = CircleCollisionDetectionSystem.CircleCollisionDetection(entityID, prevShapeComponent, playerEntityID, playerShapeComponent) | entityCollisionBool[entityID];
                        entityCollisionBool[entityID] = isCollision;
                    });
                });
                //simulate bounce back
                ComponentsManager.Instance.ForEach<ClientHistory>((entityID, entityClientHistory) =>
                {
                    if (entityCollisionBool[entityID])
                    {
                        prevShapeComponent = entityClientHistory.shapeComponents[i];//continuer la simulation de cet entité
                        newShapeComponent = BounceBackSystem.BounceBack(prevShapeComponent);
                        entityClientHistory.shapeComponents[i] = newShapeComponent;
                    }
                });
                //simulate position updates and overwrite history
                ComponentsManager.Instance.ForEach<ClientHistory>((entityID, entityClientHistory) =>
                {
                    prevShapeComponent = entityClientHistory.shapeComponents[i];//continuer la simulation de cet entité
                    newShapeComponent = prevShapeComponent;
                    newShapeComponent.pos = PositionUpdateSystem.GetNewPosition(prevShapeComponent.pos, prevShapeComponent.speed);
                    entityClientHistory.shapeComponents[i] = newShapeComponent;
                });
            }

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

                    int latestIndex = ComponentsManager.Instance.GetComponent<ClientHistory>(msgReplication.entityId).shapeComponents.Count - 1;
                    component = ComponentsManager.Instance.GetComponent<ClientHistory>(msgReplication.entityId).shapeComponents[latestIndex];
                    ComponentsManager.Instance.SetComponent<ShapeComponent>(msgReplication.entityId, component);
                }
                else
                {
                    //Debug.Log("InputPred not enabled");
                    component = ComponentsManager.Instance.GetComponent<ShapeComponent>(msgReplication.entityId);
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

                    int latestIndex = ComponentsManager.Instance.GetComponent<ClientHistory>(msgReplication.entityId).shapeComponents.Count - 1;
                    component = ComponentsManager.Instance.GetComponent<ClientHistory>(msgReplication.entityId).shapeComponents[latestIndex];
                    ComponentsManager.Instance.SetComponent<ShapeComponent>(msgReplication.entityId, component);

                }
                else//without dead-reckoning activated, simply replicate non-player entities 
                {
                    //Debug.Log("DeadReackoning not enabled");
                    component = ComponentsManager.Instance.GetComponent<ShapeComponent>(msgReplication.entityId);
                    component.pos = msgReplication.pos;
                    component.speed = msgReplication.speed;
                    component.size = msgReplication.size;
                    ComponentsManager.Instance.SetComponent<ShapeComponent>(msgReplication.entityId, component);
                }
            }
        });

    }
}
