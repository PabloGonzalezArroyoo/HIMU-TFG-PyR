/// <summary>
/// Abstract intention declared by a button of a remote control scene.
/// The button does not know which player it drives: it only declares what it means.
/// The <see cref="RemoteControlRig"/> of its scene translates the set of pressed
/// actions into a <see cref="ControlState"/>, and the gameplay layer consumes that state.
/// This indirection is what allows a button (authored at edit time, inside the control
/// scene) to drive a player object that lives in a different scene and is only resolved
/// at runtime.
/// </summary>
public enum RemoteControlAction
{
    /// <summary>The button has no gameplay meaning (decorative / not yet configured).</summary>
    None,

    /// <summary>Move the player along its own forward axis.</summary>
    MoveForward,

    /// <summary>Move the player against its own forward axis.</summary>
    MoveBackward,

    /// <summary>Move the player against its own right axis.</summary>
    MoveLeft,

    /// <summary>Move the player along its own right axis.</summary>
    MoveRight,

    /// <summary>Rotate the player counter-clockwise around the Y axis.</summary>
    RotateLeft,

    /// <summary>Rotate the player clockwise around the Y axis.</summary>
    RotateRight,

    /// <summary>Fire the player's weapon.</summary>
    Shoot
}
