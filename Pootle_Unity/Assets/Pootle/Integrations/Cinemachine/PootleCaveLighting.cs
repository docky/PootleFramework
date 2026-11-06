using UnityEngine;
using UnityEngine.Rendering;
using Sirenix.OdinInspector;

namespace Pootle
{
    // Single-player prototype: restore the outdoor environment when leaving or disabling this component.
    [DefaultExecutionOrder(100)]
    public sealed class PootleCaveLighting : MonoBehaviour
    {
        public Transform player;
        public Light outdoorSun;
        public Light avatarLight;
        public Bounds localInterior = new Bounds(new Vector3(9, 6, 33), new Vector3(52, 18, 36));
        public Color caveAmbient = new Color(.18f, .22f, .27f);
        [Min(0)] public float avatarIntensity = 3.5f;
        [Min(.1f)] public float avatarRange = 7.5f;
        public Color avatarColour = new Color(1, .8f, .55f);
        [ShowInInspector, ReadOnly] public bool IsInside { get; private set; }
        AmbientMode originalMode;
        Color originalLight, originalSky, originalEquator, originalGround;
        SphericalHarmonicsL2 originalProbe;
        float originalIntensity, originalReflection;
        bool captured, sunWasEnabled;
        Color appliedAmbient;
        void OnEnable() => BeginSession();
        public void BeginSession()
        {
            if (captured) return;
            originalMode = RenderSettings.ambientMode; originalLight = RenderSettings.ambientLight;
            originalSky = RenderSettings.ambientSkyColor; originalEquator = RenderSettings.ambientEquatorColor;
            originalGround = RenderSettings.ambientGroundColor; originalProbe = RenderSettings.ambientProbe;
            originalIntensity = RenderSettings.ambientIntensity; originalReflection = RenderSettings.reflectionIntensity;
            sunWasEnabled = outdoorSun && outdoorSun.enabled; captured = true;
            Refresh();
        }
        void LateUpdate() => Refresh();
        public void Refresh()
        {
            bool inside = player && localInterior.Contains(transform.InverseTransformPoint(player.position));
            if (inside != IsInside || inside && caveAmbient != appliedAmbient)
            {
                IsInside = inside;
                if (inside)
                {
                    appliedAmbient = caveAmbient;
                    if (outdoorSun) outdoorSun.enabled = false;
                    RenderSettings.ambientMode = AmbientMode.Flat;
                    RenderSettings.ambientLight = caveAmbient;
                    RenderSettings.ambientIntensity = 1;
                    RenderSettings.reflectionIntensity = 0;
                    var probe = new SphericalHarmonicsL2(); probe.AddAmbientLight(caveAmbient.linear);
                    RenderSettings.ambientProbe = probe;
                }
                else RestoreEnvironment();
            }
            if (avatarLight)
            {
                avatarLight.enabled = inside;
                avatarLight.intensity = avatarIntensity; avatarLight.range = avatarRange; avatarLight.color = avatarColour;
            }
        }
        void RestoreEnvironment()
        {
            if (!captured) return;
            if (outdoorSun) outdoorSun.enabled = sunWasEnabled;
            RenderSettings.ambientMode = originalMode; RenderSettings.ambientLight = originalLight;
            RenderSettings.ambientSkyColor = originalSky; RenderSettings.ambientEquatorColor = originalEquator;
            RenderSettings.ambientGroundColor = originalGround; RenderSettings.ambientIntensity = originalIntensity;
            RenderSettings.reflectionIntensity = originalReflection; RenderSettings.ambientProbe = originalProbe;
        }
        void OnDisable() => EndSession();
        public void EndSession() { RestoreEnvironment(); IsInside = false; captured = false; if (avatarLight) avatarLight.enabled = false; }
    }
}
