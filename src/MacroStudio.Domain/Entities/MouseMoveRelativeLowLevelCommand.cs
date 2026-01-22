namespace MacroStudio.Domain.Entities;

/// <summary>
/// Represents a low-level relative mouse movement command which should be executed via injected input (e.g., SendInput).
/// This is intended for scenarios (like some games) where direct cursor positioning is ignored.
/// </summary>
public sealed class MouseMoveRelativeLowLevelCommand : Command
{
    /// <summary>
    /// The horizontal displacement (delta X) in pixels. Can be negative.
    /// </summary>
    public int DeltaX { get; set; }

    /// <summary>
    /// The vertical displacement (delta Y) in pixels. Can be negative.
    /// </summary>
    public int DeltaY { get; set; }

    /// <inheritdoc />
    public override string DisplayName => "Mouse Move (Relative, Low Level)";

    /// <inheritdoc />
    public override string Description => $"Move relative (low-level) ({DeltaX}, {DeltaY})";

    public MouseMoveRelativeLowLevelCommand(int deltaX, int deltaY) : base()
    {
        DeltaX = deltaX;
        DeltaY = deltaY;
    }

    public MouseMoveRelativeLowLevelCommand(Guid id, TimeSpan delay, DateTime createdAt, int deltaX, int deltaY)
        : base(id, delay, createdAt)
    {
        DeltaX = deltaX;
        DeltaY = deltaY;
    }

    public override bool IsValid()
    {
        // Allow reasonable range for relative movement (e.g., -32768 to 32767 pixels)
        const int maxDelta = 32767;
        const int minDelta = -32768;
        return DeltaX >= minDelta && DeltaX <= maxDelta && DeltaY >= minDelta && DeltaY <= maxDelta;
    }

    public override Command Clone()
        => new MouseMoveRelativeLowLevelCommand(Guid.NewGuid(), Delay, DateTime.UtcNow, DeltaX, DeltaY);
}
