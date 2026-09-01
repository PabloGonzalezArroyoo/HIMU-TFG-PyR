using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class RemoteControlButton : MonoBehaviour
{

    #region Variables

    /// <summary>
    /// Gameplay intention produced while this button is held.
    /// </summary>
    [Header("Behaviour")]
    [SerializeField] private RemoteControlAction action = RemoteControlAction.None;

    /// <summary>
    /// When true the action is reported for every frame the button stays pressed (movement).
    /// When false the action is reported only on the frame the press starts (shooting, menus).
    /// </summary>
    [SerializeField] private bool continuous = true;

    /// <summary>
    /// Graphic tinted while the button is held. Resolved from this GameObject when left empty.
    /// Purely local visual feedback: it is what the player will see in the streamed frame.
    /// </summary>
    [Header("Feedback")]
    [SerializeField] private Graphic targetGraphic;

    /// <summary>
    /// Multiplicative tint applied to <see cref="targetGraphic"/> while held.
    /// </summary>
    [SerializeField] private Color pressedTint = new Color(0.65f, 0.65f, 0.65f, 1f);

    /// <summary>
    /// Colour of <see cref="targetGraphic"/> when the button is idle.
    /// </summary>
    private Color idleColor;

    /// <summary>
    /// Optional hooks for behaviour that is local to the control scene (sounds, animations).
    /// They must NOT be used to reach the player: the player lives in another scene and is
    /// resolved at runtime, so it cannot be wired here at edit time.
    /// </summary>
    [Header("Local hooks (optional)")]
    public UnityEvent onPressed;
    public UnityEvent onReleased;

    /// <summary>Gameplay intention declared by this button.</summary>
    public RemoteControlAction Action => action;

    /// <summary>Whether the action is held (true) or edge-triggered (false).</summary>
    public bool Continuous => continuous;

    /// <summary>Whether the button is currently held by at least one touch.</summary>
    public bool IsPressed { get; private set; }

    #endregion

    #region Methods

    /// <summary>
    /// Updates the pressed state of the button. Called exclusively by the
    /// <see cref="RemoteControlRig"/> of this scene, and only on transitions.
    /// </summary>
    /// <param name="pressed">New pressed state.</param>
    public void SetPressed(bool pressed)
    {
        if (IsPressed == pressed) return;

        IsPressed = pressed;

        if (targetGraphic != null)
            targetGraphic.color = pressed ? idleColor * pressedTint : idleColor;

        if (pressed) onPressed?.Invoke();
        else onReleased?.Invoke();
    }

    #endregion

    #region MonoBehaviour

    private void Awake()
    {
        if (targetGraphic == null) targetGraphic = GetComponent<Graphic>();
        if (targetGraphic != null) idleColor = targetGraphic.color;

        // The rig needs at least one raycastable graphic to be able to reach this button;
        // a RectTransform on its own is invisible to any GraphicRaycaster.
        if (GetComponentInChildren<Graphic>(true) == null)
            Debug.LogError($"[RemoteControlButton] '{name}' has no Graphic, so it can never be hit.");

        // A Button component would re-introduce the EventSystem dependency this class exists to
        // avoid: Selectable.OnPointerDown writes into EventSystem.current, whose single selection
        // slot would be contended by every loaded copy of the control scene. Disabled rather than
        // forbidden so that existing scenes keep working after the migration.
        Button legacyButton = GetComponent<Button>();
        if (legacyButton != null) legacyButton.enabled = false;
    }

    private void OnDisable()
    {
        // Fail safe: a button disabled while held must not leave the action latched.
        SetPressed(false);
    }

    #endregion

}
