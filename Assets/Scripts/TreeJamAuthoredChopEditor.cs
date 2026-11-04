#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TreeJam.Editor
{
    public static class TreeJamAuthoredChopInstaller
    {
        [MenuItem("Tools/Tree Jam/Lumberjack - Install Editable Chop Poses")]
        public static void Install()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Stop Play Mode", "Install and edit poses outside Play Mode so they are saved.", "OK");
                return;
            }
            TreeJamEndgameLoop ending = Object.FindFirstObjectByType<TreeJamEndgameLoop>(FindObjectsInactive.Include);
            if (ending == null || ending.lumberjack == null)
            {
                EditorUtility.DisplayDialog("Missing lumberjack", "Open the game scene with your existing LUMBERJACK ENDING object.", "OK");
                return;
            }
            var puppet = ending.lumberjack;
            var component = puppet.GetComponent<TreeJamAuthoredChop>();
            if (component == null)
            {
                component = Undo.AddComponent<TreeJamAuthoredChop>(puppet.gameObject);
                component.puppet = puppet;
                component.AutoWire();
                component.CaptureRestAndCreateStarterPoses();
                EditorUtility.SetDirty(component);
            }
            Selection.activeGameObject = puppet.gameObject;
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorUtility.DisplayDialog("Pose editor installed",
                "Select the lumberjack's new Tree Jam Authored Chop component. Enable 'Show puppet for editing', then preview and adjust each pose in the Scene view. Click Capture to save each pose.\n\nWhen done, preview Rest and hide the puppet again. Existing walking and rebirth are unaffected.",
                "OK");
        }
    }

    [CustomEditor(typeof(TreeJamAuthoredChop))]
    public sealed class TreeJamAuthoredChopEditor : UnityEditor.Editor
    {
        private float timeline;
        private static readonly string[] Names = { "Rest", "Raised / wind-up", "Blade impact", "Recovery" };

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var chop = (TreeJamAuthoredChop)target;
            EditorGUILayout.Space(10);
            EditorGUILayout.HelpBox(
                "POSE IN EDIT MODE (not Play Mode):\n" +
                "1. Show the lumberjack and place him next to the tree.\n" +
                "2. Preview a pose, rotate the shoulder/elbow/hand pivots and axe in Scene view.\n" +
                "3. Capture that pose. Repeat for wind-up, blade impact, and recovery.\n" +
                "4. The blade should touch the actual bark in the Impact pose.\n" +
                "5. Preview Rest and hide puppet after editing. No runtime IK is used.", MessageType.Info);

            if (Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Pose capture is disabled during Play Mode so changes aren't lost.", MessageType.Warning);
                return;
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(chop.gameObject.activeSelf ? "Hide puppet" : "Show puppet"))
            {
                Undo.RecordObject(chop.gameObject, "Toggle lumberjack preview");
                chop.gameObject.SetActive(!chop.gameObject.activeSelf);
                MarkDirty(chop);
            }
            if (GUILayout.Button("Place beside tree")) PlaceByTree(chop);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(6);

            for (int i = 0; i < Names.Length; ++i)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(Names[i], GUILayout.Width(130));
                int index = i;
                if (GUILayout.Button("Preview"))
                {
                    RecordRig(chop);
                    chop.PreviewPose(index);
                    MarkDirty(chop);
                }
                if (GUILayout.Button("Capture current"))
                {
                    Undo.RecordObject(chop, "Capture lumberjack " + Names[i]);
                    chop.SaveCurrentAs(index);
                    MarkDirty(chop);
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.Space(8);
            EditorGUI.BeginChangeCheck();
            float next = EditorGUILayout.Slider("Scrub chop", timeline, 0f, 1f);
            if (EditorGUI.EndChangeCheck())
            {
                timeline = next;
                RecordRig(chop);
                chop.PoseAt(timeline);
                MarkDirty(chop);
            }
            if (GUILayout.Button("Restore Rest Pose"))
            {
                RecordRig(chop);
                chop.PreviewPose(0);
                MarkDirty(chop);
            }
            EditorGUILayout.HelpBox("The preset poses are only a starting point. Adjusting the actual arm and axe transforms in the Scene view is what makes the action convincing.", MessageType.None);
        }

        private static void RecordRig(TreeJamAuthoredChop chop)
        {
            chop.AutoWire();
            var rig = chop.puppet;
            if (rig == null) return;
            List<Object> targets = new List<Object>();
            Transform[] joints = {
                rig.torso, rig.head, rig.leftShoulder, rig.leftElbow, chop.leftWrist,
                rig.rightShoulder, rig.rightElbow, chop.rightWrist, rig.axeGrip
            };
            foreach (Transform joint in joints) if (joint != null) targets.Add(joint);
            Undo.RecordObjects(targets.ToArray(), "Preview lumberjack pose");
        }

        private static void PlaceByTree(TreeJamAuthoredChop chop)
        {
            var ending = Object.FindFirstObjectByType<TreeJamEndgameLoop>(FindObjectsInactive.Include);
            if (ending == null || ending.tree == null) return;
            var p = chop.transform;
            Undo.RecordObject(p, "Place lumberjack beside tree");
            Undo.RecordObject(chop.gameObject, "Show lumberjack beside tree");
            float sign = ending.startingSide < 0 ? -1f : 1f;
            float x = ending.tree.transform.position.x + sign * Mathf.Min(ending.approachDistance, .70f);
            float y = ending.soil != null
                ? ending.soil.HeightOr(x, ending.tree.transform.position.y)
                : ending.tree.transform.position.y;
            p.position = new Vector3(x, y, 0f);
            Vector3 scale = p.localScale;
            scale.x = Mathf.Abs(scale.x) * (sign < 0 ? 1f : -1f);
            p.localScale = scale;
            chop.gameObject.SetActive(true);
            chop.PreviewPose(0);
            MarkDirty(chop);
        }

        private static void MarkDirty(TreeJamAuthoredChop chop)
        {
            EditorUtility.SetDirty(chop);
            if (SceneManager.GetActiveScene().IsValid())
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            SceneView.RepaintAll();
        }
    }
}
#endif
