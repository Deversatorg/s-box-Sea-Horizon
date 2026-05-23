namespace Sandbox;

[Title( "Player Musket" )]
[Category( "Gameplay" )]
[Icon( "ads_click" )]
public sealed class PlayerMusket : Component
{
	[Property] public string FireAction { get; set; } = "attack1";
	[Property] public float Damage { get; set; } = 35f;
	[Property] public float Range { get; set; } = 800f;
	[Property] public float FireDelay { get; set; } = 1f;
	[Property] public string ViewModelPath { get; set; } = "models/sea_horizon/musket.vmdl";
	[Property] public Vector3 ViewModelOffset { get; set; } = new( 50f, -14f, -22f );
	[Property] public Vector3 ViewModelAngles { get; set; } = new( 6f, -4f, 2f );
	[Sync] public bool HasMusket { get; private set; }

	PlayerController controller;
	PlayerGrabber grabber;
	HealthComponent health;
	TimeSince timeSinceShot;
	GameObject viewModel;
	ModelRenderer viewRenderer;

	protected override void OnStart()
	{
		controller = GetComponent<PlayerController>();
		grabber = GetComponent<PlayerGrabber>();
		health = GetComponent<HealthComponent>();
		timeSinceShot = FireDelay;
	}

	protected override void OnUpdate()
	{
		if ( IsProxy )
			return;

		UpdateViewModel();

		if ( !HasMusket || !controller.IsValid() || (health.IsValid() && health.IsDead) || IsInputBlocked() )
			return;

		if ( grabber.IsValid() && grabber.IsHoldingSomething )
			return;

		if ( !Input.Pressed( FireAction ) || timeSinceShot < FireDelay )
			return;

		timeSinceShot = 0f;
		var eye = controller.EyeTransform;
		FireShot( eye.Position, eye.Rotation.Forward );
	}

	public void GrantMusket()
	{
		HasMusket = true;
	}

	[Rpc.Host]
	public void FireShot( Vector3 position, Vector3 forward )
	{
		if ( !HasMusket )
			return;

		var trace = Scene.Trace
			.Ray( position, position + forward.Normal * Range )
			.IgnoreGameObjectHierarchy( GameObject )
			.UseHitPosition( true )
			.Run();

		if ( !trace.Hit )
			return;

		var hitObject = trace.Collider?.GameObject ?? trace.GameObject;
		if ( !hitObject.IsValid() )
			return;

		var pirate = hitObject.GetComponentInParent<PirateEnemy>( true, true )
			?? hitObject.GetComponentInChildren<PirateEnemy>( true );
		if ( !pirate.IsValid() )
			return;

		var health = pirate.GetComponent<HealthComponent>();
		health?.TakeDamage( Damage );
	}

	void UpdateViewModel()
	{
		var show = HasMusket
			&& controller.IsValid()
			&& (!health.IsValid() || !health.IsDead)
			&& !(grabber.IsValid() && grabber.IsHoldingSomething)
			&& !IsInputBlocked();

		if ( !show )
		{
			if ( viewModel.IsValid() )
				viewModel.Enabled = false;
			return;
		}

		EnsureViewModel();
		if ( !viewModel.IsValid() )
			return;

		var eye = controller.EyeTransform;
		viewModel.WorldPosition = eye.Position
			+ eye.Rotation.Forward * ViewModelOffset.x
			+ eye.Rotation.Right * ViewModelOffset.y
			+ eye.Rotation.Up * ViewModelOffset.z;
		viewModel.WorldRotation = eye.Rotation
			* Rotation.FromPitch( ViewModelAngles.x )
			* Rotation.FromYaw( ViewModelAngles.y )
			* Rotation.FromRoll( ViewModelAngles.z );
		viewModel.Enabled = true;
	}

	void EnsureViewModel()
	{
		if ( viewModel.IsValid() && viewRenderer.IsValid() )
			return;

		viewModel = new GameObject( true, "Musket View Model" );
		viewRenderer = viewModel.AddComponent<ModelRenderer>();
		viewRenderer.Model = Model.Load( ViewModelPath );
		viewModel.Enabled = false;
	}

	bool IsInputBlocked()
	{
		return Scene.GetAllComponents<PirateRaidController>().Any( x => x.IsValid() && x.IsOfferVisible );
	}
}
