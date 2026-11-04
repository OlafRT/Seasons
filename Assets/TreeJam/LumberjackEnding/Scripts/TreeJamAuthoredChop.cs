using System;
using UnityEngine;

namespace TreeJam
{
    /// <summary>
    /// Explicit 2D cut-out poses for the axe sequence. Deliberately NO arm IK
    /// during chopping: elbows cannot flip to an alternative IK solution.
    /// Pose joints in the Scene view, Capture each pose in the Inspector.
    /// The existing puppet still owns the walking animation.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TreeJamAuthoredChop : MonoBehaviour
    {
        [Serializable]
        public struct Pose
        {
            public Vector3 torsoPosition;
            public float torso, head;
            public float leftShoulder, leftElbow, leftWrist;
            public float rightShoulder, rightElbow, rightWrist;
            public Vector3 axePosition;
            public float axe;
        }

        public TreeJamLumberjackPuppet puppet;
        public Transform leftWrist;
        public Transform rightWrist;
        [Tooltip("Only enable after the four poses have been captured. Disable to use the old procedural chopping animation.")]
        public bool useAuthoredMotion = true;
        [Tooltip("Fraction of a strike animation at which the blade hits the trunk. The ending's tree fall uses this exact moment.")]
        [Range(.45f, .80f)] public float impactTime = .63f;

        [Header("Editable poses (use the custom Inspector to preview and capture)")]
        public Pose resting;
        public Pose raised;
        public Pose impact;
        public Pose recovery;
        public bool posesCaptured;

        public bool IsReady { get { return isActiveAndEnabled && useAuthoredMotion && posesCaptured && puppet != null; } }

        private static float Z(Transform t) { return t == null ? 0f : Mathf.DeltaAngle(0f, t.localEulerAngles.z); }
        private static void SetZ(Transform t, float value)
        {
            if (t != null) t.localRotation = Quaternion.Euler(0f, 0f, value);
        }
        private static float LerpAngle(float a, float b, float t) { return Mathf.LerpAngle(a, b, t); }
        private static float Ease(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        public void AutoWire()
        {
            if (puppet == null) puppet = GetComponent<TreeJamLumberjackPuppet>();
            if (puppet != null)
            {
                if (leftWrist == null && puppet.leftElbow != null) leftWrist = puppet.leftElbow.Find("Left wrist");
                if (rightWrist == null && puppet.rightElbow != null) rightWrist = puppet.rightElbow.Find("Right wrist");
            }
        }

        public Pose ReadCurrent()
        {
            AutoWire();
            Pose p = new Pose();
            if (puppet == null) return p;
            p.torsoPosition = puppet.torso != null ? puppet.torso.localPosition : Vector3.zero;
            p.torso = Z(puppet.torso); p.head = Z(puppet.head);
            p.leftShoulder = Z(puppet.leftShoulder); p.leftElbow = Z(puppet.leftElbow); p.leftWrist = Z(leftWrist);
            p.rightShoulder = Z(puppet.rightShoulder); p.rightElbow = Z(puppet.rightElbow); p.rightWrist = Z(rightWrist);
            p.axePosition = puppet.axeGrip != null ? puppet.axeGrip.localPosition : Vector3.zero;
            p.axe = Z(puppet.axeGrip);
            return p;
        }

        public void Apply(Pose p)
        {
            AutoWire();
            if (puppet == null) return;
            if (puppet.torso != null) puppet.torso.localPosition = p.torsoPosition;
            SetZ(puppet.torso, p.torso); SetZ(puppet.head, p.head);
            SetZ(puppet.leftShoulder, p.leftShoulder); SetZ(puppet.leftElbow, p.leftElbow); SetZ(leftWrist, p.leftWrist);
            SetZ(puppet.rightShoulder, p.rightShoulder); SetZ(puppet.rightElbow, p.rightElbow); SetZ(rightWrist, p.rightWrist);
            if (puppet.axeGrip != null) puppet.axeGrip.localPosition = p.axePosition;
            SetZ(puppet.axeGrip, p.axe);
        }

        public void CaptureRestAndCreateStarterPoses()
        {
            AutoWire();
            if (puppet == null) return;
            puppet.PoseIdle();
            resting = ReadCurrent();
            // Intentionally simple starting poses. They are NOT a substitute
            // for posing the wrists and axe together using the Scene view.
            raised = resting;
            raised.torso = resting.torso - 8f;
            raised.torsoPosition += new Vector3(0f, .035f, 0f);
            raised.rightShoulder = resting.rightShoulder + 142f;
            raised.rightElbow = resting.rightElbow - 48f;
            raised.leftShoulder = resting.leftShoulder + 110f;
            raised.leftElbow = resting.leftElbow - 55f;
            raised.axe = resting.axe - 45f;
            impact = resting;
            impact.torsoPosition += new Vector3(-.12f, -.08f, 0f);
            impact.torso = resting.torso + 12f;
            impact.rightShoulder = resting.rightShoulder - 65f;
            impact.rightElbow = resting.rightElbow + 28f;
            impact.leftShoulder = resting.leftShoulder - 54f;
            impact.leftElbow = resting.leftElbow + 25f;
            impact.axe = resting.axe + 25f;
            recovery = impact;
            recovery.torsoPosition = Vector3.Lerp(impact.torsoPosition, resting.torsoPosition, .60f);
            recovery.rightShoulder = Mathf.LerpAngle(impact.rightShoulder, resting.rightShoulder, .60f);
            recovery.leftShoulder = Mathf.LerpAngle(impact.leftShoulder, resting.leftShoulder, .60f);
            recovery.rightElbow = Mathf.LerpAngle(impact.rightElbow, resting.rightElbow, .60f);
            recovery.leftElbow = Mathf.LerpAngle(impact.leftElbow, resting.leftElbow, .60f);
            recovery.axe = Mathf.LerpAngle(impact.axe, resting.axe, .60f);
            posesCaptured = true;
            Apply(resting);
        }

        public void SaveCurrentAs(int index)
        {
            Pose p = ReadCurrent();
            switch (index)
            {
                case 0: resting = p; break;
                case 1: raised = p; break;
                case 2: impact = p; break;
                case 3: recovery = p; break;
            }
            posesCaptured = true;
        }

        public void PreviewPose(int index)
        {
            switch (index)
            {
                case 0: Apply(resting); break;
                case 1: Apply(raised); break;
                case 2: Apply(impact); break;
                case 3: Apply(recovery); break;
            }
        }

        private static Pose Blend(Pose a, Pose b, float t)
        {
            Pose p = new Pose();
            p.torsoPosition = Vector3.LerpUnclamped(a.torsoPosition, b.torsoPosition, t);
            p.torso = LerpAngle(a.torso, b.torso, t); p.head = LerpAngle(a.head, b.head, t);
            p.leftShoulder = LerpAngle(a.leftShoulder, b.leftShoulder, t);
            p.leftElbow = LerpAngle(a.leftElbow, b.leftElbow, t);
            p.leftWrist = LerpAngle(a.leftWrist, b.leftWrist, t);
            p.rightShoulder = LerpAngle(a.rightShoulder, b.rightShoulder, t);
            p.rightElbow = LerpAngle(a.rightElbow, b.rightElbow, t);
            p.rightWrist = LerpAngle(a.rightWrist, b.rightWrist, t);
            p.axePosition = Vector3.LerpUnclamped(a.axePosition, b.axePosition, t);
            p.axe = LerpAngle(a.axe, b.axe, t);
            return p;
        }

        /// <summary>Called once per frame by the existing endgame cinematic.</summary>
        public void PoseAt(float t)
        {
            if (!posesCaptured) return;
            t = Mathf.Clamp01(t);
            Pose pose;
            float impactAt = Mathf.Clamp(impactTime, .54f, .78f);
            float readyAt = Mathf.Max(.18f, impactAt - .17f);
            if (t < readyAt)
            {
                pose = Blend(resting, raised, Ease(t / readyAt));
            }
            else if (t < impactAt)
            {
                float u = (t - readyAt) / (impactAt - readyAt);
                // The move into impact accelerates rather than easing to a stop.
                pose = Blend(raised, impact, u * u);
            }
            else if (t < impactAt + .075f)
            {
                pose = impact;
            }
            else if (t < .87f)
            {
                float u = (t - (impactAt + .075f)) / Mathf.Max(.01f, .87f - impactAt - .075f);
                pose = Blend(impact, recovery, Ease(u));
            }
            else
            {
                pose = Blend(recovery, resting, Ease((t - .87f) / .13f));
            }
            Apply(pose);
        }
    }
}
