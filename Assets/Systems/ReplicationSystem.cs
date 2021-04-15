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

    public static int threshold = 1;

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


            ComponentsManager.Instance.SetComponent<ReplicationMessage>(entityID, msg);
        });
    }
    public static void UpdateSystemClient()
    {
        // apply state from server
        // can receive only one replication message per entity for simplicity

        //reshaping entities into players
        reshape();

        //updating the entity client histories to only contain states with time equal or larger than the timeCreated of the server replication message
        historyRemoveOld();


        //verifying if reconcialiation is needed for the NPC entities
        bool npcReconciliationBool = isNPCReconciliation(threshold);

        //applying simulations to NPC entities
        if (npcReconciliationBool)
        {
            //overwrite history at time t with the server state for NPC entities
            simulateNPCUpdates();
        }
        //applying the corrected/simulated state to the NPC entities
        updateNPCEntities(npcReconciliationBool);



        bool inputReconciliationBool = isInputReconciliation(threshold);
        if (inputReconciliationBool)
        {
          simulateInputUpdates();
        }
        updateClientEntity(inputReconciliationBool);

    }
    private static void reshape()
    {
        ShapeComponent component;
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
    }

    private static void historyRemoveOld()
    {
        ComponentsManager.Instance.ForEach<ReplicationMessage>((entityID, msgReplication) =>
        {
            //get history of current entity in loop
            ClientHistory entityClientHistory = ComponentsManager.Instance.GetComponent<ClientHistory>(msgReplication.entityId);

            //update entity history to only contain states with time equal or greater than timestamp in the server replication message
            while (msgReplication.timeCreated - entityClientHistory.timeCreated[0] > 2 * Time.deltaTime)
            {
                entityClientHistory.timeCreated.RemoveAt(0);
                entityClientHistory.shapeComponents.RemoveAt(0);

            }
        });
    }

    private static bool isNPCReconciliation(int threshold)
    {
        bool reconciliationBool = false;
        if (ECSManager.Instance.Config.enableDeadReckoning)
        {
            ComponentsManager.Instance.ForEach<ReplicationMessage>((entityID, msgReplication) =>
            {
                if ((uint)entityID != (uint)ECSManager.Instance.NetworkManager.LocalClientId)
                  {
                    //get history of current entity in loop
                    ClientHistory entityClientHistory = ComponentsManager.Instance.GetComponent<ClientHistory>(msgReplication.entityId);

                    //get entity client state at time t
                    ShapeComponent pastShapeComponent = entityClientHistory.shapeComponents[0];

                    if ((pastShapeComponent.pos - msgReplication.pos).sqrMagnitude > threshold)//if the position difference is larger than a threshold, apply reconciliation
                    {
                        reconciliationBool = true;
                    }
                  }
            });
        }
        return reconciliationBool;
    }

    private static void simulateNPCUpdates()
    {
        ShapeComponent component;
        ComponentsManager.Instance.ForEach<ReplicationMessage>((entityID, msgReplication) =>
        {
          if ((uint)entityID != (uint)ECSManager.Instance.NetworkManager.LocalClientId)
            {
            ClientHistory entityHistory = ComponentsManager.Instance.GetComponent<ClientHistory>(msgReplication.entityId);
            component = ComponentsManager.Instance.GetComponent<ShapeComponent>(msgReplication.entityId);
            component.pos = msgReplication.pos;
            component.speed = msgReplication.speed;
            component.size = msgReplication.size;
            entityHistory.shapeComponents[0] = component;
          }
        });


        ComponentsManager.Instance.ForEach<ClientHistory>((entityID, entityClientHistory) =>
        {
          if ((uint)entityID != (uint)ECSManager.Instance.NetworkManager.LocalClientId)
            {
                if (ComponentsManager.Instance.GetComponent<ClientHistory>((uint)entityID).shapeComponents.Count > 0)
                  {
                    for (int i = 1; i < ComponentsManager.Instance.GetComponent<ClientHistory>((uint)entityID).shapeComponents.Count; i++)
                      {

                        ShapeComponent prevShapeComponent;
                        ShapeComponent newShapeComponent;
                        ShapeComponent playerShapeComponent;

                        Dictionary<uint, bool> entityCollisionBool = new Dictionary<uint, bool>();
                        bool isCollision;

                        //simulate wall collisions
                        prevShapeComponent = entityClientHistory.shapeComponents[i - 1];
                        newShapeComponent = WallCollisionDetectionSystem.WallCollisionDetection(prevShapeComponent);
                        entityClientHistory.shapeComponents[i] = newShapeComponent;//this is an intermediate value that will be modified by the simulations below.


                        //simulate entity collisions
                        //init collision bool to false
                        entityCollisionBool.Add(entityID, false);

                        ComponentsManager.Instance.ForEach<ClientHistory, PlayerComponent>((playerEntityID, playerClientHistory, playerComponent) =>
                        {
                            prevShapeComponent = entityClientHistory.shapeComponents[i];//continuer la simulation de cet entité
                            playerShapeComponent = ComponentsManager.Instance.GetComponent<ClientHistory>(playerEntityID).shapeComponents[i];
                            isCollision = CircleCollisionDetectionSystem.CircleCollisionDetection(entityID, prevShapeComponent, playerEntityID, playerShapeComponent) | entityCollisionBool[entityID];
                            entityCollisionBool[entityID] = isCollision;
                        });


                        //simulate bounceback
                        if (entityCollisionBool[entityID])
                        {
                            prevShapeComponent = entityClientHistory.shapeComponents[i];//continuer la simulation de cet entité
                            newShapeComponent = BounceBackSystem.BounceBack(prevShapeComponent);
                            entityClientHistory.shapeComponents[i] = newShapeComponent;
                        }

                        //simulate positionUpdates and rewrite history
                        prevShapeComponent = entityClientHistory.shapeComponents[i];//continuer la simulation de cet entité
                        newShapeComponent = prevShapeComponent;
                        newShapeComponent.pos = PositionUpdateSystem.GetNewPosition(prevShapeComponent.pos, prevShapeComponent.speed);
                        entityClientHistory.shapeComponents[i] = newShapeComponent;

                      }
                  }
            }
        });
    }

    private static void updateNPCEntities(bool reconciliationBool)
    {
        ShapeComponent component;
        ComponentsManager.Instance.ForEach<ReplicationMessage>((entityID, msgReplication) =>
        {
            //if is client entity
            if ((uint)entityID != (uint)ECSManager.Instance.NetworkManager.LocalClientId)
            {

                if (ECSManager.Instance.Config.enableDeadReckoning)
                {
                    //Debug.Log("DeadReckoning enabled");
                    if (reconciliationBool)
                    {
                        int latestIndex = ComponentsManager.Instance.GetComponent<ClientHistory>(msgReplication.entityId).shapeComponents.Count - 1;
                        component = ComponentsManager.Instance.GetComponent<ClientHistory>(msgReplication.entityId).shapeComponents[latestIndex];
                        ComponentsManager.Instance.SetComponent<ShapeComponent>(msgReplication.entityId, component);
                    }
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


    private static bool isInputReconciliation(int threshold)
    {
        bool reconciliationBool = false;
        if (ECSManager.Instance.Config.enableInputPrediction)
        {
            ComponentsManager.Instance.ForEach<ReplicationMessage>((entityID, msgReplication) =>
            {
                if ((uint)entityID == (uint)ECSManager.Instance.NetworkManager.LocalClientId)
                  {
                    //get history of current entity in loop
                    ClientHistory entityClientHistory = ComponentsManager.Instance.GetComponent<ClientHistory>(msgReplication.entityId);

                    //get entity client state at time t
                    ShapeComponent pastShapeComponent = entityClientHistory.shapeComponents[0];

                    if ((pastShapeComponent.pos - msgReplication.pos).sqrMagnitude > threshold)//if the position difference is larger than a threshold, apply reconciliation
                    {
                        reconciliationBool = true;
                    }
                  }
            });
        }
        return reconciliationBool;
    }

    private static void simulateInputUpdates()
    {
      ShapeComponent component;
      ComponentsManager.Instance.ForEach<ReplicationMessage>((entityID, msgReplication) =>
      {
        if ((uint)entityID == (uint)ECSManager.Instance.NetworkManager.LocalClientId)
          {
          ClientHistory entityHistory = ComponentsManager.Instance.GetComponent<ClientHistory>(msgReplication.entityId);
          component = ComponentsManager.Instance.GetComponent<ShapeComponent>(msgReplication.entityId);
          component.pos = msgReplication.pos;
          component.speed = msgReplication.speed;
          component.size = msgReplication.size;
          entityHistory.shapeComponents[0] = component;
        }
      });


      ComponentsManager.Instance.ForEach<ClientHistory>((entityID, entityClientHistory) =>
      {
        if ((uint)entityID == (uint)ECSManager.Instance.NetworkManager.LocalClientId)
          {
              if (ComponentsManager.Instance.GetComponent<ClientHistory>((uint)entityID).shapeComponents.Count > 0)
                {
                  for (int i = 1; i < ComponentsManager.Instance.GetComponent<ClientHistory>((uint)entityID).shapeComponents.Count; i++)
                    {

                      ShapeComponent prevShapeComponent;
                      ShapeComponent newShapeComponent;
                      ShapeComponent playerShapeComponent;

                      Dictionary<uint, bool> entityCollisionBool = new Dictionary<uint, bool>();
                      bool isCollision;

                      //simulate wall collisions
                      prevShapeComponent = entityClientHistory.shapeComponents[i - 1];
                      newShapeComponent = WallCollisionDetectionSystem.WallCollisionDetection(prevShapeComponent);
                      entityClientHistory.shapeComponents[i] = newShapeComponent;//this is an intermediate value that will be modified by the simulations below.


                      //simulate entity collisions
                      //init collision bool to false
                      entityCollisionBool.Add(entityID, false);

                      ComponentsManager.Instance.ForEach<ClientHistory, PlayerComponent>((playerEntityID, playerClientHistory, playerComponent) =>
                      {
                          prevShapeComponent = entityClientHistory.shapeComponents[i];//continuer la simulation de cet entité
                          playerShapeComponent = ComponentsManager.Instance.GetComponent<ClientHistory>(playerEntityID).shapeComponents[i];
                          isCollision = CircleCollisionDetectionSystem.CircleCollisionDetection(entityID, prevShapeComponent, playerEntityID, playerShapeComponent) | entityCollisionBool[entityID];
                          entityCollisionBool[entityID] = isCollision;
                      });


                      //simulate bounceback
                      if (entityCollisionBool[entityID])
                      {
                          prevShapeComponent = entityClientHistory.shapeComponents[i];//continuer la simulation de cet entité
                          newShapeComponent = BounceBackSystem.BounceBack(prevShapeComponent);
                          entityClientHistory.shapeComponents[i] = newShapeComponent;
                      }

                      //simulate positionUpdates and rewrite history
                      prevShapeComponent = entityClientHistory.shapeComponents[i];//continuer la simulation de cet entité
                      newShapeComponent = prevShapeComponent;
                      newShapeComponent.pos = PositionUpdateSystem.GetNewPosition(prevShapeComponent.pos, prevShapeComponent.speed);
                      entityClientHistory.shapeComponents[i] = newShapeComponent;

                    }
                }
          }
      });
    }

    private static void updateClientEntity(bool reconciliationBool)
    {
      ShapeComponent component;
      ComponentsManager.Instance.ForEach<ReplicationMessage>((entityID, msgReplication) =>
      {
          //if is client entity
          if ((uint)entityID == (uint)ECSManager.Instance.NetworkManager.LocalClientId)
          {

              if (ECSManager.Instance.Config.enableInputPrediction)
              {
                  if (reconciliationBool)
                  {
                      int latestIndex = ComponentsManager.Instance.GetComponent<ClientHistory>(msgReplication.entityId).shapeComponents.Count - 1;
                      component = ComponentsManager.Instance.GetComponent<ClientHistory>(msgReplication.entityId).shapeComponents[latestIndex];
                      ComponentsManager.Instance.SetComponent<ShapeComponent>(msgReplication.entityId, component);
                  }
              }
              else
              {
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
