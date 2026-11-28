using Godot;

namespace Imperialism.Client;

/// <summary>The original's code-drawn frame around an unscaled panel bitmap.</summary>
public sealed partial class IndustryDialog : Control
{
    public event Action? CloseRequested;

    public IndustryDialog() => MouseFilter = MouseFilterEnum.Stop;

    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), new Color("4d4d4d"));
        DrawRect(new Rect2(0, 0, Size.X - 1, Size.Y - 1), new Color("e9ecf3"), false);
        DrawRect(new Rect2(2, 2, Size.X - 5, 23), new Color("bfcedc"));
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mouse &&
            (mouse.Position.Y < 26 || mouse.Position.X < 3 || mouse.Position.X >= Size.X - 3 || mouse.Position.Y >= Size.Y - 3))
        {
            AcceptEvent();
            CloseRequested?.Invoke();
        }
    }
}
