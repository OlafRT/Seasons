using System;
using UnityEditor;
using UnityEngine;

namespace TreeJam.Editor
{
    /// <summary>Install only one new camera component. Nothing else in the scene is edited.</summary>
    public static class TreeJamGrowthCameraInstaller
    {
        [MenuItem("Tools/Tree Jam/Add Gentle Growth Camera (Current Scene)")]
        public static void Install()
        {
            Camera camera = Camera.main;
            if (camera == null) camera = UnityEngine.Object.FindFirstObjectByType<Camera>();
            if (camera == null)
            {
                EditorUtility.DisplayDialog("Tree Jam", "No Camera found in the current scene.", "OK");
                return;
            }

            TreeJamGrowthCameraZoom zoom = camera.GetComponent<TreeJamGrowthCameraZoom>();
            if (zoom == null) zoom = Undo.AddComponent<TreeJamGrowthCameraZoom>(camera.gameObject);
            GameObject tree = GameObject.Find("The Tree");
            if (tree != null && zoom.treeRoot == null) zoom.treeRoot = tree.transform;
            SpriteRenderer[] sprites = UnityEngine.Object.FindObjectsByType<SpriteRenderer>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (zoom.daySky == null)
                zoom.daySky = FindArt(sprites, new string[] { "01 SKY / BACKGROUND", "Day Sky", "Sky", "01 SKY" });
            if (zoom.soil == null)
                zoom.soil = FindArt(sprites, new string[] { "03 SOIL TEXTURE", "Soil", "Soil Texture", "03 SOIL" });
            EditorUtility.SetDirty(zoom);
            Selection.activeGameObject = camera.gameObject;
            EditorGUIUtility.PingObject(camera.gameObject);
            EditorUtility.DisplayDialog("Tree Jam Growth Camera",
                "Added/located the gentle zoom component on the Camera.\n\n" +
                "In the Inspector, verify: Tree Root, Day Sky and Soil. " +
                "The camera itself was NOT changed in Edit Mode.\n\n" +
                "Play to see the closer starting frame, then grow a branch to test zoom.", "OK");
        }

        private static SpriteRenderer FindArt(SpriteRenderer[] candidates, string[] names)
        {
            foreach (string name in names)
                foreach (SpriteRenderer sr in candidates)
                    if (sr.gameObject.name.Equals(name, StringComparison.OrdinalIgnoreCase)
                        && sr.sprite != null)
                        return sr;
            return null;
        }
    }
}
