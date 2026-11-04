using UnityEngine;
namespace Pootle
{
    /// <summary>Momentum-driven slide track. Contact normals are interpolated across mesh triangles.</summary>
    [DisallowMultipleComponent,AddComponentMenu("Pootle/Slide Surface")]
    public sealed class PootleSlideSurface : MonoBehaviour
    {
        [Min(1)] public float gravity=38;
        [Min(1)] public float maximumSpeed=24;
        [Min(0)] public float friction=.6f;
        [Min(0)] public float steeringAcceleration=12;
        [Min(0)] public float brakingDeceleration=16;
        [Min(0)] public float runoutDeceleration=5;
        private Mesh mesh;
        private Vector3[] vertices,normals;
        private int[] triangles;
        public Vector3 NormalAt(RaycastHit hit)
        {
            var collider=hit.collider as MeshCollider;
            if(!collider || !collider.sharedMesh || !collider.sharedMesh.isReadable || hit.triangleIndex<0)return hit.normal;
            if(mesh!=collider.sharedMesh)
            {mesh=collider.sharedMesh;vertices=mesh.vertices;normals=mesh.normals;triangles=mesh.triangles;}
            int index=hit.triangleIndex*3;
            if(index+2>=triangles.Length || normals.Length!=vertices.Length)return hit.normal;
            int a=triangles[index],b=triangles[index+1],c=triangles[index+2];
            Vector3 v0=vertices[b]-vertices[a],v1=vertices[c]-vertices[a];
            Vector3 v2=collider.transform.InverseTransformPoint(hit.point)-vertices[a];
            float d00=Vector3.Dot(v0,v0),d01=Vector3.Dot(v0,v1),d11=Vector3.Dot(v1,v1);
            float denominator=d00*d11-d01*d01;if(Mathf.Abs(denominator)<1e-10f)return hit.normal;
            float u=Mathf.Clamp01((d11*Vector3.Dot(v2,v0)-d01*Vector3.Dot(v2,v1))/denominator);
            float v=Mathf.Clamp01((d00*Vector3.Dot(v2,v1)-d01*Vector3.Dot(v2,v0))/denominator);
            Vector3 normal=collider.transform.worldToLocalMatrix.transpose.MultiplyVector(normals[a]*Mathf.Max(0,1-u-v)+normals[b]*u+normals[c]*v).normalized;
            return Vector3.Dot(normal,hit.normal)>.7f?normal:hit.normal;
        }
    }
}
