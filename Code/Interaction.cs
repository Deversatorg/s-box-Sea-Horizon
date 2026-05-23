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

public enum ObjectiveContractState
{
	Ready,
	Active,
	Paused,
	Complete,
	Failed
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
	[Property, Sync] public int RequiredCount { get; set; } = 3;
	[Property, Sync] public int CompletedCount { get; set; }
	[Property, Sync] public string ObjectiveName { get; set; } = "Objective";
	[Property, Sync] public string ContractTarget { get; set; } = "Recover treasure";
	[Property, Sync] public float ContractDuration { get; set; } = 300f;
	[Property, Sync] public int TargetValue { get; set; } = 500;
	[Property, Sync] public int RecoveredValue { get; set; }
	[Sync] public int EarnedCash { get; private set; }
	[Sync] public ObjectiveContractState ContractState { get; private set; } = ObjectiveContractState.Ready;
	[Sync] public string ContractFeedbackText { get; private set; }
	[Sync] public string ContractFeedbackClass { get; private set; } = "neutral";
	[Sync] public TimeSince ContractFeedbackElapsed { get; private set; }
	[Sync] public float ContractFeedbackDuration { get; private set; } = 2.5f;
	[Sync] public float PausedRemainingSeconds { get; private set; }
	[Property] public bool AutoStart { get; set; }
	[Property] public List<GameObject> OnCompleteTargets { get; set; } = new();

	[Sync] public bool ContractStarted { get; private set; }
	[Sync] public TimeSince ContractElapsed { get; private set; }

	public bool IsComplete => ContractState == ObjectiveContractState.Complete || CompletedCount >= RequiredCount;
	public bool IsExpired => ContractState == ObjectiveContractState.Failed
		|| (ContractState == ObjectiveContractState.Active && ContractDuration > 0f && RemainingSeconds <= 0f && CompletedCount < RequiredCount);
	public bool CanReceiveDeliveries => ContractState == ObjectiveContractState.Active;
	public bool HasContractFeedback => !string.IsNullOrWhiteSpace( ContractFeedbackText ) && ContractFeedbackElapsed < ContractFeedbackDuration;
	public float RemainingSeconds => ContractState == ObjectiveContractState.Paused
		? PausedRemainingSeconds
		: !ContractStarted || ContractDuration <= 0f
		? System.MathF.Max( ContractDuration, 0f )
		: System.MathF.Max( ContractDuration - ContractElapsed, 0f );

	protected override void OnStart()
	{
		if ( IsProxy )
			return;

		if ( AutoStart )
		{
			StartConfiguredContract( ContractTarget, RequiredCount, TargetValue, ContractDuration, ObjectiveName );
			return;
		}

		CompletedCount = 0;
		RecoveredValue = 0;
		ContractElapsed = 0f;
		PausedRemainingSeconds = 0f;
		ContractStarted = false;
		ContractState = ObjectiveContractState.Ready;
		ClearContractFeedback();
	}

	protected override void OnUpdate()
	{
		if ( IsProxy || ContractState != ObjectiveContractState.Active || ContractDuration <= 0f )
			return;

		if ( RemainingSeconds <= 0f && CompletedCount < RequiredCount )
			Fail();
	}

	[Rpc.Host]
	public void StartConfiguredContract( string contractTarget, int requiredCount, int targetValue, float contractDuration, string objectiveName )
	{
		if ( !string.IsNullOrWhiteSpace( contractTarget ) )
			ContractTarget = contractTarget;

		if ( !string.IsNullOrWhiteSpace( objectiveName ) )
			ObjectiveName = objectiveName;

		RequiredCount = System.Math.Max( requiredCount, 1 );
		TargetValue = System.Math.Max( targetValue, 0 );
		ContractDuration = System.MathF.Max( contractDuration, 0f );
		CompletedCount = 0;
		RecoveredValue = 0;
		ContractElapsed = 0f;
		PausedRemainingSeconds = 0f;
		ContractStarted = true;
		ContractState = ObjectiveContractState.Active;
		ClearContractFeedback();

		Log.Info( $"{ObjectiveName} started: {ContractTarget}" );
	}

	public void AddProgress( int amount = 1, int recoveredValue = 0 )
	{
		if ( !CanReceiveDeliveries )
			return;

		RecoveredValue = System.Math.Max( RecoveredValue + recoveredValue, 0 );
		CompletedCount = System.Math.Min( CompletedCount + amount, RequiredCount );
		ShowContractFeedback( recoveredValue > 0 ? $"SCRAP DELIVERED  +${recoveredValue}" : "SCRAP DELIVERED", "success" );
		Log.Info( $"{ObjectiveName}: {CompletedCount}/{RequiredCount}, recovered ${RecoveredValue}" );

		if ( IsComplete )
			Complete();
	}

	void Complete()
	{
		var payout = System.Math.Max( RecoveredValue, 0 );
		AwardCash( payout, ObjectiveName );
		ContractState = ObjectiveContractState.Complete;
		ShowContractFeedback( payout > 0 ? $"CONTRACT COMPLETE  +${payout}" : "CONTRACT COMPLETE", "success", 4f );
		Log.Info( $"{ObjectiveName} complete, paid ${payout}" );

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

	void Fail()
	{
		ContractState = ObjectiveContractState.Failed;
		ShowContractFeedback( "CONTRACT FAILED", "danger", 4f );
		Log.Info( $"{ObjectiveName} failed" );
	}

	public void PauseForSpecialEvent()
	{
		if ( ContractState != ObjectiveContractState.Active )
			return;

		PausedRemainingSeconds = RemainingSeconds;
		ContractState = ObjectiveContractState.Paused;
		ClearContractFeedback();
	}

	public void ResumeAfterSpecialEvent()
	{
		if ( ContractState != ObjectiveContractState.Paused )
			return;

		ContractDuration = PausedRemainingSeconds;
		ContractElapsed = 0f;
		PausedRemainingSeconds = 0f;
		ContractState = ObjectiveContractState.Active;
		ClearContractFeedback();
	}

	public void CancelPausedContract()
	{
		if ( ContractState != ObjectiveContractState.Paused )
			return;

		PausedRemainingSeconds = 0f;
		ContractStarted = false;
		ContractState = ObjectiveContractState.Failed;
		ShowContractFeedback( "CONTRACT CANCELLED", "danger", 4f );
	}

	public void AwardCash( int amount, string reason )
	{
		if ( amount <= 0 )
			return;

		EarnedCash += amount;
		Log.Info( $"Reward paid: ${amount} ({reason})" );
	}

	public void ShowContractFeedback( string text, string feedbackClass = "neutral", float duration = 2.5f )
	{
		ContractFeedbackText = text;
		ContractFeedbackClass = string.IsNullOrWhiteSpace( feedbackClass ) ? "neutral" : feedbackClass;
		ContractFeedbackDuration = System.MathF.Max( duration, 0.1f );
		ContractFeedbackElapsed = 0f;
	}

	void ClearContractFeedback()
	{
		ContractFeedbackText = string.Empty;
		ContractFeedbackClass = "neutral";
		ContractFeedbackDuration = 0f;
		ContractFeedbackElapsed = 0f;
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

		var manager = FindManager();
		if ( !manager.IsValid() || !manager.CanReceiveDeliveries )
			return;

		Completed = true;
		manager?.AddProgress( Amount, FindRecoveredValue() );

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

	int FindRecoveredValue()
	{
		var carryable = GameObject.GetComponentInParent<ICarryable>( true, true )
			?? GameObject.GetComponentInChildren<ICarryable>( true );

		return carryable?.Value ?? 0;
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
