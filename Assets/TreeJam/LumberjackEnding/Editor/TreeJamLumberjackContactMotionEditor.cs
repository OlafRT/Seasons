#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TreeJam.Editor
{
    public static class TreeJamLumberjackContactMotionEditor
    {
        [MenuItem("Tools/Tree Jam/Lumberjack - Apply Planted Walk + Real Axe Contact")]
        public static void Apply()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Stop Play Mode", "Stop Play Mode before updating the scene settings.", "OK");
                return;
            }
            Scene scene = SceneManager.GetActiveScene();
            TreeJamEndgameLoop ending = null;
            foreach (TreeJamEndgameLoop candidate in Object.FindObjectsByType<TreeJamEndgameLoop>(
                FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (candidate.gameObject.scene == scene) { ending = candidate; break; }
            }
            if (ending == null || ending.lumberjack == null)
            {
                EditorUtility.DisplayDialog("Lumberjack not found",
                    "Open the saved gameplay scene with 'LUMBERJACK ENDING - EDIT HERE' and its existing puppet. No changes made.", "OK");
                return;
            }
            TreeJamLumberjackPuppet p = ending.lumberjack;
            if (p.leftKnee == null || p.rightKnee == null || p.leftElbow == null ||
                p.rightElbow == null || p.axeGrip == null)
            {
                EditorUtility.DisplayDialog("Incomplete puppet",
                    "Some limb/axe references are missing. Restore those in the Puppet component first. No changes made.", "OK");
                return;
            }
            Undo.RecordObject(p, "Set planted-foot gait and target-based axe stroke");
            Undo.RecordObject(ending, "Set tree-contact chop timings");
            p.distancePerWalkCycle = 1.12f;
            p.stanceFraction = .62f;
            p.stepLength = .34f;
            p.stepLift = .16f;
            p.walkingPelvisDrop = .14f;
            p.walkingBounce = .018f;
            p.armSwingAngle = 10f;
            p.axeForwardLunge = .12f;
            p.axeBodyDrop = .21f;
            p.contactAxeAngle = -90f;
            p.raisedAxeAngle = -18f;
            p.bladeEdgeFromGrip = new Vector2(-.13f, .64f);
            if (p.leftAnkle == null) p.leftAnkle = p.leftKnee.Find("Left ankle");
            if (p.rightAnkle == null) p.rightAnkle = p.rightKnee.Find("Right ankle");
            ending.approachDistance = .61f;
            ending.secondsPerStrike = 1.75f;
            ending.chopContactHeight = .69f;
            ending.chopBarkOffset = .10f;
            ending.showWoodChips = true;
            EditorUtility.SetDirty(p);
            EditorUtility.SetDirty(ending);
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = p.gameObject;
            Debug.Log("Tree Jam: Planted-foot walking and two-handed axe contact settings applied. Existing puppet art, upgrade scene and rebirth logic unchanged.", p);
            EditorUtility.DisplayDialog("Contact animation settings applied",
                "Save the scene. In Play Mode, TEST - Trigger lumberjack ending. If the axe blade is off by a few pixels, adjust 'Blade Edge From Grip' and 'Chop Contact Height' in the Inspectors.", "OK");
        }
    }
}
#endif
