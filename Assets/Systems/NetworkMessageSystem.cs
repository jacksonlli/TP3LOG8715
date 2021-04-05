public class NetworkMessageSystem : ISystem
{
    public string Name
    {
        get
        {
            return GetType().Name;
        }
    }

    // In charge of sending all messages pending sending
    public void UpdateSystem()
    {
        bool messagingInfoFound = ComponentsManager.Instance.TryGetComponent(new EntityComponent(0), out MessagingInfo messagingInfo);

        if (!messagingInfoFound)
        {
            messagingInfo = new MessagingInfo() { currentMessageId = 0 };
        }

        if (ECSManager.Instance.NetworkManager.isServer)
        {
            ComponentsManager.Instance.ForEach<ReplicationMessage>((entityID, msg) =>
            {
                msg.messageID = messagingInfo.currentMessageId++;
                ECSManager.Instance.NetworkManager.SendReplicationMessage(msg);
            });
        }
        
        if (ECSManager.Instance.NetworkManager.isClient)
        {
            ComponentsManager.Instance.ForEach<UserInputMessage>((entityID, msg) =>
            {
                msg.messageID = messagingInfo.currentMessageId++;//pas certain si on devrait garder cela
                ECSManager.Instance.NetworkManager.SendUserInputMessage(msg);
            });
        }

        ComponentsManager.Instance.SetComponent<MessagingInfo>(new EntityComponent(0), messagingInfo);
    }
}