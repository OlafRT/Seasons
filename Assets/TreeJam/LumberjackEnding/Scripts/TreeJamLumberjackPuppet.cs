using UnityEngine;

namespace TreeJam
{
    /// <summary>
    /// Cutout puppet with foot-planting two-bone IK and a two-handed, target-driven
    /// axe stroke. Uses the ORIGINAL nine sprite pieces and rig transforms.
    /// No animator controller or rig rebuild is required.
    /// </summary>
    public sealed class TreeJamLumberjackPuppet : MonoBehaviour
    {
        public Transform head;
        public Transform torso;
        public Transform leftShoulder, rightShoulder;
        public Transform leftElbow, rightElbow;
        public Transform leftHip, rightHip;
        public Transform leftKnee, rightKnee;
        public Transform axeGrip;
        public Transform leftAnkle, rightAnkle;

        [Header("Walk - planted feet / leg IK")]
        [Min(.3f)] public float distancePerWalkCycle = 1.12f;
        [Range(.45f, .8f)] public float stanceFraction = .62f;
        [Range(.08f, .6f)] public float stepLength = .24f; // half of local foot travel
        [Range(.02f, .4f)] public float stepLift = .11f;
        [Range(0f, .26f)] public float walkingPelvisDrop = .025f;
        [Range(0f, .07f)] public float walkingBounce = .018f;
        [Range(0f, 35f)] public float armSwingAngle = 10f;

        // Backwards-compatible fields kept so existing inspector data survives.
        [HideInInspector] public float strideAngle = 18f;
        [HideInInspector] public float kneeBendAngle = 20f;
        [HideInInspector] public float ankleCounterRotation = .8f;
        [HideInInspector] public float axeWindupAngle = 126f;
        [HideInInspector] public float axeImpactAngle = -58f;
        [HideInInspector] public float axeSwingBodyLean = .9f;

        [Header("Two-handed axe swing")]
        [Range(.1f, .4f)] public float axeForwardLunge = .12f;
        [Range(.05f, .38f)] public float axeBodyDrop = .12f;
        [Tooltip("Local position on the axe sprite from the Axe grip to the cutting edge. " +
                 "It is based on the supplied LumberJack_Axe.png and can be tuned.")]
        public Vector2 bladeEdgeFromGrip = new Vector2(-.13f, .64f);

        [Header("Two hands on the same handle")]
        [Tooltip("The right (near) hand grips above the left hand along the axe shaft. " +
                 "These values are in axe-local coordinates and never swap during a swing.")]
        [Range(.04f, .30f)] public float rightHandHandleOffset = .14f;
        [Range(-.30f, -.04f)] public float leftHandHandleOffset = -.12f;
        [Tooltip("Twist each hand to match the axe shaft instead of allowing the wrist to follow the forearm.")]
        [Range(0f, 1f)] public float handFollowAxeRotation = .85f;
        [Range(-120f, -35f)] public float contactAxeAngle = -90f;
        [Range(-65f, 35f)] public float raisedAxeAngle = 18f;
        [Header("Strong axe strike - edit in Inspector")]
        [Tooltip("How high the hand grip rises during the axe wind-up (world units).")]
        [Range(.40f, 1.15f)] public float axeWindupHeight = .77f;
        [Tooltip("Maximum permitted elbow/shoulder rotation speed in degrees per second. Prevents sudden IK flips.")]
        [Range(240f, 1400f)] public float maxArmRotationDegreesPerSecond = 640f;

        /// <summary>Normalized strike moment, used also by the sound/wood-chip event.</summary>
        public const float ContactTime = .61f;

        private bool captured;
        private Vector3 torsoRest, leftHipRestPosition, rightHipRestPosition;
        private Quaternion torsoRestRot, headRest;
        private Quaternion lsRest, rsRest, leRest, reRest, lwRest, rwRest, lhRest, rhRest, lkRest, rkRest, laRest, raRest, axeRest;
        private Transform leftWrist, rightWrist;
        private Vector3 oldAxeLocalPosition;
        private Transform oldAxeParent;
        private Quaternion oldAxeLocalRotation;
        private Vector2 carriedGrip, carriedLeftWrist, carriedRightWrist, contactEdge;
        private float carriedAxeAngle;
        private bool chopPrepared;
        // Retain the previous solved arm pose between frames. PoseIdle resets joint transforms
        // before every pose, so damping must use these stored values, not current transforms.
        private Quaternion previousLeftShoulder, previousLeftElbow;
        private Quaternion previousRightShoulder, previousRightElbow;
        private bool havePreviousArmPose;

        private void Awake() => CaptureNeutralPose();

        [ContextMenu("Capture current rig as neutral (Edit Mode only)")]
        public void CaptureNeutralPose()
        {
            if (torso == null || leftShoulder == null || rightShoulder == null ||
                leftHip == null || rightHip == null) return;
            if (leftAnkle == null && leftKnee != null) leftAnkle = leftKnee.Find("Left ankle");
            if (rightAnkle == null && rightKnee != null) rightAnkle = rightKnee.Find("Right ankle");
            if (leftWrist == null && leftElbow != null) leftWrist = leftElbow.Find("Left wrist");
            if (rightWrist == null && rightElbow != null) rightWrist = rightElbow.Find("Right wrist");
            torsoRest = torso.localPosition;
            leftHipRestPosition = leftHip.localPosition;
            rightHipRestPosition = rightHip.localPosition;
            torsoRestRot = torso.localRotation;
            headRest = head != null ? head.localRotation : Quaternion.identity;
            lsRest = leftShoulder.localRotation;
            rsRest = rightShoulder.localRotation;
            leRest = leftElbow != null ? leftElbow.localRotation : Quaternion.identity;
            reRest = rightElbow != null ? rightElbow.localRotation : Quaternion.identity;
            lwRest = leftWrist != null ? leftWrist.localRotation : Quaternion.identity;
            rwRest = rightWrist != null ? rightWrist.localRotation : Quaternion.identity;
            lhRest = leftHip.localRotation;
            rhRest = rightHip.localRotation;
            lkRest = leftKnee != null ? leftKnee.localRotation : Quaternion.identity;
            rkRest = rightKnee != null ? rightKnee.localRotation : Quaternion.identity;
            laRest = leftAnkle != null ? leftAnkle.localRotation : Quaternion.identity;
            raRest = rightAnkle != null ? rightAnkle.localRotation : Quaternion.identity;
            axeRest = axeGrip != null ? axeGrip.localRotation : Quaternion.identity;
            captured = true;
        }

        private static float Smooth(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }
        private static Quaternion Z(float angle) => Quaternion.Euler(0f, 0f, angle);
        private static float Angle(Quaternion q) => Mathf.DeltaAngle(0f, q.eulerAngles.z);
        private Vector2 InRoot(Transform joint) => transform.InverseTransformPoint(joint.position);
        private static Vector2 Rotate(Vector2 v, float degrees)
        {
            float a = degrees * Mathf.Deg2Rad;
            return new Vector2(v.x * Mathf.Cos(a) - v.y * Mathf.Sin(a),
                               v.x * Mathf.Sin(a) + v.y * Mathf.Cos(a));
        }

        public void PoseIdle()
        {
            if (!captured) CaptureNeutralPose();
            if (!captured) return;
            torso.localPosition = torsoRest;
            torso.localRotation = torsoRestRot;
            leftHip.localPosition = leftHipRestPosition;
            rightHip.localPosition = rightHipRestPosition;
            leftShoulder.localRotation = lsRest;
            rightShoulder.localRotation = rsRest;
            leftHip.localRotation = lhRest;
            rightHip.localRotation = rhRest;
            if (leftElbow != null) leftElbow.localRotation = leRest;
            if (rightElbow != null) rightElbow.localRotation = reRest;
            if (leftWrist != null) leftWrist.localRotation = lwRest;
            if (rightWrist != null) rightWrist.localRotation = rwRest;
            if (leftKnee != null) leftKnee.localRotation = lkRest;
            if (rightKnee != null) rightKnee.localRotation = rkRest;
            if (leftAnkle != null) leftAnkle.localRotation = laRest;
            if (rightAnkle != null) rightAnkle.localRotation = raRest;
            if (head != null) head.localRotation = headRest;
            if (axeGrip != null && !chopPrepared) axeGrip.localRotation = axeRest;
        }

        /// <summary>
        /// Analytic 2-bone IK. The goal is expressed in the puppet ROOT's local
        /// coordinates. Bends the elbow/knee but never stretches the sprite art.
        /// </summary>
        private void Reach(Transform upper, Transform lower, Transform tip,
                           Quaternion restUpper, Quaternion restLower,
                           Vector2 targetRoot, float bendSide, bool levelFoot = false,
                           Quaternion restFoot = default)
        {
            if (upper == null || lower == null || tip == null) return;
            Vector2 rootWorld = transform.TransformPoint(new Vector3(targetRoot.x, targetRoot.y));
            Vector2 goal = upper.parent.InverseTransformPoint(rootWorld);
            Vector2 origin = upper.localPosition;
            Vector2 upperBone = lower.localPosition;
            Vector2 lowerBone = tip.localPosition;
            float a = upperBone.magnitude;
            float b = lowerBone.magnitude;
            if (a < .01f || b < .01f) return;
            Vector2 delta = goal - origin;
            float length = Mathf.Max(.0001f, delta.magnitude);
            Vector2 forward = delta / length;
            float distance = Mathf.Clamp(length, Mathf.Abs(a - b) + .0001f, a + b - .0001f);
            float along = (a * a - b * b + distance * distance) / (2f * distance);
            float perpendicular = Mathf.Sqrt(Mathf.Max(0f, a * a - along * along));
            Vector2 knee = origin + forward * along +
                new Vector2(-forward.y, forward.x) * (perpendicular * Mathf.Sign(bendSide));
            Vector2 clampedGoal = origin + forward * distance;
            Vector2 firstNeutral = (Vector2)(restUpper * (Vector3)upperBone);
            float upperAngle = Vector2.SignedAngle(firstNeutral, knee - origin);
            upper.localRotation = Z(upperAngle) * restUpper;
            Vector2 secondNeutral = (Vector2)(upper.localRotation * restLower * (Vector3)lowerBone);
            float lowerAngle = Vector2.SignedAngle(secondNeutral, clampedGoal - knee);
            lower.localRotation = Z(lowerAngle) * restLower;
            if (levelFoot)
                tip.localRotation = Quaternion.Inverse(upper.localRotation * lower.localRotation) * restFoot;
        }

        /// <summary>
        /// Each foot has a stance phase where it moves backwards RELATIVE to
        /// the advancing body, so it remains planted in world space. The quick
        /// swing phase lifts the foot and carries it forward for the next step.
        /// </summary>
        private Vector2 FootGoal(float travelled, float offset, float hipX, float blend)
        {
            float phase = Mathf.Repeat(travelled / Mathf.Max(.3f, distancePerWalkCycle) + offset, 1f);
            float stance = Mathf.Clamp(stanceFraction, .45f, .8f);
            float x, y = 0f;
            float half = stepLength;
            if (phase < stance)
            {
                x = Mathf.Lerp(half, -half, phase / stance);
            }
            else
            {
                float p = (phase - stance) / (1f - stance);
                x = Mathf.Lerp(-half, half, Smooth(p));
                y = Mathf.Sin(p * Mathf.PI) * stepLift;
            }
            return new Vector2(hipX + x * blend, .09f + y * blend);
        }

        public void PoseWalking(float distanceTravelled, float blend = 1f)
        {
            PoseIdle();
            if (!captured) return;
            blend = Mathf.Clamp01(blend);
            float phase = Mathf.Repeat(distanceTravelled / Mathf.Max(.3f, distancePerWalkCycle), 1f);
            float bounce = walkingBounce * (1f - Mathf.Cos(phase * 4f * Mathf.PI)) * .5f * blend;
            float drop = walkingPelvisDrop * blend;
            torso.localPosition = torsoRest + new Vector3(0f, -drop + bounce, 0f);
            leftHip.localPosition = leftHipRestPosition + new Vector3(0f, -drop + bounce, 0f);
            rightHip.localPosition = rightHipRestPosition + new Vector3(0f, -drop + bounce, 0f);
            float armPhase = Mathf.Sin(phase * 2f * Mathf.PI) * blend;
            leftShoulder.localRotation = lsRest * Z(-armSwingAngle * armPhase);
            rightShoulder.localRotation = rsRest * Z(armSwingAngle * .32f * armPhase);
            if (leftKnee != null && leftAnkle != null)
                Reach(leftHip, leftKnee, leftAnkle, lhRest, lkRest,
                    FootGoal(distanceTravelled, 0f, leftHipRestPosition.x, blend), 1f, true, laRest);
            if (rightKnee != null && rightAnkle != null)
                Reach(rightHip, rightKnee, rightAnkle, rhRest, rkRest,
                    FootGoal(distanceTravelled, .5f, rightHipRestPosition.x, blend), 1f, true, raRest);
            if (head != null) head.localRotation = headRest * Z(-armPhase * 1.0f);
        }

        /// <summary>
        /// Detaches ONLY the axe transform from the carrying wrist while
        /// chopping, allowing us to animate the cutting edge to an actual tree
        /// contact point. Arms are IK'd to the moving handle (two-handed chop).
        /// Must be called after the actor reaches its final standing location.
        /// </summary>
        public void PrepareChopping(Vector3 worldContactPoint)
        {
            PoseIdle();
            if (axeGrip == null || rightWrist == null || leftWrist == null) return;
            if (chopPrepared) FinishChopping();
            carriedLeftWrist = InRoot(leftWrist);
            carriedRightWrist = InRoot(rightWrist);
            oldAxeParent = axeGrip.parent;
            oldAxeLocalPosition = axeGrip.localPosition;
            oldAxeLocalRotation = axeGrip.localRotation;
            axeGrip.SetParent(transform, true);
            carriedGrip = axeGrip.localPosition;
            carriedAxeAngle = Angle(axeGrip.localRotation);
            contactEdge = transform.InverseTransformPoint(worldContactPoint);
            chopPrepared = true;
            previousLeftShoulder = leftShoulder != null ? leftShoulder.localRotation : Quaternion.identity;
            previousLeftElbow = leftElbow != null ? leftElbow.localRotation : Quaternion.identity;
            previousRightShoulder = rightShoulder != null ? rightShoulder.localRotation : Quaternion.identity;
            previousRightElbow = rightElbow != null ? rightElbow.localRotation : Quaternion.identity;
            havePreviousArmPose = true;
        }

        public void FinishChopping()
        {
            if (!chopPrepared) return;
            chopPrepared = false;
            havePreviousArmPose = false;
            if (axeGrip != null && oldAxeParent != null)
            {
                axeGrip.SetParent(oldAxeParent, false);
                axeGrip.localPosition = oldAxeLocalPosition;
                axeGrip.localRotation = oldAxeLocalRotation;
            }
            PoseIdle();
        }

        /// <summary>The actual world-space position of the axe's cutting edge.</summary>
        public Vector3 CurrentBladeEdgeWorld()
        {
            if (axeGrip == null) return transform.position;
            return axeGrip.TransformPoint(new Vector3(bladeEdgeFromGrip.x, bladeEdgeFromGrip.y, 0f));
        }

        /// <summary>
        /// Physical, impact-led chop. Most of the motion is a deliberate wind-up,
        /// followed by a SHORT accelerating strike. The cutting edge arrives at
        /// the supplied tree contact point precisely at ContactTime.
        /// </summary>
        public void PoseChop(float time)
        {
            PoseIdle();
            if (!captured || !chopPrepared || axeGrip == null) return;
            time = Mathf.Clamp01(time);

            // Keep the working two-hand grip mapping. Raise the handle toward
            // shoulder level so the axe head actually comes up above the character.
            // Old scenes often store -18 for raisedAxeAngle; enforce a real wind-up.
            float highAngle = Mathf.Max(raisedAxeAngle, 14f);
            Vector2 impactGrip = contactEdge - Rotate(bladeEdgeFromGrip, contactAxeAngle);
            Vector2 highGrip = impactGrip + new Vector2(-.10f, axeWindupHeight);
            Vector2 recoilGrip = impactGrip + new Vector2(-.065f, .16f);

            Vector2 position;
            float angle, bodyX, bodyY, handBlend;
            if (time < .44f)
            {
                // Gather strength, lifting from the carry pose.
                float u = Smooth(time / .44f);
                position = Vector2.LerpUnclamped(carriedGrip, highGrip, u);
                angle = Mathf.LerpAngle(carriedAxeAngle, highAngle, u);
                bodyX = Mathf.Lerp(0f, -.055f, u);
                bodyY = Mathf.Lerp(0f, .035f, u);
                handBlend = u;
            }
            else if (time < .50f)
            {
                // Tiny hold before accelerating into the cut.
                position = highGrip;
                angle = highAngle;
                bodyX = -.055f;
                bodyY = .035f;
                handBlend = 1f;
            }
            else if (time < ContactTime)
            {
                float u = (time - .50f) / (ContactTime - .50f);
                // Quadratic progress accelerates all the way into impact. No
                // ease-out means the axe doesn't gently come to rest on the bark.
                float strike = u * u;
                position = Vector2.LerpUnclamped(highGrip, impactGrip, strike);
                angle = Mathf.LerpAngle(highAngle, contactAxeAngle, strike);
                bodyX = Mathf.Lerp(-.055f, axeForwardLunge, strike);
                bodyY = Mathf.Lerp(.035f, -axeBodyDrop, strike);
                handBlend = 1f;
            }
            else if (time < .69f)
            {
                // Blade bites into wood. Final hit stays here while tree falls.
                position = impactGrip;
                angle = contactAxeAngle;
                bodyX = axeForwardLunge;
                bodyY = -axeBodyDrop;
                handBlend = 1f;
            }
            else if (time < .82f)
            {
                float u = Smooth((time - .69f) / .13f);
                position = Vector2.Lerp(impactGrip, recoilGrip, u);
                angle = Mathf.LerpAngle(contactAxeAngle, -42f, u);
                bodyX = Mathf.Lerp(axeForwardLunge, .035f, u);
                bodyY = Mathf.Lerp(-axeBodyDrop, -.035f, u);
                handBlend = 1f;
            }
            else
            {
                float u = Smooth((time - .82f) / .18f);
                position = Vector2.Lerp(recoilGrip, carriedGrip, u);
                angle = Mathf.LerpAngle(-42f, carriedAxeAngle, u);
                bodyX = Mathf.Lerp(.035f, 0f, u);
                bodyY = Mathf.Lerp(-.035f, 0f, u);
                handBlend = 1f - u;
            }

            // Drive the torso through the stroke; keep a braced, planted stance.
            torso.localPosition = torsoRest + new Vector3(bodyX, bodyY, 0f);
            if (head != null) head.localRotation = headRest * Z(-bodyX * 20f);
            leftHip.localPosition = leftHipRestPosition + new Vector3(0f, bodyY * .20f, 0f);
            rightHip.localPosition = rightHipRestPosition + new Vector3(0f, bodyY * .20f, 0f);
            if (leftKnee != null && leftAnkle != null)
                Reach(leftHip, leftKnee, leftAnkle, lhRest, lkRest,
                    new Vector2(leftHipRestPosition.x - .16f, .09f), 1f, true, laRest);
            if (rightKnee != null && rightAnkle != null)
                Reach(rightHip, rightKnee, rightAnkle, rhRest, rkRest,
                    new Vector2(rightHipRestPosition.x + .15f, .09f), 1f, true, raRest);

            axeGrip.localPosition = new Vector3(position.x, position.y, 0f);
            axeGrip.localRotation = Z(angle);

            // Preserve the corrected hand ordering from the previous grip fix.
            Vector2 leftHandGoal = position + Rotate(new Vector2(0f, leftHandHandleOffset), angle);
            Vector2 rightHandGoal = position + Rotate(new Vector2(0f, rightHandHandleOffset), angle);
            float grip = Smooth(handBlend);
            if (leftElbow != null && leftWrist != null)
                Reach(leftShoulder, leftElbow, leftWrist, lsRest, leRest,
                      Vector2.Lerp(carriedLeftWrist, leftHandGoal, grip), -1f);
            if (rightElbow != null && rightWrist != null)
                Reach(rightShoulder, rightElbow, rightWrist, rsRest, reRest,
                      Vector2.Lerp(carriedRightWrist, rightHandGoal, grip), 1f);

            // Arm IK sometimes swings to a very different elbow solution at a
            // near-straight extension. Constrain angular velocity across frames.
            // Do this AFTER Reach; otherwise PoseIdle resets the easing each call.
            if (havePreviousArmPose)
            {
                float maxTurn = maxArmRotationDegreesPerSecond * Mathf.Max(Time.deltaTime, .001f);
                if (leftShoulder != null) leftShoulder.localRotation = Quaternion.RotateTowards(
                    previousLeftShoulder, leftShoulder.localRotation, maxTurn);
                if (leftElbow != null) leftElbow.localRotation = Quaternion.RotateTowards(
                    previousLeftElbow, leftElbow.localRotation, maxTurn);
                if (rightShoulder != null) rightShoulder.localRotation = Quaternion.RotateTowards(
                    previousRightShoulder, rightShoulder.localRotation, maxTurn);
                if (rightElbow != null) rightElbow.localRotation = Quaternion.RotateTowards(
                    previousRightElbow, rightElbow.localRotation, maxTurn);
            }
            if (leftShoulder != null) previousLeftShoulder = leftShoulder.localRotation;
            if (leftElbow != null) previousLeftElbow = leftElbow.localRotation;
            if (rightShoulder != null) previousRightShoulder = rightShoulder.localRotation;
            if (rightElbow != null) previousRightElbow = rightElbow.localRotation;
            havePreviousArmPose = true;

            float wristBlend = grip * handFollowAxeRotation;
            if (leftWrist != null)
                leftWrist.rotation = Quaternion.Slerp(leftWrist.rotation, axeGrip.rotation, wristBlend);
            if (rightWrist != null)
                rightWrist.rotation = Quaternion.Slerp(rightWrist.rotation, axeGrip.rotation, wristBlend);
        }

    }
}
