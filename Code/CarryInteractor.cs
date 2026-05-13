namespace Sandbox;

[Title( "Carry Interactor" )]
[Category( "Gameplay" )]
[Icon( "back_hand" )]
public sealed class CarryInteractor : Component
{
	[Property] public float ReachDistance { get; set; } = 120f;
	[Property] public float HoldDistance { get; set; } = 75f;
	[Property] public float HoldHeight { get; set; } = -8f;
	[Property] public float HoldSpring { get; set; } = 12f;
	[Property] public float MaxHoldSpeed { get; set; } = 750f;
	[Property] public float BreakDistance { get; set; } = 260f;
	[Property] public float ThrowSpeed { get; set; } = 700f;
	[Property] public string PickupAction { get; set; } = "use";
	[Property] public string DropAction { get; set; } = "drop";
	[Property] public string ThrowAction { get; set; } = "attack1";

	PlayerController controller;
	Rigidbody heldBody;
	bool heldGravity;
	float heldLinearDamping;
	float heldAngularDamping;
	HeavyCarryObject heldHeavy;
	IHoldInteractable holdTarget;
	float holdTimer;

	public Rigidbody HeldBody => heldBody;
	public string InteractionPrompt { get; private set; }
	public float InteractionProgress { get; private set; }

	protected override void OnStart()
	{
		controller = GetComponent<PlayerController>();
	}

	protected override void OnUpdate()
	{
		InteractionPrompt = null;
		InteractionProgress = 0f;

		if ( IsProxy )
			return;

		if ( !controller.IsValid() )
			return;

		UpdateInteractionState();

		if ( Input.Pressed( PickupAction ) )
		{
			if ( heldBody.IsValid() )
				DropHeld( false );
			else if ( TryInteract( false ) )
				return;
			else
				TryPickup();
		}

		if ( heldBody.IsValid() && Input.Pressed( DropAction ) )
			DropHeld( false );

		if ( heldBody.IsValid() && Input.Pressed( ThrowAction ) )
			DropHeld( true );
	}

	protected override void OnFixedUpdate()
	{
		if ( IsProxy )
			return;

		if ( !heldBody.IsValid() )
			return;

		var physicsBody = heldBody.PhysicsBody;
		if ( physicsBody is null )
		{
			heldBody = null;
			return;
		}

		var targetPosition = GetHoldPosition();
		var toTarget = targetPosition - heldBody.WorldPosition;
		var distance = toTarget.Length;

		if ( distance > BreakDistance )
		{
			DropHeld( false );
			return;
		}

		var heavySpeedMultiplier = heldHeavy.IsValid() ? heldHeavy.SpeedMultiplier : 1f;
		var speed = System.MathF.Min( distance * HoldSpring * heavySpeedMultiplier, MaxHoldSpeed * heavySpeedMultiplier );
		physicsBody.Velocity = distance > 1f ? toTarget.Normal * speed : Vector3.Zero;
		physicsBody.AngularVelocity *= 0.75f;
		physicsBody.Sleeping = false;
	}

	protected override void OnDestroy()
	{
		DropHeld( false );
	}

	void TryPickup()
	{
		if ( !TryFindCarryBody( out var body ) )
			return;

		var physicsBody = body.PhysicsBody;
		if ( physicsBody is null || !physicsBody.MotionEnabled )
			return;

		var heavy = body.GameObject.GetComponentInParent<HeavyCarryObject>( true, true );
		if ( heavy.IsValid() && !heavy.TryRegister( this ) )
			return;

		heldBody = body;
		heldHeavy = heavy;
		heldGravity = body.Gravity;
		heldLinearDamping = physicsBody.LinearDamping;
		heldAngularDamping = physicsBody.AngularDamping;

		if ( body.GameObject.Network.Active && !body.GameObject.Network.IsOwner )
			body.GameObject.Network.TakeOwnership();

		if ( heldHeavy.IsValid() )
		{
			heldHeavy.PrepareHeldPhysics( body );
		}
		else
		{
			body.Gravity = false;
			physicsBody.LinearDamping = 5f;
			physicsBody.AngularDamping = 8f;
			physicsBody.Sleeping = false;
		}
	}

	bool TryInteract( bool allowHoldInteractables )
	{
		if ( !TryFindInteractable( out var interactable, out var context ) )
			return false;

		if ( !allowHoldInteractables && interactable is IHoldInteractable )
			return false;

		interactable.Interact( context );
		return true;
	}

	void UpdateInteractionState()
	{
		if ( heldBody.IsValid() )
		{
			InteractionPrompt = heldHeavy.IsValid()
				? $"E/G: Drop  |  Mouse1: Throw  |  Heavy {heldHeavy.CarrierCount}/{heldHeavy.RequiredCarriers}"
				: "E/G: Drop  |  Mouse1: Throw";
			ResetHold();
			return;
		}

		if ( TryFindInteractable( out var interactable, out var context ) )
		{
			if ( interactable is IHoldInteractable holdInteractable )
			{
				UpdateHoldInteraction( holdInteractable, context );
				return;
			}

			InteractionPrompt = "E: Use";
			ResetHold();
			return;
		}

		if ( TryFindCarryBody( out var body ) )
		{
			var heavy = body.GameObject.GetComponentInParent<HeavyCarryObject>( true, true );
			InteractionPrompt = heavy.IsValid() ? $"E: Grab heavy ({heavy.CarrierCount}/{heavy.RequiredCarriers})" : "E: Pick up";
			ResetHold();
			return;
		}

		ResetHold();
	}

	void UpdateHoldInteraction( IHoldInteractable interactable, InteractionContext context )
	{
		var duration = System.MathF.Max( interactable.HoldDuration, 0.05f );
		InteractionPrompt = interactable.HoldPrompt;

		if ( holdTarget != interactable )
		{
			holdTarget = interactable;
			holdTimer = 0f;
		}

		if ( !Input.Down( PickupAction ) )
		{
			InteractionProgress = 0f;
			return;
		}

		holdTimer += Time.Delta;
		InteractionProgress = System.MathF.Min( holdTimer / duration, 1f );

		if ( InteractionProgress < 1f )
			return;

		interactable.Interact( context );
		ResetHold();
	}

	bool TryFindInteractable( out IInteractable interactable, out InteractionContext context )
	{
		interactable = null;
		context = default;

		var eye = controller.EyeTransform;
		var start = eye.Position;
		var end = start + eye.Rotation.Forward * ReachDistance;

		var trace = Scene.Trace
			.Ray( start, end )
			.IgnoreGameObjectHierarchy( GameObject )
			.UseHitPosition( true )
			.Run();

		if ( !trace.Hit )
			return false;

		var hitObject = trace.Collider?.GameObject ?? trace.GameObject;
		if ( !hitObject.IsValid() )
			return false;

		interactable = hitObject.GetComponentInParent<IInteractable>( true, true );
		if ( interactable is null )
			return false;

		context = new InteractionContext( GameObject, this, trace.HitPosition, trace.Normal );
		return interactable.CanInteract( context );
	}

	bool TryFindCarryBody( out Rigidbody body )
	{
		body = null;

		var eye = controller.EyeTransform;
		var start = eye.Position;
		var end = start + eye.Rotation.Forward * ReachDistance;

		var trace = Scene.Trace
			.Ray( start, end )
			.IgnoreGameObjectHierarchy( GameObject )
			.IgnoreStatic()
			.Run();

		if ( !trace.Hit )
			return false;

		body = trace.Collider?.Rigidbody ?? trace.GameObject?.GetComponentInParent<Rigidbody>( true, true );
		if ( !body.IsValid() || body == GetComponent<Rigidbody>() )
			return false;

		if ( body.GameObject.GetComponentInParent<HealthComponent>( true, true ).IsValid() )
			return false;

		return true;
	}

	void DropHeld( bool throwForward )
	{
		if ( !heldBody.IsValid() )
		{
			heldBody = null;
			return;
		}

		var body = heldBody;
		var heavy = heldHeavy;
		heldBody = null;
		heldHeavy = null;

		if ( heavy.IsValid() )
		{
			heavy.Release( this, body );
		}
		else
		{
			var restoreBody = body.PhysicsBody;
			if ( restoreBody is null )
				return;

			body.Gravity = heldGravity;
			restoreBody.LinearDamping = heldLinearDamping;
			restoreBody.AngularDamping = heldAngularDamping;
			restoreBody.Sleeping = false;
		}

		var throwBody = body.PhysicsBody;
		if ( throwBody is null )
			return;

		if ( throwForward )
			throwBody.Velocity += controller.EyeTransform.Rotation.Forward * ThrowSpeed;
	}

	void ResetHold()
	{
		holdTarget = null;
		holdTimer = 0f;
		InteractionProgress = 0f;
	}

	Vector3 GetHoldPosition()
	{
		var eye = controller.EyeTransform;
		return eye.Position + eye.Rotation.Forward * HoldDistance + Vector3.Up * HoldHeight;
	}
}
