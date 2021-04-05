using UnityEngine;
using System.Collections.Generic;


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
                ySpeed++;
            }
            if (Input.GetKey(KeyCode.A))
            {
                xSpeed--;
            }
            if (Input.GetKey(KeyCode.S))
            {
                ySpeed--;
            }
            if (Input.GetKey(KeyCode.D))
            {
                xSpeed++;
            }

            float speedMagnitude = 10;
            Vector2 speed = new Vector2(xSpeed*speedMagnitude, ySpeed*speedMagnitude);
            CreateUserInputMessage(speed);
        }
    }

    private static void CreateUserInputMessage(Vector2 speed)
    {
        // creates messages from user input
        uint clientId = (uint)ECSManager.Instance.NetworkManager.LocalClientId;
        UserInputMessage msg = new UserInputMessage()
        {
            messageID = 0,
            timeCreated = Utils.SystemTime,
            entityId = clientId,
            speed = speed
        };
        ComponentsManager.Instance.SetComponent<UserInputMessage>(clientId, msg);
    }
}

