using UnityEngine;
using System;
using System.Collections.Generic;
public struct ClientHistory : IComponent//contient l'historique de cet entité
{
    public List<int> timeCreated;
    public uint entityId;
    public List<ShapeComponent> shapeComponents;
}
