using System.Threading.Tasks;

namespace Sandbox;

[Title( "Health" )]
[Category( "Gameplay" )]
[Icon( "favorite" )]
public sealed class HealthComponent : Component, IHoldInteractable
{
	[Property] public float MaxHealth { get; set; } = 100f;
	[Property] public float CurrentHealth { get; set; } = 100f;
	[Property] public float RespawnDelay { get; set; } = 2f;
	[Property] public float ReviveHoldDuration { get; set; } = 2f;
	[Property] public float ReviveHealthFraction { get; set; } = 0.5f;
	[Property] public GameObject SpawnPoint { get; set; }
	[Property] public bool RespawnOnDeath { get; set; } = true;
	[Property] public bool CanBeRevivedByUse { get; set; } = true;
	[Property] public bool DisableMovementOnDeath { get; set; } = true;
	[Property] public bool HideBodyOnDeath { get; set; }

	public bool IsDead { get; private set; }
	public float HoldDuration => ReviveHoldDuration;
	public string HoldPrompt => "Hold E: Revive";

	Vector3 startPosition;
	Rotation startRotation;
	PlayerController controller;
	Rigidbody body;
	SkinnedModelRenderer renderer;

	protected override void OnStart()
	{
		startPosition = WorldPosition;
		startRotation = WorldRotation;
		controller = GetComponent<PlayerController>();
		body = GetComponent<Rigidbody>();
		renderer = GetComponentInChildren<SkinnedModelRenderer>( true, true );
		CurrentHealth = MaxHealth;
	}

	public void TakeDamage( float amount )
	{
		if ( IsDead || amount <= 0f )
			return;

		CurrentHealth = System.MathF.Max( CurrentHealth - amount, 0f );

		if ( CurrentHealth <= 0f )
			_ = DieAsync();
	}

	public void Heal( float amount )
	{
		if ( IsDead || amount <= 0f )
			return;

		CurrentHealth = System.MathF.Min( CurrentHealth + amount, MaxHealth );
	}

	public void Kill()
	{
		TakeDamage( MaxHealth );
	}

	async Task DieAsync()
	{
		if ( IsDead )
			return;

		IsDead = true;
		CurrentHealth = 0f;

		SetAliveState( false );

		if ( !RespawnOnDeath )
			return;

		await Task.DelayRealtimeSeconds( RespawnDelay );

		if ( !GameObject.IsValid() || !IsDead )
			return;

		Respawn();
	}

	public void Respawn()
	{
		var spawn = SpawnPoint.IsValid() ? SpawnPoint : null;

		WorldPosition = spawn.IsValid() ? spawn.WorldPosition : startPosition;
		WorldRotation = spawn.IsValid() ? spawn.WorldRotation : startRotation;

		CurrentHealth = MaxHealth;
		IsDead = false;

		if ( body?.PhysicsBody is { } physicsBody )
		{
			physicsBody.Velocity = Vector3.Zero;
			physicsBody.AngularVelocity = Vector3.Zero;
			physicsBody.Sleeping = false;
		}

		SetAliveState( true );
	}

	public bool CanInteract( InteractionContext context )
	{
		return CanBeRevivedByUse && IsDead && context.User != GameObject;
	}

	public void Interact( InteractionContext context )
	{
		if ( CanInteract( context ) )
			Revive();
	}

	public void Revive()
	{
		if ( !IsDead )
			return;

		CurrentHealth = System.MathF.Max( MaxHealth * ReviveHealthFraction, 1f );
		IsDead = false;

		if ( body?.PhysicsBody is { } physicsBody )
		{
			physicsBody.Velocity = Vector3.Zero;
			physicsBody.AngularVelocity = Vector3.Zero;
			physicsBody.Sleeping = false;
		}

		SetAliveState( true );
	}

	void SetAliveState( bool alive )
	{
		if ( DisableMovementOnDeath && controller.IsValid() )
			controller.Enabled = alive;

		if ( body?.PhysicsBody is { } physicsBody )
			physicsBody.MotionEnabled = alive;

		if ( HideBodyOnDeath && renderer.IsValid() )
			renderer.Enabled = alive;
	}
}
