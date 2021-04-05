using UnityEngine;

public class UserInputSystem : ISystem
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
        if (ECSManager.Instance.NetworkManager.isClient)
        {
            float xSpeed = 0;
            float ySpeed = 0;

            if (Input.GetKey(KeyCode.W))
            {
                ySpeed--;
            }
            if (Input.GetKey(KeyCode.A))
            {
                xSpeed--;
            }
            if (Input.GetKey(KeyCode.S))
            {
                ySpeed++;
            }
            if (Input.GetKey(KeyCode.D))
            {
                xSpeed++;
            }

            Vector2 speed = new Vector2(xSpeed, ySpeed);
            Debug.Log("Speed: " + speed);
        }
    }
}
