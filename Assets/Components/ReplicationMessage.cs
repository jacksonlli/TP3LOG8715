using UnityEngine;

public struct ReplicationMessage : IComponent
{
    public int messageID;
    public int timeCreated;

    // public int clientTimeCreated;

    public uint entityId;
    public Config.Shape shape;
    public Vector2 pos;
    public Vector2 speed;
    public float size;
}
