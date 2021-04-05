using UnityEngine;
//Essentiellement la même chose que Replication Message pour maintenant. 
//Pourrait etre modifié pour accomoder les besoins du user input

public struct UserInputMessage : IComponent
{
    public int messageID;
    public int timeCreated;

    public uint entityId;
    public Vector2 speed;
}