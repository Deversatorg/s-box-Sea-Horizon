namespace Sandbox;

[Title( "Delivery Zone" )]
[Category( "Gameplay" )]
[Icon( "inventory" )]
public sealed class DeliveryZone : Component, Component.ITriggerListener
{
	[Property] public ObjectiveManager Manager { get; set; }
	[Property] public string RequiredTag { get; set; } = "objective";
	[Property] public bool RequireObjectiveItem { get; set; } = true;
	[Property] public bool DisableDeliveredObject { get; set; } = true;

	readonly HashSet<GameObject> delivered = new();

	public void OnTriggerEnter( Collider other )
	{
		var item = other.GameObject.GetComponentInParent<ObjectiveItem>( true, true );
		var target = item.IsValid() ? item.GameObject : other.GameObject;

		if ( !target.IsValid() || delivered.Contains( target ) )
			return;

		if ( RequireObjectiveItem && !item.IsValid() )
			return;

		if ( !string.IsNullOrWhiteSpace( RequiredTag ) && !target.Tags.Has( RequiredTag ) )
			return;

		delivered.Add( target );

		if ( item.IsValid() )
		{
			item.CompleteObjective();
		}
		else
		{
			FindManager()?.AddProgress();
		}

		if ( DisableDeliveredObject && target.IsValid() )
			target.Enabled = false;
	}

	public void OnTriggerExit( Collider other )
	{
	}

	ObjectiveManager FindManager()
	{
		if ( Manager.IsValid() )
			return Manager;

		Manager = Scene.GetAllComponents<ObjectiveManager>().FirstOrDefault();
		return Manager;
	}
}
