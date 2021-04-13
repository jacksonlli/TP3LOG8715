using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct ShapeComponent : IComponent
{
    public Vector2 pos;
    public float size;
    public Vector2 speed;
    public Config.Shape shape;

    public ShapeComponent(Vector2 pos, float size, Vector2 speed, Config.Shape shape)
    {
        this.pos = pos;
        this.size = size;
        this.speed = speed;
        this.shape = shape;
    }


    public void LogInfo()
    {
      string text = "";
      text += pos.ToString() + "\n";
      text += speed.ToString() + "\n";
      Debug.Log(text);

    }

    public bool isCloseEnough(ShapeComponent other)
    {
      return Vector2.Distance(pos, other.pos) <= 0.5;
    }



}
