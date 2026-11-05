using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DavidJalbert.TinyCarController
{
    public class TCCWheelCollider : MonoBehaviour
    {
        private List<float> dotSideList = new List<float>();
        private List<TCCSurface> surfaces = new List<TCCSurface>();
        private float sideDotLastFrame = 0;
        private bool hadCollisionLastFrame = false;
        private TCCWheel wheel = null;

        public TCCSurfaceModifier surfaceModifier = new TCCSurfaceModifier();

        public void initialize(TCCWheel w)
        {
            wheel = w;
        }

        void FixedUpdate()
        {
            if (dotSideList.Count > 0)
            {
                foreach (float d in dotSideList)
                {
                    sideDotLastFrame += d;
                }
                sideDotLastFrame /= dotSideList.Count;
                dotSideList.Clear();
                hadCollisionLastFrame = true;
            }
            else
            {
                sideDotLastFrame = 0;
                hadCollisionLastFrame = false;
            }

            if (surfaces.Count > 0)
            {
                surfaceModifier.clear();
                foreach (TCCSurface s in surfaces)
                {
                    surfaceModifier += s.surfaceModifier;
                }
                surfaceModifier /= surfaces.Count;
            }
            else
            {
                surfaceModifier.resetToDefault();
            }

            surfaces.Clear();
        }

        public bool isTouchingGround()
        {
            return hadCollisionLastFrame;
        }

        public bool isStableOnGround()
        {
            return hadCollisionLastFrame && sideDotLastFrame >= 0.5f;
        }

        public TCCSurfaceModifier getSurfaceModifier()
        {
            return surfaceModifier;
        }

        private void OnCollisionEnter(Collision collision)
        {
            wheel.onCollisionEnterActions?.Invoke(collision);
        }

        private void OnCollisionExit(Collision collision)
        {
            wheel.onCollisionExitActions?.Invoke(collision);
        }

        private void OnCollisionStay(Collision collision)
        {
            for (int i = 0; i < collision.contactCount; i++)
            {
                ContactPoint contact = collision.GetContact(i);
                if (contact.otherCollider.GetComponent<TCCWheel>() == null)
                {
                    TCCSurface surface = contact.otherCollider.GetComponentInParent<TCCSurface>();

                    if (surface != null)
                    {
                        surfaces.Add(surface);
                    }

                    float dotSide = 1f - Mathf.Abs(Vector3.Dot(contact.normal, transform.right));
                    dotSideList.Add(dotSide);
                }
            }

            wheel.onCollisionStayActions?.Invoke(collision);
        }

        private void OnTriggerEnter(Collider other)
        {
            wheel.onTriggerEnterActions?.Invoke(other);
        }

        private void OnTriggerExit(Collider other)
        {
            wheel.onTriggerExitActions?.Invoke(other);
        }

        private void OnTriggerStay(Collider other)
        {
            wheel.onTriggerStayActions?.Invoke(other);
        }
    }
}