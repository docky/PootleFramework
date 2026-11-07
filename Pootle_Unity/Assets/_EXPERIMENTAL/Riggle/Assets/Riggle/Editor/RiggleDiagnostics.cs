using UnityEngine;

namespace Docky.RiggleTools.Editor
{
    internal static class RiggleDiagnostics
    {
        public static void Validate(Riggle rig)
        {
            int meshes = 0;
            int vertices = 0;
            int problems = 0;
            foreach (var item in rig.BakedSources)
            {
                if (item == null || !item.baked || !item.source)
                {
                    Debug.LogError("Riggle validation: a baked mesh or its source is missing.", rig);
                    problems++;
                    continue;
                }

                SkinnedMeshRenderer skin = item.baked;
                Mesh mesh = skin.sharedMesh;
                if (!mesh || skin.bones == null)
                {
                    Debug.LogError("Riggle validation: missing mesh or bones on " + skin.name, skin);
                    problems++;
                    continue;
                }
                meshes++;
                vertices += mesh.vertexCount;
                if (mesh.bindposes.Length != skin.bones.Length)
                {
                    Debug.LogError("Riggle validation: bind pose / bone array mismatch on " + skin.name, skin);
                    problems++;
                }
                var weights = mesh.boneWeights;
                if (weights == null || weights.Length != mesh.vertexCount)
                {
                    Debug.LogError("Riggle validation: vertex / weight array mismatch on " + skin.name, skin);
                    problems++;
                    continue;
                }
                int invalid = 0;
                foreach (BoneWeight w in weights)
                {
                    float sum = w.weight0 + w.weight1 + w.weight2 + w.weight3;
                    if (Mathf.Abs(sum - 1f) > 0.002f ||
                        w.boneIndex0 < 0 || w.boneIndex0 >= skin.bones.Length ||
                        (w.weight1 > 0 && w.boneIndex1 >= skin.bones.Length) ||
                        (w.weight2 > 0 && w.boneIndex2 >= skin.bones.Length) ||
                        (w.weight3 > 0 && w.boneIndex3 >= skin.bones.Length) ||
                        w.weight0 + 0.0001f < w.weight1 ||
                        w.weight1 + 0.0001f < w.weight2 ||
                        w.weight2 + 0.0001f < w.weight3)
                        invalid++;
                }
                if (invalid > 0)
                {
                    Debug.LogError($"Riggle validation: {invalid}/{weights.Length} invalid weight records in {skin.name}.", skin);
                    problems++;
                }
                if (!skin.rootBone)
                {
                    Debug.LogError("Riggle validation: missing rootBone on " + skin.name, skin);
                    problems++;
                }
            }
            if (problems == 0)
                Debug.Log($"Riggle validation passed: {meshes} SkinnedMeshRenderer(s), " +
                          $"{vertices} vertices with normalized 4-bone weights.", rig);
            else
                Debug.LogWarning($"Riggle validation found {problems} issue(s) across {meshes} mesh(es).", rig);
        }
    }
}
