/// <summary>
/// Asbtract representation declared by a button of a remote control scene.
/// The button does not know which player it drives: it only declares what it means.
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
