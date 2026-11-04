#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TreeJam.Editor
{
    public static class TreeJamLumberjackForcefulChopEditor
    {
        [MenuItem("Tools/Tree Jam/Lumberjack - Apply Forceful Chop + Immediate Fall")]
        public static void Apply()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Stop Play Mode", "Stop Play Mode before applying these settings.", "OK");
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
                    "Open the gameplay scene with the existing lumberjack ending. Nothing changed.", "OK");
                return;
            }

            TreeJamLumberjackPuppet puppet = ending.lumberjack;
            Undo.RecordObject(puppet, "Forceful axe swing / stable elbow setup");
            Undo.RecordObject(ending, "Axe strike timing / actual trunk contact");

            // Keep the two-hand grip mapping repaired by the previous patch.
            puppet.rightHandHandleOffset = .14f;
            puppet.leftHandHandleOffset = -.12f;
            puppet.raisedAxeAngle = 18f;
            puppet.axeWindupHeight = .77f;
            puppet.maxArmRotationDegreesPerSecond = 640f;
            puppet.axeForwardLunge = .13f;
            puppet.axeBodyDrop = .11f;

            // Old .69 contact height forced the hand grip far below shoulder reach,
            // producing the dramatic elbow IK snap visible in the test recording.
            ending.chopContactHeight = 1.04f;
            ending.secondsPerStrike = 1.42f;

            EditorUtility.SetDirty(puppet);
            EditorUtility.SetDirty(ending);
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = puppet.gameObject;
            Debug.Log("Tree Jam: Forceful chop, reachable axe contact height, and instant final-hit felling configured. Gait and character references unchanged.", puppet);
            EditorUtility.DisplayDialog("Lumberjack swing updated",
                "Save the scene and trigger the ending in Play Mode. The final blow begins the tree fall on impact, without recovering the axe first.", "OK");
        }
    }
}
#endif
