using Godot;

namespace Imperialism.Client;

/// <summary>Lets mouse input pass through the transparent pixels of city tiles.</summary>
internal sealed partial class IndustryBuildingHit : Control
{
    internal Func<Vector2, bool> ContainsPixel { get; init; } = _ => false;
    internal event Action? MouseMoved;
    public override bool _HasPoint(Vector2 point) => ContainsPixel(point);
    public override void _GuiInput(InputEvent input)
    {
        if (input is InputEventMouseMotion) MouseMoved?.Invoke();
    }
}
