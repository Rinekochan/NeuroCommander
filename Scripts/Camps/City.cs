using Godot;
using System;

namespace NeuroWarCommander.Scripts.Camps;

public partial class City : CampBase
{
    public override void _Ready()
    {
        MaxHealth = 250f;
        CurrentHealth = 250f;
        HealingRadius = 150f;
        HealingAmount = 1.25f;
        RegenerationRate = 2f;

        base._Ready();
    }

    public override CampType GetCampType()
    {
        return CampType.City;
    }
}