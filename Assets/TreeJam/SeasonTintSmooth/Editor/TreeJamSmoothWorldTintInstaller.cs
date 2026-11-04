using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TreeJam.Editor
{
    public static class TreeJamSmoothWorldTintInstaller
    {
        [MenuItem("Tools/Tree Jam/Make Seasonal World Tint Gradual (Current Scene)")]
        public static void Install()
        {
            var seasons = Object.FindFirstObjectByType<TreeJamSeasonCycle>();
            var dayNight = Object.FindFirstObjectByType<TreeJamDayNightCycle>();
            var nordicSky = Object.FindFirstObjectByType<TreeJamNordicSky>();

            if (seasons == null || dayNight == null)
            {
                EditorUtility.DisplayDialog("Tree Jam - Smooth Tint",
                    "This scene needs TreeJamSeasonCycle and TreeJamDayNightCycle before installing the smoother.", "OK");
                return;
            }

            var smoother = Object.FindFirstObjectByType<TreeJamSmoothWorldTint>();
            if (smoother == null)
            {
                var go = new GameObject("SMOOTH SEASONAL TINT - EDIT HERE");
                Undo.RegisterCreatedObjectUndo(go, "Add smooth seasonal tint");
                go.transform.SetParent(seasons.transform, false);
                smoother = Undo.AddComponent<TreeJamSmoothWorldTint>(go);
            }
            Undo.RecordObject(smoother, "Assign smooth seasonal tint references");
            smoother.seasons = seasons;
            smoother.dayNight = dayNight;
            smoother.nordicSky = nordicSky;
            smoother.seasonOverlay = seasons.seasonTintOverlay;
            smoother.nightOverlay = dayNight.nightOverlay;
            smoother.daySky = nordicSky != null ? nordicSky.daySky : null;
            EditorUtility.SetDirty(smoother);
            EditorSceneManager.MarkSceneDirty(smoother.gameObject.scene);

            Selection.activeGameObject = smoother.gameObject;
            EditorUtility.DisplayDialog("Tree Jam - Smooth Tint",
                "Done! Select 'SMOOTH SEASONAL TINT - EDIT HERE' to change transition times.\n\n" +
                "Defaults: 20 seconds for season changes, 4.5 seconds for dawn/dusk fades.\n\n" +
                "No existing gameplay scripts or art were replaced.", "OK");
        }
    }
}
