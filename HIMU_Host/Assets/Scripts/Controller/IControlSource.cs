using UnityEngine;

/// <summary>
/// Immutable snapshot of the input intention of one player for one frame.
/// It is device-agnostic on purpose: the gameplay layer must not know whether the state
/// came from a remote touch device, a keyboard, or a replay.
/// </summary>
public struct ControlState
{
    /// <summary>Planar movement. X = strafe (right positive), Y = forward (forward positive).</summary>
    public Vector2 move;

    /// <summary>Yaw intention in [-1, 1]. Positive rotates clockwise.</summary>
    public float rotation;

    /// <summary>True while the fire action is held.</summary>
    public bool shootHeld;

    /// <summary>True only on the frame the fire action started.</summary>
    public bool shootPressed;
}

/// <summary>
/// Polling interface implemented by anything able to act as a virtual input device for a
/// player. It mirrors the polling model of Unity's Input System (read the current state
/// when you need it) instead of an event model, because the consumer
/// (<see cref="ShooterPlayerController"/>) drives a Rigidbody and therefore needs a
/// well-defined state at a well-defined point of the frame, not callbacks arriving from
/// the network at arbitrary moments.
/// </summary>
public interface IControlSource
{
    /// <summary>
    /// True when the source is bound to a live peer. While false, consumers must treat the
    /// source as absent (and may fall back to a local device).
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Returns the state computed for the current frame.
    /// </summary>
    ControlState GetState();
}
