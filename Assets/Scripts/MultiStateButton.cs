using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Cycles a <see cref="Button"/> through a list of states: each press invokes the callback of the
/// current state, then advances to the next one and shows its icon.
/// </summary>
public class MultiStateButton : MonoBehaviour
{
    [Serializable]
    public class State
    {
        [Tooltip("Icon shown on the button while in this state.")]
        public Sprite Icon;

        [Tooltip("Text shown on the label while in this state.")]
        public string Label;

        [Tooltip("Invoked when the button is pressed while in this state.")]
        public UnityEvent OnPressed;
    }

    [SerializeField]
    private Button _button;

    [SerializeField]
    [Tooltip("Image that displays the icon of the current state.")]
    private Image _icon;

    [SerializeField]
    [Tooltip("Optional text that displays the label of the current state.")]
    private TMP_Text _label;

    [SerializeField]
    private State[] _states;

    [SerializeField]
    [Tooltip("Index of the state the button starts in.")]
    private int _initialState;

    [Tooltip("Invoked with the new state index every time the state changes.")]
    public UnityEvent<int> OnStateChanged;

    private int _currentState;

    public int CurrentState => _currentState;

    private void Awake()
    {
        SetState(_initialState, false);
    }

    private void OnEnable()
    {
        _button.onClick.AddListener(Press);
    }

    private void OnDisable()
    {
        _button.onClick.RemoveListener(Press);
    }

    /// <summary>
    /// Invokes the callback of the current state and advances to the next one.
    /// </summary>
    public void Press()
    {
        if (_states.Length == 0)
        {
            return;
        }

        int pressedState = _currentState;
        SetState((_currentState + 1) % _states.Length);
        _states[pressedState].OnPressed.Invoke();
    }

    /// <summary>
    /// Jumps to the given state without invoking its callback.
    /// </summary>
    public void SetState(int index) => SetState(index, true);

    private void SetState(int index, bool notify)
    {
        if (_states.Length == 0)
        {
            return;
        }

        _currentState = Mathf.Clamp(index, 0, _states.Length - 1);

        if (_icon != null)
        {
            _icon.sprite = _states[_currentState].Icon;
        }

        if (_label != null)
        {
            _label.text = _states[_currentState].Label;
        }

        if (notify)
        {
            OnStateChanged.Invoke(_currentState);
        }
    }

    private void Reset()
    {
        _button = GetComponent<Button>();
        _icon = _button != null ? _button.image : null;
        _label = GetComponentInChildren<TMP_Text>();
    }
}
