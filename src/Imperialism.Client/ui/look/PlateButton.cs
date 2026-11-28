using Godot;

namespace Imperialism.Client;

/// <summary>Bitmap button with rectangular picking and the look guide's hold timing.</summary>
public sealed partial class PlateButton : TextureRect
{
    public event Action? Pressed;
    public Texture2D? Resting { get; set; }
    public Texture2D? Down { get; set; }
    public bool Repeat { get; set; }
    private bool _held;
    private double _delay;

    public PlateButton()
    {
        MouseFilter = MouseFilterEnum.Stop;
        FocusMode = FocusModeEnum.All;
        MouseExited += CancelHold;
        FocusExited += CancelHold;
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left } mouse)
        {
            if (mouse.Pressed)
            {
                GrabFocus();
                _held = true;
                _delay = 0.4;
                Texture = Down ?? Resting;
                AcceptEvent();
                Pressed?.Invoke();
                return;
            }
            CancelHold();
            AcceptEvent();
        }
        if (@event is InputEventKey { Pressed: true, Echo: false } key && key.Keycode is Key.Enter or Key.Space)
        {
            AcceptEvent();
            Pressed?.Invoke();
        }
    }

    public override void _Process(double delta)
    {
        if (!IsVisibleInTree() || !Input.IsMouseButtonPressed(MouseButton.Left)) { CancelHold(); return; }
        if (!_held || !Repeat) return;
        _delay -= delta;
        if (_delay > 0) return;
        _delay += 0.06;
        Pressed?.Invoke();
    }

    public void CancelHold()
    {
        _held = false;
        if (Resting is not null) Texture = Resting;
    }
}
