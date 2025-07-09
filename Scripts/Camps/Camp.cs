using Godot;
using System;

namespace NeuroWarCommander.Scripts.Camps;

public partial class Camp : CampBase
{
    public override void _Ready()
    {
        MaxHealth = 50f;
        CurrentHealth = 50f;
        HealingRadius = 80f;
        HealingAmount = 0.5f;
        RegenerationRate = 0.75f;

        base._Ready();
    }

    public override CampType GetCampType()
    {
        return CampType.Camp;
    }
}