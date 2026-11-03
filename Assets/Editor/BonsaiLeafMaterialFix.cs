#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TreeJam.Editor
{
    /// <summary>
    /// Create an actual .mat asset so the URP 2D unlit shader is retained in builds,
    /// then assign it to each bonsai tree in the active scene. Does not touch the art,
    /// the upgrade setup, or the resource/season systems.
    /// </summary>
    public static class BonsaiLeafMaterialFix
    {
        private const string MaterialPath = "Assets/TreeJam/Materials/BonsaiLeavesUnlit.mat";

        [MenuItem("Tools/Tree Jam/Fix Bonsai Leaf Visibility (Current Scene)")]
        private static void Install()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                EditorUtility.DisplayDialog("Bonsai Leaves", "No compatible unlit Sprite shader found. " +
                    "Check that your 2D URP renderer is installed.", "OK");
                return;
            }

            if (!AssetDatabase.IsValidFolder("Assets/TreeJam"))
                AssetDatabase.CreateFolder("Assets", "TreeJam");
            if (!AssetDatabase.IsValidFolder("Assets/TreeJam/Materials"))
                AssetDatabase.CreateFolder("Assets/TreeJam", "Materials");

            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "BonsaiLeavesUnlit" };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
                EditorUtility.SetDirty(material);
            }

            int changed = 0;
            foreach (OrganicBonsaiTree tree in Object.FindObjectsByType<OrganicBonsaiTree>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (tree.gameObject.scene != EditorSceneManager.GetActiveScene()) continue;
                Undo.RecordObject(tree, "Assign Bonsai Unlit Leaf Material");
                tree.leafMaterial = material;
                EditorUtility.SetDirty(tree);
                changed++;
            }
            AssetDatabase.SaveAssets();
            if (changed > 0) EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            EditorUtility.DisplayDialog("Bonsai leaves fixed",
                "Assigned the unlit leaf material to " + changed + " tree(s).\n\n" +
                "Stop and restart Play Mode to rebuild the generated leaves, then save the scene.", "OK");
        }
    }
}
#endif
