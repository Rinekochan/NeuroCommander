using Godot;
using System;

namespace NeuroWarCommander.Scripts.Camps;

public partial class Town : CampBase
{
    public override void _Ready()
    {
        MaxHealth = 100f;
        CurrentHealth = 100f;
        HealingRadius = 120f;
        HealingAmount = 1f;
        RegenerationRate = 1.25f;

        base._Ready();
    }

    public override CampType GetCampType()
    {
        return CampType.Town;
    }
}