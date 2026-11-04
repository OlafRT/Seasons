#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TreeJam.Editor
{
    /// <summary>
    /// Adjusts the existing cutout puppet's saved tuning; never rebuilds the rig.
    /// </summary>
    public static class TreeJamLumberjackMotionHotfixEditor
    {
        [MenuItem("Tools/Tree Jam/Lumberjack - Apply Smooth Motion Settings (Current Scene)")]
        public static void Apply()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Stop Play Mode", "Stop Play Mode before adjusting the puppet.", "OK");
                return;
            }

            var scene = SceneManager.GetActiveScene();
            TreeJamEndgameLoop ending = null;
            foreach (TreeJamEndgameLoop candidate in Object.FindObjectsByType<TreeJamEndgameLoop>(
                FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (candidate.gameObject.scene == scene) { ending = candidate; break; }
            }
            if (ending == null)
            {
                EditorUtility.DisplayDialog("Lumberjack not found",
                    "Open your gameplay scene with the Lumberjack Ending object installed. Nothing changed.", "OK");
                return;
            }

            TreeJamLumberjackPuppet puppet = ending.lumberjack;
            if (puppet == null)
            {
                EditorUtility.DisplayDialog("Lumberjack reference missing",
                    "Assign the LUMBERJACK PUPPET object to the Endgame Loop's Lumberjack field first. Nothing changed.", "OK");
                return;
            }

            Undo.RecordObject(puppet, "Smooth lumberjack puppet motion");
            Undo.RecordObject(ending, "Walk into camera and smooth axe swings");

            puppet.strideAngle = 17f;
            puppet.armSwingAngle = 11f;
            puppet.walkingBounce = .02f;
            puppet.kneeBendAngle = 18f;
            puppet.distancePerWalkCycle = 1.12f;
            puppet.ankleCounterRotation = .8f;
            puppet.axeWindupAngle = 126f;
            puppet.axeImpactAngle = -58f;
            puppet.axeSwingBodyLean = .9f;

            // The original rig already has ankle pivots; link them without
            // changing the hierarchy or the puppet's user-adjusted joint positions.
            if (puppet.leftKnee != null && puppet.leftAnkle == null)
                puppet.leftAnkle = puppet.leftKnee.Find("Left ankle");
            if (puppet.rightKnee != null && puppet.rightAnkle == null)
                puppet.rightAnkle = puppet.rightKnee.Find("Right ankle");

            ending.offscreenEntryPadding = .6f;
            ending.walkingUnitsPerSecond = 1.7f;
            ending.secondsPerStrike = 1.35f;

            EditorUtility.SetDirty(puppet);
            EditorUtility.SetDirty(ending);
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = puppet.gameObject;
            Debug.Log("Tree Jam: Smooth lumberjack motion settings applied. Existing puppet, scene, art, and rebirth logic were preserved.", puppet);
            EditorUtility.DisplayDialog("Motion settings applied",
                "The lumberjack will now walk in from outside the camera and use softer gait and axe swing settings. Save the scene, enter Play Mode, and test the ending.", "OK");
        }
    }
}
#endif
