using Godot;
using Imperialism.Presentation;

namespace Imperialism.Client;

public sealed partial class MapCameraController : Camera2D
{
    private const float MinimumZoom = 0.15f;
    private const float MaximumZoom = 4.5f;
    private MapBounds _mapBounds;
    private bool _dragging;
    private bool _fitPending;
    private Vector2 _fitViewport;
    private float _minimumZoom = MinimumZoom;

    public bool AllowRightDrag { get; set; } = true;

    public override void _Ready()
    {
        PositionSmoothingEnabled = true;
        PositionSmoothingSpeed = 8;
    }

    public void Configure(MapBounds bounds)
    {
        _mapBounds = bounds;
        // Containers settle over several layout passes. Keep the initial fit
        // in sync with the real viewport until the player moves the camera.
        _fitPending = true;
        _fitViewport = Vector2.Zero;
    }

    public override void _Process(double delta)
    {
        var viewportSize = GetViewportRect().Size;
        if (_fitPending && viewportSize.X > 1 && viewportSize.Y > 1 && viewportSize != _fitViewport)
        {
            FitMap();
            _fitViewport = viewportSize;
        }

        var direction = Input.GetVector("ui_left", "ui_right", "ui_up", "ui_down");
        if (direction != Vector2.Zero)
        {
            _fitPending = false;
            Position += direction * (650f / Zoom.X) * (float)delta;
            ClampPosition();
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseButton)
        {
            if (mouseButton.ButtonIndex == MouseButton.Middle ||
                mouseButton.ButtonIndex == MouseButton.Right && AllowRightDrag)
            {
                _fitPending = false;
                _dragging = mouseButton.Pressed;
                GetViewport().SetInputAsHandled();
                return;
            }

            if (mouseButton.Pressed &&
                mouseButton.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown)
            {
                _fitPending = false;
                var multiplier = mouseButton.ButtonIndex == MouseButton.WheelUp ? 1.15f : 1 / 1.15f;
                var next = Mathf.Clamp(Zoom.X * multiplier, _minimumZoom, MaximumZoom);
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
            _fitPending = true;
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
            0.001f,
            MaximumZoom);
        _minimumZoom = Mathf.Min(MinimumZoom, scale);
        Zoom = new Vector2(scale, scale);
        Position = new Vector2((float)_mapBounds.Center.X, (float)_mapBounds.Center.Y);
        ResetSmoothing();
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
