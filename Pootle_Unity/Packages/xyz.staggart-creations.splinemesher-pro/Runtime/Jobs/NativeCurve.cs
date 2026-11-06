// Spline Mesher Pro © Staggart Creations (http://staggart.xyz)
// COPYRIGHT PROTECTED UNDER THE UNITY ASSET STORE EULA (https://unity.com/legal/as-terms)
//
// ⚠️ WARNING: UNAUTHORIZED USE OR DISTRIBUTION IS STRICTLY PROHIBITED
// • Copying, referencing, or reverse-engineering this source code for the creation of new Asset Store or derivative products,
//   or any other publicly distributed content is strictly forbidden and will result in legal action.
// • Studying this file for the purpose of reproducing its functionality in your own assets or tools is not permitted.
// • If you are viewing this file as a reference, please close it immediately to avoid unintentional design influence or potential EULA violations.
// • Uploading this file or any derivative of it to a public GitHub or similar repository will trigger an automated DMCA takedown request.
// • Studying to understand for personal, educational or integration purposes is allowed, studying to reproduce is not.

using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace sc.splinemesher.pro.runtime
{
    /// <summary>
    /// Burst compatible AnimationCurve sampler
    /// </summary>
    [BurstCompile]
    public struct NativeCurve
    {
        [ReadOnly]
        private NativeArray<float> samples;

        private readonly int sampleCount;

        private const int MIN_SAMPLE_COUNT = 32;
        private const int MAX_SAMPLE_COUNT = 128;

        //Auto calculate the resolution. Higher for shorter curves, needing more detail, lower for long curves.
        private static int CalculateSampleCount(AnimationCurve curve)
        {
            if (curve.length <= 1) return 2;

            Keyframe f0 = curve.keys[0];
            Keyframe f1 = curve.keys[^1];
            
            float length = math.abs(f1.time - f0.time);

            float sampleRate = math.lerp(MAX_SAMPLE_COUNT, MIN_SAMPLE_COUNT, math.saturate(length));
            int sampleCount = (int)math.ceil(sampleRate);

            //Debug.Log($"Curve with length {length} yields {sampleCount} samples.");
            
            return math.min(MAX_SAMPLE_COUNT, sampleCount);
        }
        
        public NativeCurve(AnimationCurve curve, Allocator allocator)
        {
            sampleCount = CalculateSampleCount(curve);
            
            samples = new NativeArray<float>(sampleCount, allocator, NativeArrayOptions.UninitializedMemory);
            
            Update(curve);
        }

        public void Update(AnimationCurve curve)
        {
            unsafe
            {
                float* samplesPtr = (float*)samples.GetUnsafePtr();
    
                for (int i = 0; i < sampleCount; i++)
                {
                    float t = (float)i / (sampleCount - 1);
                    samplesPtr[i] = curve.Evaluate(t);
                }
            }
        }

        public float Sample(float t)
        {
            t = math.clamp(t, 0f, 1f);
            float scaled = t * (sampleCount - 1);
            int index = (int)math.floor(scaled);
            int nextIndex = math.min(index + 1, sampleCount - 1);
            float frac = scaled - index;

            return math.lerp(samples[index], samples[nextIndex], frac);
        }

        public void Dispose()
        {
            if (samples.IsCreated)
                samples.Dispose();
        }
    }
}