using Godot;
using System;

namespace NeuroWarCommander.Scripts.Camps;

public partial class Castle : CampBase
{
    public override void _Ready()
    {
        MaxHealth = 500f;
        CurrentHealth = 500f;
        HealingRadius = 200f;
        HealingAmount = 1.5f;
        RegenerationRate = 4f;

        base._Ready();
    }

    public override CampType GetCampType()
    {
        return CampType.Castle;
    }
}