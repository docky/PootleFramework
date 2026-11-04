using UnityEngine;
namespace Pootle
{
    [DefaultExecutionOrder(150),DisallowMultipleComponent]
    public sealed class PootleBallVisual : MonoBehaviour
    {
        public PootleCharacter character;
        public Transform ball;
        private Vector3 previous;
        private bool wasRolling;
        private void OnEnable(){previous=transform.position;}
        private void LateUpdate()
        {
            if(!character || !ball)return;
            bool rolling=character.IsRolling;
            ball.gameObject.SetActive(rolling);
            if(character.visualRoot)character.visualRoot.gameObject.SetActive(!rolling);
            ball.localPosition=character.Motor.center;
            Vector3 movement=transform.position-previous;previous=transform.position;
            if(rolling && wasRolling && movement.magnitude<2)
            {
                Vector3 axis=Vector3.Cross(Vector3.up,movement);
                if(axis.sqrMagnitude>.000001f)ball.Rotate(axis.normalized,movement.magnitude/character.Motor.Radius*Mathf.Rad2Deg,Space.World);
            }
            wasRolling=rolling;
        }
        private void OnDisable()
        {
            if(ball)ball.gameObject.SetActive(false);
            if(character && character.visualRoot)character.visualRoot.gameObject.SetActive(true);
        }
    }
}
