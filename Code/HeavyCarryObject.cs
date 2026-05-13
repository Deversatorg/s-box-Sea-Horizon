namespace Sandbox;

[Title( "Heavy Carry Object" )]
[Category( "Gameplay" )]
[Icon( "fitness_center" )]
public sealed class HeavyCarryObject : Component
{
	[Property] public int RequiredCarriers { get; set; } = 2;
	[Property] public int MaxCarriers { get; set; } = 2;
	[Property] public float UnderstaffedSpeedMultiplier { get; set; } = 0.35f;

	readonly HashSet<CarryInteractor> carriers = new();
	bool savedPhysics;
	bool savedGravity;
	float savedLinearDamping;
	float savedAngularDamping;

	public int CarrierCount
	{
		get
		{
			CleanupCarriers();
			return carriers.Count;
		}
	}

	public bool HasEnoughCarriers => CarrierCount >= System.Math.Max( RequiredCarriers, 1 );
	public float SpeedMultiplier => HasEnoughCarriers ? 1f : System.MathF.Max( UnderstaffedSpeedMultiplier, 0f );

	public bool TryRegister( CarryInteractor carrier )
	{
		if ( !carrier.IsValid() )
			return false;

		CleanupCarriers();

		if ( MaxCarriers > 0 && !carriers.Contains( carrier ) && carriers.Count >= MaxCarriers )
			return false;

		carriers.Add( carrier );
		return true;
	}

	public void PrepareHeldPhysics( Rigidbody body )
	{
		var physicsBody = body?.PhysicsBody;
		if ( physicsBody is null )
			return;

		if ( !savedPhysics )
		{
			savedGravity = body.Gravity;
			savedLinearDamping = physicsBody.LinearDamping;
			savedAngularDamping = physicsBody.AngularDamping;
			savedPhysics = true;
		}

		body.Gravity = false;
		physicsBody.LinearDamping = 5f;
		physicsBody.AngularDamping = 8f;
		physicsBody.Sleeping = false;
	}

	public void Release( CarryInteractor carrier, Rigidbody body )
	{
		if ( carrier is null )
			return;

		carriers.Remove( carrier );

		if ( carriers.Count > 0 || !savedPhysics )
			return;

		var physicsBody = body?.PhysicsBody;
		if ( physicsBody is null )
			return;

		body.Gravity = savedGravity;
		physicsBody.LinearDamping = savedLinearDamping;
		physicsBody.AngularDamping = savedAngularDamping;
		physicsBody.Sleeping = false;
		savedPhysics = false;
	}

	void CleanupCarriers()
	{
		carriers.RemoveWhere( carrier => !carrier.IsValid() || !carrier.HeldBody.IsValid() );
	}
}
