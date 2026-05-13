namespace Sandbox;

public readonly struct InteractionContext
{
	public InteractionContext( GameObject user, Component source, Vector3 hitPosition, Vector3 hitNormal )
	{
		User = user;
		Source = source;
		HitPosition = hitPosition;
		HitNormal = hitNormal;
	}

	public GameObject User { get; }
	public Component Source { get; }
	public Vector3 HitPosition { get; }
	public Vector3 HitNormal { get; }
}

public interface IInteractable
{
	bool CanInteract( InteractionContext context );
	void Interact( InteractionContext context );
}

public interface IHoldInteractable : IInteractable
{
	float HoldDuration { get; }
	string HoldPrompt { get; }
}

public interface IActivatable
{
	void Activate( Component source );
	void Deactivate( Component source );
	void Toggle( Component source );
}

[Title( "Interactable Button" )]
[Category( "Gameplay" )]
[Icon( "ads_click" )]
public sealed class InteractableButton : Component, IInteractable
{
	[Property] public List<GameObject> Targets { get; set; } = new();
	[Property] public bool OneShot { get; set; }
	[Property] public bool Activated { get; set; }

	public bool CanInteract( InteractionContext context )
	{
		return !OneShot || !Activated;
	}

	public void Interact( InteractionContext context )
	{
		if ( OneShot && Activated )
			return;

		Activated = !Activated || OneShot;

		foreach ( var target in Targets )
		{
			if ( !target.IsValid() )
				continue;

			foreach ( var activatable in target.GetComponentsInChildren<IActivatable>( true, true ) )
			{
				if ( OneShot )
					activatable.Activate( this );
				else
					activatable.Toggle( this );
			}
		}
	}
}

[Title( "Sliding Door" )]
[Category( "Gameplay" )]
[Icon( "door_sliding" )]
public sealed class SlidingDoor : Component, IActivatable
{
	[Property] public Vector3 OpenOffset { get; set; } = Vector3.Up * 96f;
	[Property] public float MoveSpeed { get; set; } = 6f;
	[Property] public bool StartsOpen { get; set; }
	[Property] public bool IsOpen { get; set; }

	Vector3 closedPosition;
	Vector3 openPosition;

	protected override void OnStart()
	{
		closedPosition = LocalPosition;
		openPosition = closedPosition + OpenOffset;
		IsOpen = StartsOpen;
		LocalPosition = IsOpen ? openPosition : closedPosition;
	}

	protected override void OnUpdate()
	{
		var target = IsOpen ? openPosition : closedPosition;
		var delta = target - LocalPosition;
		var t = System.MathF.Min( Time.Delta * MoveSpeed, 1f );
		LocalPosition += delta * t;
	}

	public void Activate( Component source )
	{
		IsOpen = true;
	}

	public void Deactivate( Component source )
	{
		IsOpen = false;
	}

	public void Toggle( Component source )
	{
		IsOpen = !IsOpen;
	}
}

[Title( "Objective Manager" )]
[Category( "Gameplay" )]
[Icon( "checklist" )]
public sealed class ObjectiveManager : Component
{
	[Property] public int RequiredCount { get; set; } = 3;
	[Property] public int CompletedCount { get; set; }
	[Property] public string ObjectiveName { get; set; } = "Objective";
	[Property] public List<GameObject> OnCompleteTargets { get; set; } = new();

	public bool IsComplete => CompletedCount >= RequiredCount;

	public void AddProgress( int amount = 1 )
	{
		if ( IsComplete )
			return;

		CompletedCount = System.Math.Min( CompletedCount + amount, RequiredCount );
		Log.Info( $"{ObjectiveName}: {CompletedCount}/{RequiredCount}" );

		if ( IsComplete )
			Complete();
	}

	void Complete()
	{
		Log.Info( $"{ObjectiveName} complete" );

		foreach ( var target in OnCompleteTargets )
		{
			if ( !target.IsValid() )
				continue;

			foreach ( var activatable in target.GetComponentsInChildren<IActivatable>( true, true ) )
			{
				activatable.Activate( this );
			}
		}
	}
}

[Title( "Objective Item" )]
[Category( "Gameplay" )]
[Icon( "inventory_2" )]
public sealed class ObjectiveItem : Component, IInteractable
{
	[Property] public ObjectiveManager Manager { get; set; }
	[Property] public int Amount { get; set; } = 1;
	[Property] public bool CanUseDirectly { get; set; } = true;
	[Property] public bool DisableAfterUse { get; set; } = true;
	[Property] public bool Completed { get; set; }

	public bool CanInteract( InteractionContext context )
	{
		return CanUseDirectly && !Completed;
	}

	public void Interact( InteractionContext context )
	{
		CompleteObjective();
	}

	public void CompleteObjective()
	{
		if ( Completed )
			return;

		Completed = true;
		FindManager()?.AddProgress( Amount );

		if ( DisableAfterUse )
			GameObject.Enabled = false;
	}

	ObjectiveManager FindManager()
	{
		if ( Manager.IsValid() )
			return Manager;

		Manager = Scene.GetAllComponents<ObjectiveManager>().FirstOrDefault();
		return Manager;
	}
}

[Title( "Co-op Pressure Plate" )]
[Category( "Gameplay" )]
[Icon( "groups" )]
public sealed class CoopPressurePlate : Component, Component.ITriggerListener
{
	[Property] public int RequiredPlayers { get; set; } = 2;
	[Property] public List<GameObject> Targets { get; set; } = new();

	readonly HashSet<HealthComponent> players = new();
	bool active;

	protected override void OnFixedUpdate()
	{
		players.RemoveWhere( player => !player.IsValid() || player.IsDead );
		UpdateActiveState();
	}

	public void OnTriggerEnter( Collider other )
	{
		var health = other.GameObject.GetComponentInParent<HealthComponent>( true, true );
		if ( health.IsValid() && health.GameObject.Tags.Has( "player" ) )
		{
			players.Add( health );
			UpdateActiveState();
		}
	}

	public void OnTriggerExit( Collider other )
	{
		var health = other.GameObject.GetComponentInParent<HealthComponent>( true, true );
		if ( health.IsValid() )
		{
			players.Remove( health );
			UpdateActiveState();
		}
	}

	void UpdateActiveState()
	{
		var shouldBeActive = players.Count >= RequiredPlayers;
		if ( shouldBeActive == active )
			return;

		active = shouldBeActive;

		foreach ( var target in Targets )
		{
			if ( !target.IsValid() )
				continue;

			foreach ( var activatable in target.GetComponentsInChildren<IActivatable>( true, true ) )
			{
				if ( active )
					activatable.Activate( this );
				else
					activatable.Deactivate( this );
			}
		}
	}
}
