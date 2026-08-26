using UnityEngine;

/// <summary>
/// Immutable snapshot of the input intention of one player for one frame.
/// It is device-agnostic on purpose: the gameplay layer must not know whether the state
/// came from a remote touch device, a keyboard, or a replay.
/// </summary>
public struct ShootingState
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
