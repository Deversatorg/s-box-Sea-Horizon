namespace Sandbox;

[Title( "Arcade Boat Controller" )]
[Category( "Sea Horizon" )]
[Icon( "directions_boat" )]
public sealed class ArcadeBoatController : Component, IInteractable
{
	[Property] public float Acceleration { get; set; } = 420f;
	[Property] public float ReverseAcceleration { get; set; } = 220f;
	[Property] public float TurnSpeed { get; set; } = 1.8f;
	[Property] public float MaxSpeed { get; set; } = 360f;
	[Property] public float WaterDrag { get; set; } = 0.85f;
	[Property] public float BrakeDrag { get; set; } = 2.5f;
	[Property] public Vector3 DriverLocalOffset { get; set; } = new Vector3( -28f, 0f, 34f );
	[Property] public Vector3 ExitLocalOffset { get; set; } = new Vector3( -80f, 54f, 30f );
	[Property] public string ExitAction { get; set; } = "use";

	Rigidbody body;
	GameObject driver;
	PlayerController driverController;
	Rigidbody driverBody;
	bool oldUseInputControls;
	bool oldUseAnimatorControls;
	bool oldDriverGravity;
	bool oldDriverMotion;
	RealTimeSince timeSinceEntered;

	protected override void OnStart()
	{
		body = GetComponent<Rigidbody>();

		if ( body?.PhysicsBody is { } physicsBody )
		{
			body.Gravity = false;
			physicsBody.LinearDamping = WaterDrag;
			physicsBody.AngularDamping = 4f;
		}
	}

	protected override void OnUpdate()
	{
		if ( !driver.IsValid() )
			return;

		UpdateDriverTransform();

		if ( driver.IsProxy )
			return;

		DebugOverlay.ScreenText( new Vector2( 32f, 140f ), "Boat: W/S throttle | A/D steer | E exit", 16f, TextFlag.LeftTop, Color.Cyan, 0.05f );

		if ( timeSinceEntered > 0.25f && Input.Pressed( ExitAction ) )
			ExitDriver();
	}

	protected override void OnFixedUpdate()
	{
		if ( IsProxy || !driver.IsValid() || driver.IsProxy )
			return;

		if ( body?.PhysicsBody is not { } physicsBody )
			return;

		var throttle = 0f;
		if ( Input.Down( "Forward" ) )
			throttle += 1f;
		if ( Input.Down( "Backward" ) )
			throttle -= 1f;

		var steer = 0f;
		if ( Input.Down( "Left" ) )
			steer += 1f;
		if ( Input.Down( "Right" ) )
			steer -= 1f;

		var acceleration = throttle >= 0f ? Acceleration : ReverseAcceleration;
		physicsBody.Velocity += WorldRotation.Forward * throttle * acceleration * Time.Delta;

		var flatVelocity = physicsBody.Velocity.WithZ( 0f );
		if ( flatVelocity.Length > MaxSpeed )
		{
			flatVelocity = flatVelocity.Normal * MaxSpeed;
			physicsBody.Velocity = flatVelocity.WithZ( physicsBody.Velocity.z );
		}

		var steeringPower = flatVelocity.Length / MaxSpeed;
		physicsBody.AngularVelocity = Vector3.Up * steer * TurnSpeed * steeringPower;
		physicsBody.LinearDamping = Input.Down( "Duck" ) ? BrakeDrag : WaterDrag;
		physicsBody.Sleeping = false;

		UpdateDriverTransform();
	}

	protected override void OnDestroy()
	{
		ExitDriver();
	}

	public bool CanInteract( InteractionContext context )
	{
		return !driver.IsValid() || driver == context.User;
	}

	public void Interact( InteractionContext context )
	{
		if ( driver == context.User )
		{
			ExitDriver();
			return;
		}

		EnterDriver( context.User );
	}

	void EnterDriver( GameObject player )
	{
		if ( !player.IsValid() )
			return;

		if ( driver.IsValid() && driver != player )
			return;

		if ( GameObject.Network.Active && !GameObject.Network.IsOwner )
			GameObject.Network.TakeOwnership();

		driver = player;
		driverController = player.GetComponent<PlayerController>();
		driverBody = player.GetComponent<Rigidbody>();
		timeSinceEntered = 0f;

		if ( driverController.IsValid() )
		{
			oldUseInputControls = driverController.UseInputControls;
			oldUseAnimatorControls = driverController.UseAnimatorControls;
			driverController.UseInputControls = false;
			driverController.UseAnimatorControls = false;
		}

		if ( driverBody?.PhysicsBody is { } physicsBody )
		{
			oldDriverGravity = driverBody.Gravity;
			oldDriverMotion = physicsBody.MotionEnabled;
			driverBody.Gravity = false;
			physicsBody.MotionEnabled = false;
			physicsBody.Velocity = Vector3.Zero;
			physicsBody.AngularVelocity = Vector3.Zero;
		}

		UpdateDriverTransform();
	}

	void ExitDriver()
	{
		if ( !driver.IsValid() )
		{
			driver = null;
			driverController = null;
			driverBody = null;
			return;
		}

		if ( driverController.IsValid() )
		{
			driverController.UseInputControls = oldUseInputControls;
			driverController.UseAnimatorControls = oldUseAnimatorControls;
		}

		if ( driverBody?.PhysicsBody is { } physicsBody )
		{
			driverBody.Gravity = oldDriverGravity;
			physicsBody.MotionEnabled = oldDriverMotion;
			physicsBody.Sleeping = false;
		}

		driver.WorldPosition = LocalToWorld( ExitLocalOffset );
		driver = null;
		driverController = null;
		driverBody = null;
	}

	void UpdateDriverTransform()
	{
		if ( !driver.IsValid() )
			return;

		driver.WorldPosition = LocalToWorld( DriverLocalOffset );
		driver.WorldRotation = WorldRotation;
	}

	Vector3 LocalToWorld( Vector3 localOffset )
	{
		return WorldPosition
			+ WorldRotation.Forward * localOffset.x
			+ WorldRotation.Right * localOffset.y
			+ Vector3.Up * localOffset.z;
	}
}
