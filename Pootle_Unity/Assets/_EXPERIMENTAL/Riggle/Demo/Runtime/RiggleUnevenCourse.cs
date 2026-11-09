using UnityEngine;
namespace Docky.RiggleTools.Demo
{
    public static class RiggleUnevenCourse
    {
        public static float Height(float x, float z)
        {
            float rolling = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(1.5f,2.5f,z)) *
                (1 - Mathf.SmoothStep(0,1,Mathf.InverseLerp(5.5f,6.5f,z)));
            float wave = .075f * Mathf.Sin((z-1.5f)*3.1f) + .055f*Mathf.Sin(z*2.4f+x*3.5f);
            float ramp = .18f * Mathf.SmoothStep(0,1,Mathf.InverseLerp(6,7.5f,z));
            float cross = .07f*x * Mathf.SmoothStep(0,1,Mathf.InverseLerp(6,7,z)) *
                (1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(8.5f,9.5f,z)));
            float steps = z < 9.5f ? 0 : z < 10.25f ? .045f : z < 11 ? .09f : z < 11.75f ? .135f : .18f;
            return rolling * wave + ramp + cross - steps;
        }
        public static string Section(float z) => z < 1.5f ? "Flat baseline" : z < 6 ? "Rolling ground" : z < 9.5f ? "Ramp + cross-slope" : "Shallow descending steps";
    }
}
