using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DavidJalbert.TinyCarController
{
    public class TCCBodyCollider : MonoBehaviour
    {
        private TCCBody body = null;

        public void initialize(TCCBody b)
        {
            body = b;
        }

        private void OnCollisionEnter(Collision collision)
        {
            body.onCollisionEnterActions?.Invoke(collision);
        }

        private void OnCollisionExit(Collision collision)
        {
            body.onCollisionExitActions?.Invoke(collision);
        }

        private void OnCollisionStay(Collision collision)
        {
            body.onCollisionStayActions?.Invoke(collision);
        }

        private void OnTriggerEnter(Collider other)
        {
            body.onTriggerEnterActions?.Invoke(other);
        }

        private void OnTriggerExit(Collider other)
        {
            body.onTriggerExitActions?.Invoke(other);
        }

        private void OnTriggerStay(Collider other)
        {
            body.onTriggerStayActions?.Invoke(other);
        }
    }
}