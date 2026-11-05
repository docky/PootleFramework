using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
namespace Pootle
{
    [DisallowMultipleComponent, AddComponentMenu("Pootle/Turnstile Gate Builder")]
    public sealed class TurnstileGateBuilder : MonoBehaviour
    {
        public TurnstileGateHinge.LocalAxis hingeAxis=TurnstileGateHinge.LocalAxis.Y;
        [Min(1)] public int armCount=4;
        [Tooltip("Slot to omit, or -1 for all arms. Remaining arms keep their original angular spacing.")]
        public int missingArm=-1;
        [Min(1)] public float armLength=1.8f;
        [Min(.01f)] public float armWidth=.18f,armHeight=1.1f;
        [Min(0)] public float armBottom=.15f;
        public bool useArmHook=true;
        [Min(.01f)] public float hookRadius=.15f;
        public float defaultSpring=100,defaultDamper=15;
        public Material armMeshMaterial,hubMaterial;
        public Transform pivot;
        public Rigidbody baseRigidbody,pivotRigidbody;
        public HingeJoint hinge;
        [ContextMenu("Setup Or Rebuild")]
        public void SetupOrRebuild()
        {
#if UNITY_EDITOR
            if(Application.isPlaying)throw new System.InvalidOperationException("Rebuild turnstiles outside Play mode");
            Undo.RegisterFullObjectHierarchyUndo(gameObject,"Rebuild turnstile");
            var existing=transform.Find("Turnstile_Pivot");if(existing)Undo.DestroyObjectImmediate(existing.gameObject);
            var oldBase=transform.Find("Turnstile_Base");if(oldBase)Undo.DestroyObjectImmediate(oldBase.gameObject);
            baseRigidbody=GetComponent<Rigidbody>();if(!baseRigidbody)baseRigidbody=Undo.AddComponent<Rigidbody>(gameObject);
            baseRigidbody.isKinematic=true;baseRigidbody.useGravity=false;
            Primitive(PrimitiveType.Cylinder,"Turnstile_Base",transform,new Vector3(0,.08f,0),new Vector3(.7f,.08f,.7f),hubMaterial);
            var pivotObject=new GameObject("Turnstile_Pivot");pivotObject.transform.SetParent(transform,false);pivot=pivotObject.transform;
            pivotRigidbody=pivotObject.AddComponent<Rigidbody>();pivotRigidbody.mass=8;pivotRigidbody.useGravity=false;
            pivotRigidbody.angularDamping=.1f;pivotRigidbody.interpolation=RigidbodyInterpolation.Interpolate;
            // Speculative CCD covers the rotating arms, not only linear translation.
            pivotRigidbody.collisionDetectionMode=CollisionDetectionMode.ContinuousSpeculative;pivotRigidbody.solverIterations=12;
            hinge=pivotObject.AddComponent<HingeJoint>();hinge.connectedBody=baseRigidbody;hinge.axis=TurnstileGateHinge.AxisVector(hingeAxis);
            hinge.anchor=Vector3.zero;hinge.autoConfigureConnectedAnchor=false;hinge.connectedAnchor=Vector3.zero;
            hinge.enableCollision=false;hinge.useLimits=false;hinge.useMotor=false;
            var arms=new GameObject("Arms").transform;arms.SetParent(pivot,false);
            arms.localRotation=Quaternion.FromToRotation(Vector3.up,TurnstileGateHinge.AxisVector(hingeAxis));
            Primitive(PrimitiveType.Cylinder,"Hub",arms,new Vector3(0,armBottom+armHeight*.5f,0),new Vector3(.5f,armHeight*.5f,.5f),hubMaterial);
            int slots=Mathf.Max(1,armCount);
            for(int i=0;i<slots;i++)
            {
                if(i==missingArm)continue;
                var arm=new GameObject($"Arm_{i:00}").transform;arm.SetParent(arms,false);arm.localRotation=Quaternion.Euler(0,i*360f/slots,0);
                Primitive(PrimitiveType.Cube,"Arm Mesh",arm,new Vector3(armLength*.5f,armBottom+armHeight*.5f,0),new Vector3(armLength,armHeight,armWidth),armMeshMaterial);
                if(useArmHook)Primitive(PrimitiveType.Capsule,"Arm Hook",arm,new Vector3(armLength,armBottom+armHeight*.5f,0),new Vector3(hookRadius*2,Mathf.Max(hookRadius,armHeight*.5f),hookRadius*2),hubMaterial);
            }
            var runtime=GetComponent<TurnstileGateHinge>();if(!runtime)runtime=Undo.AddComponent<TurnstileGateHinge>(gameObject);
            runtime.hinge=hinge;runtime.pivot=pivot;runtime.axis=hingeAxis;runtime.spring=defaultSpring;runtime.damper=defaultDamper;
            runtime.SetupHingeFromSettings();EditorUtility.SetDirty(runtime);EditorUtility.SetDirty(this);
#endif
        }
#if UNITY_EDITOR
        static GameObject Primitive(PrimitiveType type,string name,Transform parent,Vector3 position,Vector3 scale,Material material)
        {
            var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=scale;
            if(material)go.GetComponent<Renderer>().sharedMaterial=material;
            return go;
        }
#endif
    }
}
