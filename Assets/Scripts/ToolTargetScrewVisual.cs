using Techstar.Procedure;
using UnityEngine;

/// <summary>
/// Screws a transform in (or out) following a <see cref="ToolTarget"/>'s progress:
/// at progress 0 the bolt is in its start pose, at progress 1 it has turned and travelled fully.
/// </summary>
/// <remarks>
/// Move a child mesh, not the ToolTarget's own transform, so its collider and alignment reference stay put.
/// </remarks>
public class ToolTargetScrewVisual : MonoBehaviour
{
    [SerializeField]
    private ToolTarget _target;

    [SerializeField]
    [Tooltip("The bolt mesh to move. Defaults to this transform.")]
    private Transform _bolt;

    [SerializeField]
    [Tooltip("Local axis the bolt turns around and travels along.")]
    private Vector3 _localAxis = Vector3.forward;

    [SerializeField]
    [Tooltip("Total turns from progress 0 to 1 (negative = unscrew).")]
    private float _turns = 5f;

    [SerializeField]
    [Tooltip("Distance travelled along the axis from progress 0 to 1, in meters (negative = outwards).")]
    private float _travel = 0.01f;

    private Vector3 _startPosition;
    private Quaternion _startRotation;

    protected virtual void Awake()
    {
        if (_bolt == null)
        {
            _bolt = transform;
        }
        _startPosition = _bolt.localPosition;
        _startRotation = _bolt.localRotation;
    }

    protected virtual void OnEnable()
    {
        _target.WhenProgressChanged.AddListener(Apply);
        Apply(_target.Progress);
    }

    protected virtual void OnDisable()
    {
        _target.WhenProgressChanged.RemoveListener(Apply);
    }

    private void Apply(float progress)
    {
        Vector3 axis = _localAxis.normalized;
        _bolt.localRotation = _startRotation * Quaternion.AngleAxis(_turns * 360f * progress, axis);
        _bolt.localPosition = _startPosition + _startRotation * axis * (_travel * progress);
    }
}
