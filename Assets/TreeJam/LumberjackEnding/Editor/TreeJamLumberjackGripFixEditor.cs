#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TreeJam.Editor
{
    public static class TreeJamLumberjackGripFixEditor
    {
        [MenuItem("Tools/Tree Jam/Lumberjack - Fix Crossed Arms and Walking Stance")]
        public static void Apply()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Stop Play Mode", "Stop Play Mode before applying the settings.", "OK");
                return;
            }
            var scene = SceneManager.GetActiveScene();
            TreeJamLumberjackPuppet puppet = null;
            foreach (var p in Object.FindObjectsByType<TreeJamLumberjackPuppet>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (p.gameObject.scene == scene) { puppet = p; break; }
            }
            if (puppet == null)
            {
                EditorUtility.DisplayDialog("No puppet found", "Open your Tree Jam scene containing the lumberjack puppet.", "OK");
                return;
            }
            Undo.RecordObject(puppet, "Correct lumberjack hand grip and gait");
            puppet.leftHandHandleOffset = -.12f;
            puppet.rightHandHandleOffset = .14f;
            puppet.handFollowAxeRotation = .85f;
            puppet.walkingPelvisDrop = .025f;
            puppet.stepLength = .24f;
            puppet.stepLift = .11f;
            puppet.walkingBounce = .02f;
            puppet.axeBodyDrop = .12f;
            EditorUtility.SetDirty(puppet);
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = puppet.gameObject;
            Debug.Log("Tree Jam: Corrected upper/lower axe grip mapping; reduced crouching walk. No scene rebuild or other gameplay changes.", puppet);
            EditorUtility.DisplayDialog("Lumberjack grip and gait updated",
                "The crossed-hand IK has been corrected, wrist alignment added, and walking crouch reduced. Save the scene and test the ending.", "OK");
        }
    }
}
#endif
