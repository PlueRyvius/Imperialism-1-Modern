using Godot;
using Imperialism.Presentation;

namespace Imperialism.Client;

public sealed partial class MapCameraController : Camera2D
{
    private const float MinimumZoom = 0.15f;
    private const float MaximumZoom = 4.5f;
    private MapBounds _mapBounds;
    private bool _dragging;

    public override void _Ready()
    {
        PositionSmoothingEnabled = true;
        PositionSmoothingSpeed = 8;
    }

    public void Configure(MapBounds bounds)
    {
        _mapBounds = bounds;
        CallDeferred(MethodName.FitMap);
    }

    public override void _Process(double delta)
    {
        var direction = Input.GetVector("ui_left", "ui_right", "ui_up", "ui_down");
        if (direction != Vector2.Zero)
        {
            Position += direction * (650f / Zoom.X) * (float)delta;
            ClampPosition();
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseButton)
        {
            if (mouseButton.ButtonIndex is MouseButton.Middle or MouseButton.Right)
            {
                _dragging = mouseButton.Pressed;
                GetViewport().SetInputAsHandled();
                return;
            }

            if (mouseButton.Pressed &&
                mouseButton.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown)
            {
                var multiplier = mouseButton.ButtonIndex == MouseButton.WheelUp ? 1.15f : 1 / 1.15f;
                var next = Mathf.Clamp(Zoom.X * multiplier, MinimumZoom, MaximumZoom);
                Zoom = new Vector2(next, next);
                ClampPosition();
                GetViewport().SetInputAsHandled();
            }
        }
        else if (@event is InputEventMouseMotion motion && _dragging)
        {
            Position -= motion.Relative / Zoom.X;
            ClampPosition();
            GetViewport().SetInputAsHandled();
        }
        else if (@event is InputEventKey key && key.Pressed && !key.Echo && key.Keycode == Key.Home)
        {
            FitMap();
            GetViewport().SetInputAsHandled();
        }
    }

    private void FitMap()
    {
        if (_mapBounds.Width <= 0 || _mapBounds.Height <= 0)
        {
            return;
        }

        var viewport = GetViewportRect().Size;
        var scale = Mathf.Clamp(
            0.88f * Mathf.Min(viewport.X / (float)_mapBounds.Width, viewport.Y / (float)_mapBounds.Height),
            MinimumZoom,
            MaximumZoom);
        Zoom = new Vector2(scale, scale);
        Position = new Vector2((float)_mapBounds.Center.X, (float)_mapBounds.Center.Y);
    }

    private void ClampPosition()
    {
        var halfViewport = GetViewportRect().Size / (2 * Zoom.X);
        var minimumX = (float)_mapBounds.Left + halfViewport.X;
        var maximumX = (float)_mapBounds.Right - halfViewport.X;
        var minimumY = (float)_mapBounds.Top + halfViewport.Y;
        var maximumY = (float)_mapBounds.Bottom - halfViewport.Y;
        Position = new Vector2(
            minimumX <= maximumX
                ? Mathf.Clamp(Position.X, minimumX, maximumX)
                : (float)_mapBounds.Center.X,
            minimumY <= maximumY
                ? Mathf.Clamp(Position.Y, minimumY, maximumY)
                : (float)_mapBounds.Center.Y);
    }
}
