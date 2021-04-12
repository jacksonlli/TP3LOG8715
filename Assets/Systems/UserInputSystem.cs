using UnityEngine;
using System.Collections.Generic;


public class UserInputSystem : ISystem
{
    public int compteur = 0;
    public string Name
    {
        get
        {
            return GetType().Name;
        }
    }

    // private Config config;
    public void UpdateSystem()
    {


        if (ECSManager.Instance.NetworkManager.isClient)
        {

            compteur++;
            bool inputEntered = false;

            float xSpeed = 0;
            float ySpeed = 0;

            if (Input.GetKey(KeyCode.W))
            {
                ySpeed++;
                inputEntered = true;
            }
            if (Input.GetKey(KeyCode.A))
            {
                xSpeed--;
                inputEntered = true;
            }
            if (Input.GetKey(KeyCode.S))
            {
                ySpeed--;
                inputEntered = true;
            }
            if (Input.GetKey(KeyCode.D))
            {
                xSpeed++;
                inputEntered = true;
            }

            float speedMagnitude = 10;
            Vector2 speed = new Vector2(xSpeed*speedMagnitude, ySpeed*speedMagnitude);

            CreateUserInputMessage(speed);

            if (ECSManager.Instance.Config.enableInputPrediction)
            {
              uint clientId = (uint)ECSManager.Instance.NetworkManager.LocalClientId;
              ShapeComponent playerComponent = ComponentsManager.Instance.GetComponent<ShapeComponent>(clientId);
              ClientTimeCreateComponent timeCreateComponent = new ClientTimeCreateComponent(clientId, compteur);
              ClientTimeCreateComponent.timedClientComponent[timeCreateComponent.clientIdTimeCreated] = playerComponent;
              ClientTimeCreateComponent.idTime[clientId] = compteur;

              playerComponent.speed = speed;
              ComponentsManager.Instance.SetComponent<ShapeComponent>(clientId, playerComponent);


            }

        }
    }

    private /*static*/ void CreateUserInputMessage(Vector2 speed)
    {
        // creates messages from user input
        uint clientId = (uint)ECSManager.Instance.NetworkManager.LocalClientId;
        UserInputMessage msg = new UserInputMessage()
        {
            messageID = 0,
            timeCreated = compteur,
            entityId = clientId,
            speed = speed
        };
        ComponentsManager.Instance.SetComponent<UserInputMessage>(clientId, msg);
    }
}
