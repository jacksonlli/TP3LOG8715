using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct ClientTimeCreateComponent : IComponent
{
    // public static Dictionary<int, uint> timeId; //clé : temps

    public static Dictionary<uint, int> idTime; //clé : Id





    public idTimeStruct clientIdTimeCreated;
    public static Dictionary<idTimeStruct, IComponent> timedClientComponent;

    public ClientTimeCreateComponent(uint id, int clientTimeCreated)
    {
        this.clientIdTimeCreated = idTimeStruct(clientTimeCreated, id);
    }
}

public struct idTimeStruct
{
  public int clientTimeCreated;
  public uint id;

  public idTimeStruct(int clientTimeCreated, uint id)
  {
    this.clientTimeCreated = clientTimeCreated;
    this.id = id;
  }
}
