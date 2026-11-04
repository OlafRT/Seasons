using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TreeJam.Editor
{
    // Run after the existing TreeJamFungiMarket.cs replacement has compiled.
    public static class TreeJamFungalMailGlowInstaller
    {
        private const string ShaderPath = "Assets/TreeJam/FungalMailGlow/Shaders/TJ_FungalMailHDR.shader";
        private const string MaterialDirectory = "Assets/TreeJam/FungalMailGlow/Materials";
        private const string MaterialPath = MaterialDirectory + "/TJ_FungalMailGlow.mat";

        [MenuItem("Tools/Tree Jam/Enable Fungal Mail Glow (Current Scene)")]
        public static void Install()
        {
            TreeJamFungiMarket[] markets = Object.FindObjectsByType<TreeJamFungiMarket>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (markets == null || markets.Length == 0)
            {
                EditorUtility.DisplayDialog("Tree Jam", "No TreeJamFungiMarket component was found in the open scene. Open the Tree Jam scene first.", "OK");
                return;
            }

            AssetDatabase.ImportAsset(ShaderPath, ImportAssetOptions.ForceSynchronousImport);
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            if (shader == null) shader = Shader.Find("TreeJam/2D Fungal Mail HDR");
            if (shader == null || !shader.isSupported)
            {
                EditorUtility.DisplayDialog("Tree Jam", "The Fungal Mail HDR shader is missing or unsupported. Check the Console for shader errors before installing.", "OK");
                return;
            }

            Directory.CreateDirectory(MaterialDirectory);
            AssetDatabase.Refresh();
            Material glowMaterial = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (glowMaterial == null)
            {
                glowMaterial = new Material(shader) { name = "TJ_FungalMailGlow" };
                glowMaterial.SetColor("_HDRColor", Color.white);
                glowMaterial.SetFloat("_Brightness", 4.5f);
                AssetDatabase.CreateAsset(glowMaterial, MaterialPath);
            }
            else if (glowMaterial.shader != shader)
            {
                Undo.RecordObject(glowMaterial, "Update Fungal Mail Shader");
                glowMaterial.shader = shader;
                EditorUtility.SetDirty(glowMaterial);
            }

            int count = 0;
            foreach (TreeJamFungiMarket market in markets)
            {
                if (market == null) continue;
                if (market.packetCoreGlowMaterial == glowMaterial) continue;
                Undo.RecordObject(market, "Assign Fungal Mail Glow");
                market.packetCoreGlowMaterial = glowMaterial;
                EditorUtility.SetDirty(market);
                if (market.gameObject.scene.IsValid())
                    EditorSceneManager.MarkSceneDirty(market.gameObject.scene);
                count++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"Tree Jam: Fungal mail HDR glow material ready. Assigned {count} market(s). Material: {MaterialPath}. Only NEW trade packets created after this will use it.");
            EditorUtility.DisplayDialog("Tree Jam", $"Fungal mail glow is enabled. Updated {count} market(s).\n\nPress Play and make a NEW mushroom trade.\nEdit '{MaterialPath}' to adjust Glow Brightness (start at 4.5).", "OK");
        }
    }
}
