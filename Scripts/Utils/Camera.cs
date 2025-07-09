using Godot;
using System;

namespace NeuroWarCommander.Scripts.Utils;

public partial class Camera : Camera2D
{
    [Export] public float Speed = 20.0f;
    [Export] public float ZoomSpeed = 20.0f;
    [Export] public float ZoomMargin = 0.1f;
    [Export] public float ZoomMin = 0.7f;
    [Export] public float ZoomMax = 2f;

    // Hardcoded map boundaries
    [Export] public Vector2 MapSize = new Vector2(2048, 2048); // Total map size
    [Export] public Vector2 MapCenter = Vector2.Zero; // Center of the map (0,0 by default)

    private float _zoomFactor = 1.0f;
    private Vector2 _zoomPos = Vector2.Zero;
    private bool _zooming = false;

    private Vector2 _dragStartMousePos = Vector2.Zero;
    private Vector2 _dragStartCameraPos = Vector2.Zero;

    private Vector2 _mousePos = Vector2.Zero;
    private Vector2 _mousePosGlobal = Vector2.Zero;

    private Vector2 _start = Vector2.Zero;
    private Vector2 _startV = Vector2.Zero;
    private Vector2 _end = Vector2.Zero;
    private Vector2 _endV = Vector2.Zero;

    private bool _isDragging = false;
    private bool _isMovingByDragging = false;

    private Control _box;

    // Map boundaries as a Rect2
    private Rect2 _mapBounds;

    public override void _Ready()
    {
        float halfWidth = MapSize.X / 2;
        float halfHeight = MapSize.Y / 2;
        _mapBounds = new Rect2(
            MapCenter.X - halfWidth,
            MapCenter.Y - halfHeight,
            MapSize.X,
            MapSize.Y
        );

        GD.Print($"Camera: Map bounds set to {_mapBounds}");
    }

    public override void _Input(InputEvent @event)
    {
        if (Math.Abs(_zoomPos.X - GetGlobalMousePosition().X) > ZoomMargin)
        {
            _zoomFactor = 1.0f;
        }

        if (Math.Abs(_zoomPos.Y - GetGlobalMousePosition().Y) > ZoomMargin)
        {
            _zoomFactor = 1.0f;
        }

        if (@event is InputEventMouseButton mouseButton)
        {
            if (mouseButton.Pressed)
            {
                _zooming = true;
                if (mouseButton.IsAction("ZoomOut"))
                {
                    _zoomFactor -= 0.01f * ZoomSpeed;
                    _zoomPos = GetGlobalMousePosition();
                }

                if (mouseButton.IsAction("ZoomIn"))
                {
                    _zoomFactor += 0.01f * ZoomSpeed;
                    _zoomPos = GetGlobalMousePosition();
                }
            }
            else
            {
                _zooming = false;
            }
        }

        if (@event is InputEventMouse mouseMoveEvent)
        {
            _mousePos = mouseMoveEvent.Position;
            _mousePosGlobal = GetGlobalMousePosition();
        }
    }

    public override void _Process(double delta)
    {
        float deltaFloat = (float)delta;

        int inputX = Convert.ToInt32(Input.IsActionPressed("ui_right")) - Convert.ToInt32(Input.IsActionPressed("ui_left"));
        int inputY = Convert.ToInt32(Input.IsActionPressed("ui_down")) - Convert.ToInt32(Input.IsActionPressed("ui_up"));

        Vector2 newPosition = new Vector2(
            Mathf.Lerp(Position.X, Position.X + inputX * Speed * Zoom.X, Speed * deltaFloat),
            Mathf.Lerp(Position.Y, Position.Y + inputY * Speed * Zoom.Y, Speed * deltaFloat)
        );

        // Apply the position but clamp to map bounds
        Position = ClampPositionToMapBounds(newPosition);

        Zoom = new Vector2(
            Mathf.Lerp(Zoom.X, Zoom.X * _zoomFactor, ZoomSpeed * deltaFloat),
            Mathf.Lerp(Zoom.Y, Zoom.Y * _zoomFactor, ZoomSpeed * deltaFloat)
        );

        Zoom = new Vector2(
            Mathf.Clamp(Zoom.X, ZoomMin, ZoomMax),
            Mathf.Clamp(Zoom.Y, ZoomMin, ZoomMax)
        );

        if (!_zooming)
        {
            _zoomFactor = 1.0f;
        }

        if (Input.IsActionJustPressed("MiddleClick"))
        {
            _isMovingByDragging = true;
            _dragStartMousePos = GetViewport().GetMousePosition();
            _dragStartCameraPos = Position;
        }

        if (_isMovingByDragging)
        {
            Vector2 currentMousePos = GetViewport().GetMousePosition();
            Vector2 dragDelta = _dragStartMousePos - currentMousePos;
            Position = ClampPositionToMapBounds(_dragStartCameraPos + dragDelta * Zoom);
        }

        if (Input.IsActionJustReleased("MiddleClick"))
        {
            _isMovingByDragging = false;
        }
    }

    private Vector2 ClampPositionToMapBounds(Vector2 position)
    {
        // Calculate visible area based on viewport size and zoom level
        Vector2 viewportSize = GetViewportRect().Size;
        Vector2 visibleAreaSize = viewportSize / Zoom;

        // Calculate the bounds taking into account the camera's visible area
        float minX = _mapBounds.Position.X + visibleAreaSize.X * 0.5f;
        float maxX = _mapBounds.End.X - visibleAreaSize.X * 0.5f;
        float minY = _mapBounds.Position.Y + visibleAreaSize.Y * 0.5f;
        float maxY = _mapBounds.End.Y - visibleAreaSize.Y * 0.5f;

        // If map is smaller than viewport, center the camera on the map
        if (minX > maxX)
        {
            float mapCenterX = _mapBounds.Position.X + _mapBounds.Size.X * 0.5f;
            position.X = mapCenterX;
        }
        else
        {
            position.X = Mathf.Clamp(position.X, minX, maxX);
        }

        if (minY > maxY)
        {
            float mapCenterY = _mapBounds.Position.Y + _mapBounds.Size.Y * 0.5f;
            position.Y = mapCenterY;
        }
        else
        {
            position.Y = Mathf.Clamp(position.Y, minY, maxY);
        }

        return position;
    }
}
