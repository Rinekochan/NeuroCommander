using Godot;
using System;
using System.Collections.Generic;
using System.Drawing;
using NeuroWarCommander.Scripts.Units.Base;
using Color = Godot.Color;

namespace NeuroWarCommander.Scripts.Camps;

public partial class CampBase : StaticBody2D
{
    public enum CampOwner { Neutral, TeamA, TeamB }
    public enum CampType { Base, Camp, Town, City, Castle }

    [Export] public float MaxHealth { get; set; } = 50f;
    [Export] public float CurrentHealth { get; set; } = 50f;

    [Export] public CampOwner TeamId { get; set; } = CampOwner.Neutral;

    [Export] public float HealingRadius { get; set; } = 50f;
    [Export] public float HealingAmount { get; set; } = 1f;
    [Export] public float HealingInterval { get; set; } = 1.5f;
    [Export] public float RegenerationRate { get; set; } = 0.5f;

    private float _healTimer; // = 0f
    private float _regenTimer; // = 0f

    [Signal] public delegate void CampCapturedEventHandler(CampBase camp, CampOwner newOwner);
    [Signal] public delegate void CampReducedToZeroHPEventHandler(CampBase camp);
    [Signal] public delegate void UnitTrainedEventHandler(CampBase camp, string unitType);

    private partial class HealingZone : Node2D
    {
        public float Radius { get; set; }
        public Color ZoneColor { get; set; } = new(0, 0, 1, 0.3f);
        private float BorderWidth { get; set; } = 3f;

        public override void _Draw()
        {
            // Draw the semi-transparent filled circle
            DrawCircle(Vector2.Zero, Radius, ZoneColor);

            // Draw the circle color
            Color borderColor = new Color(ZoneColor.R, ZoneColor.G, ZoneColor.B, 0.4f);

            DrawArc(Vector2.Zero, Radius, 0, Mathf.Tau, 64, borderColor, BorderWidth);
        }
    }

    private partial class HealthBar : Node2D
    {
        private readonly CampBase _parent;

        // Visual settings
        private readonly float _width = 60f;
        private readonly float _height = 6f;
        private readonly Color _bgColor = new(0.2f, 0.2f, 0.2f, 0.8f);
        private readonly Color _healthColor = new(0.2f, 0.8f, 0.2f);
        private readonly Color _borderColor = new(0.1f, 0.1f, 0.1f);
        private readonly float _borderWidth = 1.0f;
        private readonly float _yOffset = -15f; // Offset above the collision shape

        // Animation values
        private float _displayedHealthRatio = 1.0f;
        private Tween _tween;
        private Vector2 _collisionSize;

        public HealthBar(CampBase parent)
        {
            _parent = parent;
            _displayedHealthRatio = _parent.CurrentHealth / _parent.MaxHealth;
        }

        public HealthBar() { }

        public override void _Ready()
        {
            // Get collision shape size
            var collisionShape = _parent.GetNode<CollisionShape2D>("CollisionShapeBase");
            if (collisionShape != null)
            {
                if (collisionShape.Shape is RectangleShape2D rectShape)
                {
                    _collisionSize = rectShape.Size;
                }
                else if (collisionShape.Shape is CircleShape2D circleShape)
                {
                    _collisionSize = new Vector2(circleShape.Radius * 2, circleShape.Radius * 2);
                }
            }
        }


        public override void _Draw()
        {
            float yPos = -(_collisionSize.Y / 2) + _yOffset;
            Vector2 position = new(-_width / 2, yPos);

            // Draw background
            DrawRect(new Rect2(position, new Vector2(_width, _height)), _bgColor);

            // Draw health fill using the animated value
            DrawRect(new Rect2(position, new Vector2(_width * _displayedHealthRatio, _height)), _healthColor);

            // Draw border
            DrawRect(new Rect2(position, new Vector2(_width, _height)), _borderColor, false, _borderWidth);
        }

        public void UpdateHealth()
        {
            float targetRatio = _parent.CurrentHealth / _parent.MaxHealth;

            // Kill any existing tween
            if (_tween != null && _tween.IsValid())
            {
                _tween.Kill();
            }

            // Create new tween
            _tween = CreateTween();
            _tween.TweenProperty(this, "_displayedHealthRatio", targetRatio, 0.5f)
                  .SetTrans(Tween.TransitionType.Elastic)
                  .SetEase(Tween.EaseType.Out);

            // Ensure we update the visual during the tween
            _tween.Finished += QueueRedraw;

            ProcessMode = ProcessModeEnum.Always;
        }

        public override void _Process(double delta)
        {
            if (_tween != null && _tween.IsValid() && _tween.IsRunning())
            {
                QueueRedraw();
            }
        }
    }

    private partial class InfoLabel : Node2D
    {
        private readonly Label _label;
        private readonly CampBase _parent;
        private readonly float _yOffset = 10f; // Offset below the health bar

        public InfoLabel(CampBase parent)
        {
            _parent = parent;

            _label = new Label();
            _label.HorizontalAlignment = HorizontalAlignment.Center;
            _label.Size = new Vector2(100, 20);
            _label.AddThemeFontOverride("font", ResourceLoader.Load<Font>("res://Assets/Fonts/rimouski sb.otf"));
            _label.AddThemeColorOverride("font_color", Colors.White);
            _label.AddThemeConstantOverride("outline_size", 1);
            _label.AddThemeColorOverride("font_outline_color", Colors.Black);

            AddChild(_label);
        }

        public InfoLabel() { }

        public override void _Ready()
        {
            // Get collision shape size
            var collisionShape = _parent.GetNode<CollisionShape2D>("CollisionShapeBase");
            if (collisionShape != null)
            {
                if (collisionShape.Shape is RectangleShape2D rectShape)
                {
                    Vector2 size = rectShape.Size;
                    _label.Position = new Vector2(-50, size.Y / 2 + _yOffset);
                }
                else if (collisionShape.Shape is CircleShape2D circleShape)
                {
                    float radius = circleShape.Radius;
                    _label.Position = new Vector2(-50, radius + _yOffset);
                }
            }
            else
            {
                _label.Position = new Vector2(-50, _yOffset);
            }
        }

        public void UpdateText(CampType campType)
        {
            _label.Text = $"{campType}";
        }
    }

    private HealingZone _healingZone;
    private HealthBar _healthBar;
    private InfoLabel _infoLabel;

    public override void _Ready()
    {
        _healingZone = new HealingZone();
        AddChild(_healingZone);

        _healthBar = new HealthBar(this);
        AddChild(_healthBar);

        _infoLabel = new InfoLabel(this);
        AddChild(_infoLabel);
        _infoLabel.UpdateText(GetCampType());

        UpdateHealingZone();
    }

    public override void _Process(double delta)
    {
        float deltaFloat = (float)delta;

        // Handle camp HP regeneration
        if (CurrentHealth < MaxHealth)
        {
            _regenTimer += deltaFloat;
            if (_regenTimer >= 5.0f) // Regenerate every 5.0 seconds
            {
                _regenTimer = 0f;
                RegenerateCamp();
            }
        }

        // Handle healing nearby units
        if (TeamId != CampOwner.Neutral)
        {
            _healTimer += deltaFloat;
            if (_healTimer >= HealingInterval)
            {
                _healTimer = 0f;
                HealNearbyUnits();
            }
        }
    }

    private void RegenerateCamp()
    {
        float previousHealth = CurrentHealth;
        CurrentHealth = Mathf.Min(CurrentHealth + RegenerationRate, MaxHealth);

        // Only update the health bar if health actually changed
        if (previousHealth != CurrentHealth)
        {
            _healthBar.UpdateHealth();
        }
    }

    private void HealNearbyUnits()
    {
        if (TeamId == CampOwner.Neutral || CurrentHealth <= 0) return;

        // Create a physics space state for querying
        var spaceState = GetWorld2D().DirectSpaceState;

        // Create a circle query shape
        var query = new PhysicsShapeQueryParameters2D();
        var circleShape = new CircleShape2D();
        circleShape.Radius = HealingRadius;
        query.Shape = circleShape;
        query.Transform = GlobalTransform;
        query.CollideWithAreas = true;
        query.CollideWithBodies = true;
        query.CollisionMask = 1 << 1; // Only checks for units

        // Perform the query
        var results = spaceState.IntersectShape(query);

        // Process results
        foreach (var result in results)
        {
            var collider = result["collider"].As<Node2D>();
            if (collider == null) continue;

            // Check if it's a unit with matching team ID
            if (collider is UnitBase unit)
            {
                // For units, check if teamId matches
                if (unit.TeamId != (int)TeamId) continue;

                // Heal the unit
                if (unit.Heal(HealingAmount, this))
                {
                    // GD.Print($"Healing unit {unit.Name} for {HealingAmount}");
                }
            }
        }
    }

    private void UpdateHealingZone()
    {
        _healingZone.Radius = HealingRadius;

        if (TeamId == CampOwner.TeamA)
        {
            _healingZone.ZoneColor = new Color(0, 0, 1, 0.2f); // Blue with transparency
            _healingZone.Visible = true;
        }
        else if (TeamId == CampOwner.TeamB)
        {
            _healingZone.ZoneColor = new Color(1, 0, 0, 0.2f); // Red with transparency
            _healingZone.Visible = true;
        }
        else
        {
            _healingZone.Visible = false; // Hide for neutral camps
        }

        _healingZone.QueueRedraw();
    }

    public void TakeDamage(float amount)
    {
        float previousHP = CurrentHealth;
        CurrentHealth = Mathf.Max(0, CurrentHealth - amount);

        // Update the health bar with animation
        _healthBar.UpdateHealth();
        _regenTimer = 0f; // Reset regeneration timer on damage

        if (CurrentHealth <= 0 && previousHP > 0)
        {
            UpdateHealingZone();
            EmitSignal(SignalName.CampReducedToZeroHP, this);
            GD.Print("Camp has been destroyed: " + Name);
        }
    }

    public bool TryCapture(CampOwner newOwner)
    {
        if (CurrentHealth <= 0 && TeamId != newOwner)
        {
            TeamId = newOwner;
            CurrentHealth = 10f; // Start with low HP when captured

            UpdateHealingZone();
            EmitSignal(SignalName.CampCaptured, this, (int)newOwner);
            return true;
        }
        return false;
    }

    public virtual CampType GetCampType()
    {
        return CampType.Base;
    }
}
