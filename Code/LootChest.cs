namespace Sandbox;

[Title( "Loot Chest" )]
[Category( "Gameplay" )]
[Icon( "inventory_2" )]
public sealed class LootChest : Component, IInteractable
{
	[Property] public List<GameObject> LootObjects { get; set; } = new();
	[Property] public Vector3 DropOffset { get; set; } = new( 0f, 0f, 42f );
	[Property] public float DropSpacing { get; set; } = 34f;
	[Property] public bool DisableAfterOpen { get; set; }
	[Property, Sync] public bool Opened { get; set; }

	public bool CanInteract( InteractionContext context )
	{
		return true;
	}

	public void Interact( InteractionContext context )
	{
		var interactor = context.User.GetComponent<PlayerInteractor>();
		if ( Opened )
		{
			interactor?.ShowFeedback( "CHEST ALREADY OPEN", "warning" );
			return;
		}

		Open();
	}

	[Rpc.Host]
	public void Open()
	{
		if ( Opened )
			return;

		Opened = true;

		var validLoot = LootObjects.Where( x => x.IsValid() ).ToArray();
		var middle = (validLoot.Length - 1) * 0.5f;

		for ( var i = 0; i < validLoot.Length; i++ )
		{
			var loot = validLoot[i];
			var dropPosition = WorldPosition + DropOffset + new Vector3( (i - middle) * DropSpacing, 0f, 0f );

			loot.Enabled = true;
			loot.WorldPosition = dropPosition;
			loot.WorldRotation = WorldRotation;

			var body = loot.GetComponent<Rigidbody>();
			if ( body.IsValid() )
			{
				body.WorldPosition = dropPosition;
				body.WorldRotation = WorldRotation;

				var physicsBody = body.PhysicsBody;
				if ( physicsBody != null )
				{
					physicsBody.Sleeping = false;
					physicsBody.Velocity = new Vector3( (i - middle) * 40f, 0f, 90f );
					physicsBody.AngularVelocity = new Vector3( 0f, 0f, (i - middle) * 2f );
				}
			}
		}

		if ( DisableAfterOpen )
			GameObject.Enabled = false;
	}
}
