using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class RemoteControlRig : MonoBehaviour
{

    #region Variables

    /// <summary>
    /// Canvas holding the control buttons.
    /// </summary>
    [SerializeField] private Canvas controlCanvas;

    /// <summary>
    /// Raycaster of the controlCanvas. Disabled on Awake and driven manually.
    /// </summary>
    [SerializeField] private GraphicRaycaster raycaster;

    /// <summary>
    /// Camera that renders this control scene and whose frame is streamed to the peer.
    /// It also defines the pixel space the normalized touches are mapped into.
    /// </summary>
    [SerializeField] private Camera controlCamera;

    /// <summary> 
    /// Labels showing which player this control scene belongs to.
    /// </summary>
    [SerializeField] private TextMeshProUGUI[] playerLabels;

    /// <summary>
    /// Peer whose input drives this rig. Null until Bind is called.
    /// </summary>
    private string clientID;

    /// <summary>
    /// State published to consumers, recomputed once per frame in Update.
    /// </summary>
    private ShootingState state;

    /// <summary>
    /// Reused raycast inputs and outputs, so hit-testing does not allocate every frame.
    /// </summary>
    private PointerEventData pointerData;
    private readonly List<RaycastResult> raycastResults = new List<RaycastResult>();

    /// <summary>
    /// Buttons hit during the current frame.
    /// </summary>
    private readonly HashSet<RemoteControlButton> currentHits = new HashSet<RemoteControlButton>();

    /// <summary>
    /// Buttons that were hit during the previous frame. Used to derive press/release edges.
    /// </summary>
    private readonly HashSet<RemoteControlButton> previousHits = new HashSet<RemoteControlButton>();

    /// <summary>
    /// Buttons whose press started this frame (currentHits minus previousHits).
    /// </summary>
    private readonly HashSet<RemoteControlButton> justPressed = new HashSet<RemoteControlButton>();

    #endregion

    #region Methods

    /// <summary>
    /// Rewrites the "PX" placeholder text of this control scene with the number of the player it
    /// belongs to.
    /// </summary>
    /// <param name="number">Player number.</param>
    public void SetPlayerNumber(int number)
    {
        if (playerLabels == null || playerLabels.Length == 0)
        {
            UnityEngine.Debug.LogWarning("[RemoteControlRig] playerLabels are not assigned.");
            return;
        }

        foreach (TextMeshProUGUI label in playerLabels)
        {
            if (label == null) continue;
            label.text = "P" + number;
        }
    }

    /// <summary>
    /// Binds this control scene to the peer whose input will drive it. Passing null or an empty
    /// id unbinds it, releasing every button and clearing the published state.
    /// </summary>
    /// <param name="id">Id of the client that controls this rig, or null to unbind it.</param>
    public void Bind(string id)
    {
        clientID = id;

        if (string.IsNullOrEmpty(id))
        {
            // Releasing every button prevents a latched action from surviving the unbind
            // (for example, a player eliminated while holding "forward").
            ReleaseAll();
            state = default;
        }
    }

    /// <summary>
    /// Hit-tests one normalized touch through this canvas's own raycaster and records every
    /// RemoteControlButton it reaches.
    /// </summary>
    private void ResolveTouch(TouchData normalized)
    {
        pointerData.Reset();
        pointerData.position = new Vector2(normalized.x * controlCamera.pixelWidth,
                                           normalized.y * controlCamera.pixelHeight);

        raycastResults.Clear();

        // Instance call: this raycaster only ever queries the graphics registered to its own
        // canvas, so copies of the control scene cannot see each other's buttons.
        raycaster.Raycast(pointerData, raycastResults);

        // Results arrive sorted front-to-back.
        for (int i = 0; i < raycastResults.Count; i++)
        {
            // The graphic hit may be a child of the button (its label, its icon).
            RemoteControlButton button = raycastResults[i].gameObject.GetComponentInParent<RemoteControlButton>();
            if (button == null || !button.isActiveAndEnabled) continue;

            currentHits.Add(button);
        }
    }

    /// <summary>
    /// Aggregates the pressed buttons into the state published this frame.
    /// </summary>
    private ShootingState BuildState()
    {
        ShootingState result = default;

        foreach (RemoteControlButton button in currentHits)
        {
            bool isEdge = justPressed.Contains(button);

            // Non-continuous buttons only contribute on the frame the press starts.
            if (!button.Continuous && !isEdge) continue;

            switch (button.Action)
            {
                case RemoteControlAction.MoveForward: result.move.y += 1f; break;
                case RemoteControlAction.MoveBackward: result.move.y -= 1f; break;
                case RemoteControlAction.MoveRight: result.move.x += 1f; break;
                case RemoteControlAction.MoveLeft: result.move.x -= 1f; break;
                case RemoteControlAction.RotateRight: result.rotation += 1f; break;
                case RemoteControlAction.RotateLeft: result.rotation -= 1f; break;
                case RemoteControlAction.Shoot:
                    result.shootHeld = true;
                    break;
            }
        }

        result.move = Vector2.ClampMagnitude(result.move, 1f);
        result.rotation = Mathf.Clamp(result.rotation, -1f, 1f);

        return result;
    }

    /// <summary>
    /// Propagates press/release transitions to the buttons so the streamed frame shows feedback.
    /// </summary>
    private void ApplyButtonFeedback()
    {
        foreach (RemoteControlButton button in justPressed)
            button.SetPressed(true);

        foreach (RemoteControlButton button in previousHits)
            if (!currentHits.Contains(button)) button.SetPressed(false);
    }

    /// <summary>
    /// Forces every tracked button back to its idle state.
    /// </summary>
    private void ReleaseAll()
    {
        foreach (RemoteControlButton button in currentHits)
            if (button != null) button.SetPressed(false);

        foreach (RemoteControlButton button in previousHits)
            if (button != null) button.SetPressed(false);

        currentHits.Clear();
        previousHits.Clear();
        justPressed.Clear();
    }

    #endregion

    #region Getters

    /// <summary>
    /// Returns the input state of the shooting game
    /// </summary>
    /// <returns></returns>
    public ShootingState GetShootingState()
    {
        return state;
    }
    
    /// <summary>
    /// Returns the camera of the controler scene
    /// </summary>
    /// <returns></returns>
    public Camera GetControlCamera()
    {
        return controlCamera;
    }

    #endregion

    #region MonoBehaviour

    private void Awake()
    {
        // EventSystem.current may be null: this rig never routes events through it, and
        // PointerEventData only dereferences the system when its selection API is used.
        pointerData = new PointerEventData(EventSystem.current);

        // This removes the raycaster from the global RaycasterManager while keeping
        // Raycast() callable.
        if (raycaster != null) raycaster.enabled = false;

        clientID = null;
    }

    private void Update()
    {
        currentHits.Clear();
        justPressed.Clear();

        if (!string.IsNullOrEmpty(clientID) && controlCamera != null && raycaster != null && HostInputManager.Instance != null)
        {
            // CurrentTouches returns an empty list when the peer stopped sending, so a lost
            // connection degrades to "no input" instead of latching the last known state.
            InputFrame frame = HostInputManager.Instance.GetInputFrame(clientID);
            if (frame != null)
            {
                for (int i = 0; i < frame.touches.Count; i++)
                    ResolveTouch(frame.touches[i]);
            }
        }

        // Edges must be computed before previousHits is overwritten.
        foreach (RemoteControlButton button in currentHits)
            if (!previousHits.Contains(button)) justPressed.Add(button);

        ApplyButtonFeedback();

        state = BuildState();

        previousHits.Clear();
        foreach (RemoteControlButton button in currentHits)
            previousHits.Add(button);
    }

    private void OnDestroy()
    {
        ReleaseAll();
    }

    #endregion

}
