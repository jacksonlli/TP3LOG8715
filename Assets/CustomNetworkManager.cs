using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MLAPI;
using MLAPI.Messaging;
using MLAPI.Serialization;
using MLAPI.Serialization.Pooled;

public class CustomNetworkManager : NetworkingManager
{
    public void Awake()
    {
        OnClientConnectedCallback += OnClientConnected;
        OnServerStarted += OnStartServer;
    }

    public void OnClientConnected(ulong clientId)
    {
        if (isServer)
        {
            bool spawnFound = ComponentsManager.Instance.TryGetComponent(new EntityComponent(0), out SpawnInfo spawnInfo);

            if (!spawnFound)
            {
                spawnInfo = new SpawnInfo(false);
            }
            spawnInfo.playersToSpawn.Add((uint)clientId);
            ComponentsManager.Instance.SetComponent<SpawnInfo>(new EntityComponent(0), spawnInfo);
        }
        else
        {
            RegisterClientNetworkHandlers();
        }
    }

    public void OnStartServer()
    {
        RegisterServerNetworkHandlers();
    }

    public void SendReplicationMessage(ReplicationMessage msg)
    {
        using (PooledBitStream stream = PooledBitStream.Get())
        {
            using (PooledBitWriter writer = PooledBitWriter.Get(stream))
            {
                writer.WriteInt32(msg.clientTimeCreated);
                writer.WriteInt32(msg.messageID);
                writer.WriteInt32(msg.timeCreated);
                writer.WriteUInt32(msg.entityId);
                writer.WriteInt16((byte)msg.shape);
                writer.WriteVector2(msg.pos);
                writer.WriteVector2(msg.speed);
                writer.WriteDouble(msg.size);
                CustomMessagingManager.SendNamedMessage("Replication", null, stream, "customChannel");
            }
        }
    }


    private void HandleReplicationMessage(ulong clientId, Stream stream)
    {
        ReplicationMessage replicationMessage = new ReplicationMessage();
        using (PooledBitReader reader = PooledBitReader.Get(stream))
        {
            replicationMessage.clientTimeCreated = reader.ReadInt32();
            replicationMessage.messageID = reader.ReadInt32();
            replicationMessage.timeCreated = reader.ReadInt32();
            replicationMessage.entityId = reader.ReadUInt32();
            replicationMessage.shape = (Config.Shape)reader.ReadInt16();
            replicationMessage.pos = reader.ReadVector2();
            replicationMessage.speed = reader.ReadVector2();
            replicationMessage.size = (float)reader.ReadDouble();
            ComponentsManager.Instance.SetComponent<ReplicationMessage>(replicationMessage.entityId, replicationMessage);
            if (!ComponentsManager.Instance.EntityContains<EntityComponent>(replicationMessage.entityId))
            {
                bool spawnFound = ComponentsManager.Instance.TryGetComponent(new EntityComponent(0), out SpawnInfo spawnInfo);

                if (!spawnFound)
                {
                    spawnInfo = new SpawnInfo(false);
                }
                spawnInfo.replicatedEntitiesToSpawn.Add(replicationMessage);
                ComponentsManager.Instance.SetComponent<SpawnInfo>(new EntityComponent(0), spawnInfo);
            }
        }
    }

    public void SendUserInputMessage(UserInputMessage msg)
    {
        using (PooledBitStream stream = PooledBitStream.Get())
        {
            using (PooledBitWriter writer = PooledBitWriter.Get(stream))
            {
                writer.WriteInt32(msg.messageID);
                writer.WriteInt32(msg.timeCreated);
                writer.WriteUInt32(msg.entityId);
                writer.WriteVector2(msg.speed);
                CustomMessagingManager.SendNamedMessage("UserInput", this.ServerClientId, stream, "customChannel");//inspiré par le message de Jacob Dorais et Louis-Philippe dans Discord
            }
        }
    }

    private void HandleUserInputMessage(ulong serverClientId, Stream stream)
    {
        UserInputMessage userInputMessage = new UserInputMessage();
        using (PooledBitReader reader = PooledBitReader.Get(stream))
        {
            userInputMessage.messageID = reader.ReadInt32();
            // userInputMessage.inputEntered = reader.ReadBool();
            userInputMessage.timeCreated = reader.ReadInt32();
            //!!!PRENDRE EN COMPTE TIMECREATED SEUL TRUC IMPORTANT
            userInputMessage.entityId = reader.ReadUInt32();
            userInputMessage.speed = reader.ReadVector2();

            ShapeComponent oldShape = ComponentsManager.Instance.GetComponent<ShapeComponent>(userInputMessage.entityId);
            ShapeComponent newShape = new ShapeComponent();
            newShape.pos = oldShape.pos;
            newShape.size = oldShape.size;
            newShape.speed = userInputMessage.speed;
            newShape.shape = oldShape.shape;

            if (ECSManager.Instance.Config.enableInputPrediction)
            {
              ClientTimeCreateComponent timeCreateComponent = new ClientTimeCreateComponent(userInputMessage.entityId, userInputMessage.timeCreated);
              ClientTimeCreateComponent.timedClientComponent[timeCreateComponent.clientIdTimeCreated] = newShape;
              ClientTimeCreateComponent.idTime[userInputMessage.entityId] = userInputMessage.timeCreated;
              // ComponentsManager.Instance.SetComponent<ClientTimeCreateComponent>(userInputMessage.entityId, timeCreateComponent);
            }

            ComponentsManager.Instance.SetComponent<ShapeComponent>(userInputMessage.entityId, newShape);
        }
    }

    public void RegisterClientNetworkHandlers()
    {
        CustomMessagingManager.RegisterNamedMessageHandler("Replication", HandleReplicationMessage);
    }

    public void RegisterServerNetworkHandlers()
    {
        CustomMessagingManager.RegisterNamedMessageHandler("UserInput", HandleUserInputMessage);
    }



    public new bool isServer { get { return GetConnectionStatus() == ConnectionStatus.isServer; } }
    public new bool isClient { get { return GetConnectionStatus() == ConnectionStatus.isClient; } }

    public enum ConnectionStatus
    {
        isClient,
        isServer,
        notConnected
    }

    public ConnectionStatus GetConnectionStatus()
    {
        if (IsConnectedClient)
        {
            return ConnectionStatus.isClient;
        }
        else if (IsServer && IsListening)
        {
            return ConnectionStatus.isServer;
        }
        else
        {
            return ConnectionStatus.notConnected;
        }
    }
}
