using MacroStudio.Domain.ValueObjects;

namespace MacroStudio.Domain.Entities;

/// <summary>
/// Represents a low-level mouse movement command which should be executed via injected input (e.g., SendInput).
/// This is intended for scenarios (like some games) where direct cursor positioning (SetCursorPos) is ignored.
/// </summary>
public sealed class MouseMoveLowLevelCommand : Command
{
    /// <summary>
    /// The target position to move the mouse cursor to.
    /// </summary>
    public Point Position { get; set; }

    /// <inheritdoc />
    public override string DisplayName => "Mouse Move (Low Level)";

    /// <inheritdoc />
    public override string Description => $"Move (low-level) to {Position}";

    public MouseMoveLowLevelCommand(Point position) : base()
    {
        Position = position;
    }

    public MouseMoveLowLevelCommand(Guid id, TimeSpan delay, DateTime createdAt, Point position)
        : base(id, delay, createdAt)
    {
        Position = position;
    }

    public override bool IsValid() => Position.X >= 0 && Position.Y >= 0;

    public override Command Clone()
        => new MouseMoveLowLevelCommand(Guid.NewGuid(), Delay, DateTime.UtcNow, Position);
}

