#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TreeJam.Editor
{
    /// <summary>
    /// Installs the actual solid-leaf Sprite and exposes it on OrganicBonsaiTree.
    /// Does not rebuild or replace the scene, or touch UI/environment/gameplay.
    /// </summary>
    public static class BonsaiCrispLeafInstaller
    {
        private const string SpritePath = "Assets/TreeJam/Resources/BonsaiLeafSolid.png";

        [MenuItem("Tools/Tree Jam/Install Crisp Bonsai Leaves (Current Scene)")]
        private static void Install()
        {
            TextureImporter importer = AssetImporter.GetAtPath(SpritePath) as TextureImporter;
            if (importer == null)
            {
                EditorUtility.DisplayDialog("Bonsai Leaves", "Can't find " + SpritePath +
                    ". Make sure you've copied the entire patch (including Resources).", "OK");
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.spritePixelsPerUnit = 128;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();

            Sprite leafSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
            if (leafSprite == null)
            {
                EditorUtility.DisplayDialog("Bonsai Leaves", "The PNG did not import as a Sprite. " +
                    "Select the PNG and ensure Texture Type is Sprite (2D and UI).", "OK");
                return;
            }

            int changed = 0;
            foreach (OrganicBonsaiTree tree in Object.FindObjectsByType<OrganicBonsaiTree>(
                FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (tree.gameObject.scene != EditorSceneManager.GetActiveScene()) continue;
                Undo.RecordObject(tree, "Install Crisp Bonsai Leaves");
                tree.customLeafSprite = leafSprite;
                tree.foliagePadRadius = Mathf.Max(tree.foliagePadRadius, .36f);
                tree.individualLeafSize = Mathf.Max(tree.individualLeafSize, .33f);
                tree.leafColor = new Color(.24f, .70f, .24f, 1f);
                tree.leafHighlight = new Color(.48f, .87f, .33f, 1f);
                EditorUtility.SetDirty(tree);
                changed++;
            }
            if (changed > 0) EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorUtility.DisplayDialog("Bonsai Leaves",
                "Assigned a solid opaque-center leaf Sprite to " + changed +
                " bonsai tree(s).\n\nStop and restart Play Mode so the runtime leaves rebuild. " +
                "The Custom Leaf Sprite field can be changed any time.", "OK");
        }
    }
}
#endif
