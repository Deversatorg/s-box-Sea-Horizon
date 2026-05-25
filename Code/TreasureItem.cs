using Sandbox;

namespace Sandbox;

[Title( "Treasure Item" )]
[Category( "Gameplay" )]
[Icon( "diamond" )]
public sealed class TreasureItem : Component, ICarryable
{
	[RequireComponent] public HeavyCarryObject HeavyCarry { get; set; }

	[Property, Description( "Имя сокровища, отображаемое в интерфейсе" )] 
    public string ItemName { get; set; } = "Treasure";
	
    [Property, Description( "Стоимость сокровища при продаже" )] 
    public int Value { get; set; } = 500;
}
