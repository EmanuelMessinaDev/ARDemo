using Oculus.Interaction.HandGrab.Visuals;
using Oculus.Interaction.Input;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Context menu on <see cref="HandGhost"/> that poses an OpenXR ghost hand in a thumb-index pinch, e.g. to show
/// in the scene how an object should be grabbed. Only the finger joints change, the wrist pose is kept.
/// </summary>
/// <remarks>
/// The rotations are the OpenXR target pose of the Interaction SDK sample PingPongBall's hand grab poses,
/// in <see cref="FingersMetadata.HAND_JOINT_IDS"/> order (thumb 1-3, then metacarpal to distal of each finger).
/// </remarks>
public static class HandGhostPinchPose
{
    private static readonly Quaternion[] RightPinch =
    {
        new Quaternion(0.08023148f, 0.4439247f, -0.5818709f, -0.67669797f),
        new Quaternion(0.17247732f, 0.05319548f, -0.099665776f, 0.9785135f),
        new Quaternion(0.2892252f, -0.03206094f, 0.10085805f, 0.951393f),
        new Quaternion(0f, 0f, 0f, 1f),
        new Quaternion(0.24585672f, -0.00863884f, 0.014446782f, 0.9691601f),
        new Quaternion(0.4369164f, -0.004812201f, -0.026378555f, 0.89910245f),
        new Quaternion(0.24052286f, 0.020834664f, -0.02368045f, 0.970131f),
        new Quaternion(0f, 0f, 0f, 1f),
        new Quaternion(0.051835753f, 0.051465604f, -0.009066325f, 0.99728745f),
        new Quaternion(-0.0019782668f, 0.0043789316f, -0.011228229f, 0.9999255f),
        new Quaternion(-0.09300703f, 0.004611793f, -0.034319557f, 0.99506325f),
        new Quaternion(0f, 0f, 0f, 1f),
        new Quaternion(0.049813494f, 0.12310341f, -0.053159356f, 0.98971635f),
        new Quaternion(0.005676022f, 0.0027898266f, -0.033632524f, 0.9994143f),
        new Quaternion(-0.025028544f, -0.029179487f, -0.003477463f, 0.9992548f),
        new Quaternion(0.018311795f, 0.14034282f, -0.20703602f, 0.9680417f),
        new Quaternion(0.02812923f, -0.004071386f, 0.091113046f, 0.99543494f),
        new Quaternion(-0.0132860495f, 0.042937737f, -0.03761665f, 0.998281f),
        new Quaternion(-0.024018833f, -0.049170665f, 0.0006447454f, 0.9985014f),
    };

    private static readonly Quaternion[] LeftPinch =
    {
        new Quaternion(-0.08023148f, 0.4439247f, -0.5818709f, 0.67669797f),
        new Quaternion(-0.17247732f, 0.05319548f, -0.099665776f, -0.9785135f),
        new Quaternion(0.2892252f, 0.032060985f, -0.10085807f, 0.95139307f),
        new Quaternion(0f, 0f, 0f, 1f),
        new Quaternion(0.24585676f, 0.008638882f, -0.014446792f, 0.96916014f),
        new Quaternion(0.43691644f, 0.0048121167f, 0.026378576f, 0.89910245f),
        new Quaternion(-0.24052286f, 0.020834664f, -0.02368045f, -0.970131f),
        new Quaternion(0f, 0f, 0f, 1f),
        new Quaternion(-0.051835753f, 0.051465604f, -0.009066325f, -0.99728745f),
        new Quaternion(0.0019782668f, 0.0043789316f, -0.011228229f, -0.9999255f),
        new Quaternion(0.09300703f, 0.004611793f, -0.034319557f, -0.99506325f),
        new Quaternion(0f, 0f, 0f, 1f),
        new Quaternion(-0.049813494f, 0.12310341f, -0.053159356f, -0.98971635f),
        new Quaternion(-0.005676022f, 0.0027898266f, -0.033632524f, -0.9994143f),
        new Quaternion(-0.025028542f, 0.02917953f, 0.003477463f, 0.9992548f),
        new Quaternion(-0.018311795f, 0.14034282f, -0.20703602f, -0.9680417f),
        new Quaternion(0.028129233f, 0.0040713013f, -0.09111305f, 0.99543494f),
        new Quaternion(0.0132860495f, 0.042937737f, -0.03761665f, -0.998281f),
        new Quaternion(-0.024018835f, 0.04917071f, -0.0006447441f, 0.9985015f),
    };

    [MenuItem("CONTEXT/HandGhost/Apply Pinch Pose (Right Hand)")]
    private static void ApplyRight(MenuCommand command) => Apply((HandGhost)command.context, RightPinch);

    [MenuItem("CONTEXT/HandGhost/Apply Pinch Pose (Left Hand)")]
    private static void ApplyLeft(MenuCommand command) => Apply((HandGhost)command.context, LeftPinch);

    private static void Apply(HandGhost ghost, Quaternion[] rotations)
    {
        if (!ghost.TryGetComponent(out HandPuppet puppet))
        {
            Debug.LogWarning($"[HandGhostPinchPose] '{ghost.name}' has no HandPuppet.", ghost);
            return;
        }

        // Same math as HandPuppet.SetJointRotations, which can't be used here: its lazily built joint cache
        // may be stale in edit mode and throw.
        for (int i = 0; i < FingersMetadata.HAND_JOINT_IDS.Length && i < rotations.Length; i++)
        {
            HandJointId id = FingersMetadata.HAND_JOINT_IDS[i];
            HandJointMap jointMap = puppet.JointMaps.Find(map => map.id == id);
            if (jointMap == null || jointMap.transform == null)
            {
                continue;
            }

            Undo.RecordObject(jointMap.transform, "Apply Pinch Pose");
            jointMap.transform.localRotation = jointMap.RotationOffset * rotations[i];
            PrefabUtility.RecordPrefabInstancePropertyModifications(jointMap.transform);
        }
    }
}
