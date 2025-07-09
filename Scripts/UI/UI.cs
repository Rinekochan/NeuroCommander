using Godot;
using System;

namespace NeuroWarCommander.Scripts.UI;

public partial class UI : CanvasLayer
{
    [Export] public NodePath World { get; set; }
}
