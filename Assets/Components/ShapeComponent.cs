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


// https://stackoverflow.com/questions/15199026/comparing-two-structs-using

// https://grantwinney.com/how-to-compare-two-objects-testing-for-equality-in-c/
    // public override bool Equals(Object obj)
    // {
    //    //Check for null and compare run-time types.
    //    if ((obj == null) || ! this.GetType().Equals(obj.GetType()))
    //    {
    //       return false;
    //    }
    //    else {
    //       ShapcComponent other = (ShapeComponent) obj;
    //       return ( (pos == other.pos) && (size == other.size) && (speed == other.speed) && (shape == other.shape));
    //    }
    // }


    public static bool operator ==(ShapeComponent x, ShapeComponent y)
    {
        return x.Equals(y);
    }

    public static bool operator !=(ShapeComponent x, ShapeComponent y)
    {
        return !(x == y);
    }




}
