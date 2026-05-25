using Sandbox;

namespace Sandbox;

[Title( "Inventory Item" )]
[Category( "Gameplay" )]
[Icon( "shopping_bag" )]
public sealed class InventoryItem : Component, ICarryable
{
	[Property, Description( "Имя предмета, отображаемое в интерфейсе" )] 
    public string ItemName { get; set; } = "Scrap";
	
    [Property, Description( "Стоимость предмета при продаже" )] 
    public int Value { get; set; } = 10;
}
