using UnityEditor;
using UnityEngine;

namespace Docky.RiggleTools.Editor
{
    internal static class RigglePrefabExporter
    {
        public static GameObject Save(Riggle rig)
        {
            if (!rig.GeneratedBones || rig.BakedSources.Count == 0 && rig.AttachedObjects.Count == 0) throw new System.InvalidOperationException("Bake the rig before exporting its prefab.");
            GameObject copy = Object.Instantiate(rig.gameObject);
            try
            {
                copy.name = rig.name;
                foreach (var imported in copy.GetComponentsInChildren<RiggleImportedSource>(true)) Object.DestroyImmediate(imported.gameObject);
                foreach (var references in copy.GetComponentsInChildren<RiggleReferencePoints>(true)) Object.DestroyImmediate(references);
                foreach (var origin in copy.GetComponentsInChildren<RiggleOriginReference>(true)) Object.DestroyImmediate(origin);
                foreach (var extra in copy.GetComponentsInChildren<RiggleAppendages>(true)) Object.DestroyImmediate(extra);
                foreach (var gallery in copy.GetComponentsInChildren<RigglePoseGallery>(true)) Object.DestroyImmediate(gallery);
                copy.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                foreach (var preview in copy.GetComponentsInChildren<RiggleHumanoidPreview>(true)) Object.DestroyImmediate(preview);
                foreach (var binding in copy.GetComponentsInChildren<RiggleSkinBinding>(true)) Object.DestroyImmediate(binding);
                foreach (var part in copy.GetComponentsInChildren<RiggleBodyPart>(true)) Object.DestroyImmediate(part);
                foreach (var edits in copy.GetComponentsInChildren<RiggleMeshEdits>(true)) Object.DestroyImmediate(edits);
                var authoring = copy.GetComponent<Riggle>();
                foreach (var source in authoring.BakedSources)
                    if (source.source)
                    {
                        var filter = source.source.GetComponent<MeshFilter>();
                        Object.DestroyImmediate(source.source);
                        if (filter) Object.DestroyImmediate(filter);
                    }
                Object.DestroyImmediate(authoring);
                string name = rig.name.Replace('/', '_').Replace('\\', '_').Replace(':', '_');
                string path = rig.AssetFolder.TrimEnd('/') + "/" + name + "_Skinned.prefab";
                var prefab = PrefabUtility.SaveAsPrefabAsset(copy, path, out bool success);
                if (!success) throw new System.InvalidOperationException("Could not save skinned prefab: " + path);
                return prefab;
            }
            finally { Object.DestroyImmediate(copy); }
        }
    }
}
