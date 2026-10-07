using UnityEngine;

/// <summary>
/// Rotates the trigger of an <see cref="ElectricDrill"/> according to the strength applied by the hand:
/// at rest when not in use, fully pressed (Max Angle) at full strength.
/// </summary>
public class ElectricDrillTrigger : MonoBehaviour
{
    [SerializeField]
    private ElectricDrill _drill;

    [SerializeField]
    [Tooltip("Trigger transform, pivoted on its hinge. Its local rotation at start is taken as the rest pose.")]
    private Transform _trigger;

    [SerializeField]
    [Tooltip("Local axis of the trigger around which it rotates when pressed.")]
    private Vector3 _rotationAxis = Vector3.right;

    [SerializeField]
    [Tooltip("Rotation, in degrees, of the fully pressed trigger. Use a negative value to rotate the other way.")]
    private float _maxAngle = 15f;

    [SerializeField]
    [Tooltip("Speed, in degrees per second, at which the trigger follows the finger when pressing.")]
    private float _pressSpeed = 360f;

    [SerializeField]
    [Tooltip("Speed, in degrees per second, at which the trigger springs back when released.")]
    private float _releaseSpeed = 180f;

    private Quaternion _restRotation;
    private float _angle;

    private void Awake()
    {
        _restRotation = _trigger.localRotation;
    }

    private void OnDisable()
    {
        _angle = 0f;
        _trigger.localRotation = _restRotation;
    }

    private void Update()
    {
        float targetAngle = _drill.IsInUse ? _maxAngle * _drill.UseStrength : 0f;
        float speed = Mathf.Abs(targetAngle) > Mathf.Abs(_angle) ? _pressSpeed : _releaseSpeed;

        _angle = Mathf.MoveTowards(_angle, targetAngle, speed * Time.deltaTime);
        _trigger.localRotation = _restRotation * Quaternion.AngleAxis(_angle, _rotationAxis.normalized);
    }
}
