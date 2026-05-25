namespace Sandbox;

public enum SpecialEventState
{
	Idle,
	Offered,
	Active,
	Complete,
	Failed
}

public enum SpecialEventOfferPolicy
{
	Optional,
	Mandatory
}

[Title( "Pirate Raid Controller" )]
[Category( "Gameplay" )]
[Icon( "swords" )]
public sealed class PirateRaidController : Component
{
	[Property] public ObjectiveManager ObjectiveManager { get; set; }
	[Property] public List<GameObject> PirateActors { get; set; } = new();
	[Property] public SpecialEventOfferPolicy OfferPolicy { get; set; } = SpecialEventOfferPolicy.Optional;
	[Property] public string EventKind { get; set; } = "SPECIAL QUEST";
	[Property] public string EventTitle { get; set; } = "PIRATE RAID";
	[Property] public string EventObjective { get; set; } = "REPEL THE BOARDING PARTY";
	[Property] public string EventBriefing { get; set; } = "Hostile crew boarding the recovery zone.";
	[Property] public string FailureWarning { get; set; } = "Team wipe cancels all paused contracts.";
	[Property] public string ActiveCounterLabel { get; set; } = "PIRATES LEFT";
	[Property] public int RewardCash { get; set; } = 150;

	[Sync] public SpecialEventState EventState { get; private set; } = SpecialEventState.Idle;
	[Sync] public int PiratesRemaining { get; private set; }
	[Sync] public int TotalPirates { get; private set; }
	[Sync] public string EventFeedbackText { get; private set; }
	[Sync] public string EventFeedbackClass { get; private set; } = "warning";
	[Sync] public TimeSince EventFeedbackElapsed { get; private set; }
	[Sync] public float EventFeedbackDuration { get; private set; } = 3f;

	public bool IsOfferVisible => EventState == SpecialEventState.Offered;
	public bool IsRaidActive => EventState == SpecialEventState.Active;
	public bool IsMandatoryOffer => OfferPolicy == SpecialEventOfferPolicy.Mandatory;
	public bool CanDismissOffer => IsOfferVisible && !IsMandatoryOffer;
	public int ConfiguredEnemyCount => PirateActors.Count( x => x.IsValid() );
	public bool CanOffer => (EventState is SpecialEventState.Idle or SpecialEventState.Complete or SpecialEventState.Failed)
		&& !Scene.GetAllComponents<PirateRaidController>().Any( x => x.IsValid()
			&& x != this
			&& (x.IsOfferVisible || x.IsRaidActive) );
	public bool HasEventFeedback => !string.IsNullOrWhiteSpace( EventFeedbackText ) && EventFeedbackElapsed < EventFeedbackDuration;

	protected override void OnStart()
	{
		if ( IsProxy )
			return;

		CleanupPirates();
		EventState = SpecialEventState.Idle;
	}

	protected override void OnUpdate()
	{
		if ( IsProxy || EventState != SpecialEventState.Active )
			return;

		GrantMusketsToPlayers();

		var crew = Scene.GetAllComponents<HealthComponent>()
			.Where( x => x.IsValid() && x.GameObject.Active && x.GameObject.Tags.Has( "player" ) )
			.ToArray();

		if ( crew.Length > 0 && crew.All( x => x.IsDead ) )
			FailRaid();
	}

	[Rpc.Host]
	public void OfferRaid()
	{
		if ( !CanOffer )
			return;

		EventState = SpecialEventState.Offered;
		PiratesRemaining = 0;
		TotalPirates = 0;
		ClearEventFeedback();
	}

	[Rpc.Host]
	public void DismissOffer()
	{
		if ( !CanDismissOffer )
			return;

		EventState = SpecialEventState.Idle;
	}

	[Rpc.Host]
	public void StartRaid()
	{
		if ( EventState != SpecialEventState.Offered )
			return;

		PauseContractsForSpecialEvent();

		TotalPirates = 0;
		PiratesRemaining = 0;

		foreach ( var actor in PirateActors )
		{
			if ( !actor.IsValid() )
				continue;

			actor.Enabled = true;
			var pirate = actor.GetComponent<PirateEnemy>();
			if ( !pirate.IsValid() )
				continue;

			pirate.BeginRaid( this );
			TotalPirates++;
			PiratesRemaining++;
		}

		if ( PiratesRemaining <= 0 )
		{
			EventState = SpecialEventState.Failed;
			ShowEventFeedback( "RAID FAILED // NO BOARDERS FOUND", "danger", 4f );
			ResumePausedContracts();
			return;
		}

		EventState = SpecialEventState.Active;
		GrantMusketsToPlayers();
		ClearEventFeedback();
	}

	public void NotifyPirateDefeated( PirateEnemy pirate )
	{
		if ( EventState != SpecialEventState.Active )
			return;

		PiratesRemaining = System.Math.Max( PiratesRemaining - 1, 0 );

		if ( pirate.IsValid() )
			pirate.GameObject.Enabled = false;

		if ( PiratesRemaining <= 0 )
			CompleteRaid();
	}

	void CompleteRaid()
	{
		if ( EventState != SpecialEventState.Active )
			return;

		EventState = SpecialEventState.Complete;
		CleanupPirates();

		var manager = FindObjectiveManager();
		manager?.AwardCash( RewardCash, EventTitle );
		ResumePausedContracts();

		ShowEventFeedback( $"RAID REPELLED +${RewardCash}", "success", 4f );
	}

	void FailRaid()
	{
		if ( EventState != SpecialEventState.Active )
			return;

		EventState = SpecialEventState.Failed;
		CleanupPirates();

		CancelPausedContracts();
		ShowEventFeedback( "CREW LOST // ALL CONTRACTS CANCELLED", "danger", 5f );
	}

	void CleanupPirates()
	{
		foreach ( var actor in PirateActors )
		{
			if ( actor.IsValid() )
				actor.Enabled = false;
		}

		PiratesRemaining = 0;
	}

	void GrantMusketsToPlayers()
	{
		foreach ( var musket in Scene.GetAllComponents<PlayerMusket>() )
		{
			if ( musket.IsValid() && musket.GameObject.Active && musket.GameObject.Tags.Has( "player" ) )
				musket.GrantMusket();
		}
	}

	ObjectiveManager FindObjectiveManager()
	{
		if ( ObjectiveManager.IsValid() )
			return ObjectiveManager;

		ObjectiveManager = Scene.GetAllComponents<ObjectiveManager>().FirstOrDefault();
		return ObjectiveManager;
	}

	void PauseContractsForSpecialEvent()
	{
		foreach ( var manager in Scene.GetAllComponents<ObjectiveManager>() )
		{
			if ( manager.IsValid() )
				manager.PauseForSpecialEvent();
		}
	}

	void ResumePausedContracts()
	{
		foreach ( var manager in Scene.GetAllComponents<ObjectiveManager>() )
		{
			if ( manager.IsValid() )
				manager.ResumeAfterSpecialEvent();
		}
	}

	void CancelPausedContracts()
	{
		foreach ( var manager in Scene.GetAllComponents<ObjectiveManager>() )
		{
			if ( manager.IsValid() )
				manager.CancelPausedContract();
		}
	}

	void ShowEventFeedback( string text, string feedbackClass, float duration )
	{
		EventFeedbackText = text;
		EventFeedbackClass = feedbackClass;
		EventFeedbackDuration = System.MathF.Max( duration, 0.1f );
		EventFeedbackElapsed = 0f;
	}

	void ClearEventFeedback()
	{
		EventFeedbackText = string.Empty;
		EventFeedbackClass = "neutral";
		EventFeedbackDuration = 0f;
		EventFeedbackElapsed = 0f;
	}
}

[Title( "Special Event Starter" )]
[Category( "Gameplay" )]
[Icon( "campaign" )]
public sealed class SpecialEventStarter : Component, IInteractable
{
	[Property] public PirateRaidController Raid { get; set; }

	public bool CanInteract( InteractionContext context )
	{
		return FindRaid().IsValid() && FindRaid().CanOffer;
	}

	public void Interact( InteractionContext context )
	{
		FindRaid()?.OfferRaid();
	}

	public string GetPromptText()
	{
		var raid = FindRaid();
		return raid.IsValid() ? $"E: Inspect {raid.EventTitle}" : "E: Inspect special event";
	}

	PirateRaidController FindRaid()
	{
		if ( Raid.IsValid() )
			return Raid;

		Raid = Scene.GetAllComponents<PirateRaidController>().FirstOrDefault();
		return Raid;
	}
}

[Title( "Pirate Enemy" )]
[Category( "Gameplay" )]
[Icon( "person_alert" )]
public sealed class PirateEnemy : Component
{
	[Property] public PirateRaidController Raid { get; set; }
	[Property] public float MoveSpeed { get; set; } = 110f;
	[Property] public float AttackRange { get; set; } = 46f;
	[Property] public float AttackDamage { get; set; } = 15f;
	[Property] public float AttackCooldown { get; set; } = 1f;

	HealthComponent health;
	TimeSince timeSinceAttack;
	bool hasBeenActivated;
	bool deathReported;

	protected override void OnStart()
	{
		health = GetComponent<HealthComponent>();
		timeSinceAttack = AttackCooldown;
	}

	public void BeginRaid( PirateRaidController raid )
	{
		Raid = raid;
		deathReported = false;

		if ( !health.IsValid() )
			health = GetComponent<HealthComponent>();

		if ( hasBeenActivated && health.IsValid() )
			health.Respawn();

		hasBeenActivated = true;
		timeSinceAttack = AttackCooldown;
	}

	protected override void OnFixedUpdate()
	{
		if ( IsProxy || !Raid.IsValid() || Raid.EventState != SpecialEventState.Active )
			return;

		if ( !health.IsValid() )
			health = GetComponent<HealthComponent>();

		if ( !health.IsValid() || health.IsDead )
		{
			ReportDeath();
			return;
		}

		var target = Scene.GetAllComponents<HealthComponent>()
			.Where( x => x.IsValid() && x.GameObject.Active && x.GameObject.Tags.Has( "player" ) && !x.IsDead )
			.OrderBy( x => (x.WorldPosition - WorldPosition).Length )
			.FirstOrDefault();

		if ( !target.IsValid() )
			return;

		var toTarget = target.WorldPosition - WorldPosition;
		var horizontal = new Vector3( toTarget.x, toTarget.y, 0f );
		var distance = horizontal.Length;

		if ( distance > AttackRange )
			WorldPosition += horizontal.Normal * MoveSpeed * Time.Delta;

		if ( distance <= AttackRange && timeSinceAttack >= AttackCooldown )
		{
			target.TakeDamage( AttackDamage );
			timeSinceAttack = 0f;
		}
	}

	void ReportDeath()
	{
		if ( deathReported )
			return;

		deathReported = true;
		Raid?.NotifyPirateDefeated( this );
	}
}
