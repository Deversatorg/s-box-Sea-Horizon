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
		var manager = FindManager();
		if ( TryDeliverFromInventory( other.GameObject, manager ) )
			return;

		var item = other.GameObject.GetComponentInParent<ObjectiveItem>( true, true );
		var target = item.IsValid() ? item.GameObject : other.GameObject;

		if ( !target.IsValid() || delivered.Contains( target ) )
			return;

		if ( RequireObjectiveItem && !item.IsValid() )
			return;

		if ( !string.IsNullOrWhiteSpace( RequiredTag ) && !target.Tags.Has( RequiredTag ) )
			return;

		if ( !manager.IsValid() || !manager.CanReceiveDeliveries )
			return;

		delivered.Add( target );

		if ( item.IsValid() )
		{
			item.CompleteObjective();
		}
		else
		{
			manager?.AddProgress( 1, FindRecoveredValue( target ) );
		}

		ClearInventoryReferences( target );

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

	int FindRecoveredValue( GameObject target )
	{
		if ( !target.IsValid() )
			return 0;

		var carryable = target.GetComponentInParent<ICarryable>( true, true )
			?? target.GetComponentInChildren<ICarryable>( true );

		return carryable?.Value ?? 0;
	}

	bool TryDeliverFromInventory( GameObject source, ObjectiveManager manager )
	{
		if ( !source.IsValid() )
			return false;

		var inventory = source.GetComponentInParent<PlayerInventory>( true, true )
			?? source.GetComponentInChildren<PlayerInventory>( true );

		if ( !inventory.IsValid() )
			return false;

		if ( !manager.IsValid() )
		{
			ShowFeedback( source, "NO DELIVERY RECEIVER", "danger" );
			return false;
		}

		if ( !manager.CanReceiveDeliveries )
		{
			ShowFeedback( source, DeliveryBlockedText( manager ), DeliveryBlockedClass( manager ) );
			return false;
		}

		var deliveredAny = false;
		var deliveredCount = 0;
		var deliveredValue = 0;
		while ( manager.CanReceiveDeliveries && inventory.TryRemoveFirstMatchingItem( RequiredTag, out var value ) )
		{
			manager.AddProgress( 1, value );
			deliveredAny = true;
			deliveredCount++;
			deliveredValue += value;
		}

		if ( deliveredAny )
		{
			if ( manager.ContractState == ObjectiveContractState.Complete && manager.HasContractFeedback )
			{
				ShowFeedback( source, manager.ContractFeedbackText, manager.ContractFeedbackClass );
			}
			else
			{
				var count = deliveredCount > 1 ? $" x{deliveredCount}" : string.Empty;
				var value = deliveredValue > 0 ? $"  +${deliveredValue}" : string.Empty;
				ShowFeedback( source, $"SCRAP DELIVERED{count}{value}", "success" );
			}
		}
		else
		{
			ShowFeedback( source, "NO MATCHING CARGO", "warning" );
		}

		return deliveredAny;
	}

	string DeliveryBlockedText( ObjectiveManager manager )
	{
		return manager.ContractState switch
		{
			ObjectiveContractState.Ready => "START A CONTRACT FIRST",
			ObjectiveContractState.Paused => "CONTRACT PAUSED // SPECIAL EVENT",
			ObjectiveContractState.Complete => "CONTRACT ALREADY COMPLETE",
			ObjectiveContractState.Failed => "CONTRACT FAILED",
			_ => "DELIVERY CLOSED"
		};
	}

	string DeliveryBlockedClass( ObjectiveManager manager )
	{
		return manager.ContractState switch
		{
			ObjectiveContractState.Complete => "success",
			ObjectiveContractState.Failed => "danger",
			_ => "warning"
		};
	}

	void ShowFeedback( GameObject source, string text, string feedbackClass )
	{
		var interactor = source.GetComponentInParent<PlayerInteractor>( true, true )
			?? source.GetComponentInChildren<PlayerInteractor>( true );

		interactor?.ShowFeedback( text, feedbackClass );
	}

	void ClearInventoryReferences( GameObject target )
	{
		if ( !target.IsValid() )
			return;

		var item = target.GetComponentInParent<PickupableItem>( true, true )
			?? target.GetComponentInChildren<PickupableItem>( true );
		var body = target.GetComponentInParent<Rigidbody>( true, true )
			?? target.GetComponentInChildren<Rigidbody>( true );

		foreach ( var inventory in Scene.GetAllComponents<PlayerInventory>() )
		{
			inventory.RemoveItem( item, body );
		}
	}
}
