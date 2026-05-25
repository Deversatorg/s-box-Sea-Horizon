namespace Sandbox;

public sealed class StarterPlayerSetup : Component
{
	[Property] public float WalkSpeed { get; set; } = 150f;
	[Property] public float RunSpeed { get; set; } = 320f;
	[Property] public float DuckSpeed { get; set; } = 80f;
	[Property] public float JumpSpeed { get; set; } = 300f;
	[Property] public bool ThirdPerson { get; set; }
	[Property] public bool HideBodyInFirstPerson { get; set; }
	[Property] public bool HideHeadInFirstPerson { get; set; } = true;
	[Property] public bool UseExternalFirstPersonCamera { get; set; } = true;
	[Property] public float FirstPersonCameraForwardOffset { get; set; } = 14f;
	[Property] public float FirstPersonCameraUpOffset { get; set; } = 1.5f;
	[Property] public float FirstPersonCameraRightOffset { get; set; }
	[Property] public Vector3 CameraOffset { get; set; } = Vector3.Zero;

	PlayerController controller;
	SkinnedModelRenderer bodyRenderer;

	protected override void OnStart()
	{
		controller = GetComponent<PlayerController>();
		if ( controller is null )
			return;

		if ( IsProxy )
		{
			controller.UseInputControls = false;
			controller.UseLookControls = false;
			controller.UseCameraControls = false;
			return;
		}

		controller.UseInputControls = true;
		controller.UseLookControls = true;
		controller.UseCameraControls = true;
		controller.UseAnimatorControls = true;

		controller.WalkSpeed = WalkSpeed;
		controller.RunSpeed = RunSpeed;
		controller.DuckedSpeed = DuckSpeed;
		controller.JumpSpeed = JumpSpeed;
		controller.ThirdPerson = ThirdPerson;
		controller.HideBodyInFirstPerson = HideBodyInFirstPerson;
		controller.CameraOffset = CameraOffset;
		controller.RunByDefault = false;

		controller.AltMoveButton = "run";
		controller.ToggleCameraModeButton = "view";
		controller.UseButton = "use";

		bodyRenderer = GetComponentInChildren<SkinnedModelRenderer>( true, true );
		ApplyFirstPersonBodySettings();
	}

	protected override void OnUpdate()
	{
		if ( IsProxy )
			return;

		ApplyFirstPersonBodySettings();
	}

	protected override void OnPreRender()
	{
		if ( IsProxy )
			return;

		UpdateExternalFirstPersonCamera();
	}

	void ApplyFirstPersonBodySettings()
	{
		if ( !controller.IsValid() )
			return;

		if ( !bodyRenderer.IsValid() )
			bodyRenderer = GetComponentInChildren<SkinnedModelRenderer>( true, true );

		if ( !bodyRenderer.IsValid() )
			return;

		var firstPersonBodyVisible = !controller.ThirdPerson && !controller.HideBodyInFirstPerson;
		var shouldHideHead = HideHeadInFirstPerson && firstPersonBodyVisible;

		bodyRenderer.SetBodyGroup( "Head", shouldHideHead ? 1 : 0 );
		bodyRenderer.SetBodyGroup( "Chest", 0 );
		bodyRenderer.SetBodyGroup( "Hands", 0 );
		bodyRenderer.SetBodyGroup( "Legs", 0 );
		bodyRenderer.SetBodyGroup( "Feet", 0 );
	}

	void UpdateExternalFirstPersonCamera()
	{
		if ( !UseExternalFirstPersonCamera || !controller.IsValid() || controller.ThirdPerson )
			return;

		var camera = Scene.Camera;
		if ( !camera.IsValid() )
			return;

		var eye = controller.EyeTransform;
		camera.WorldPosition = eye.Position
			+ eye.Rotation.Forward * FirstPersonCameraForwardOffset
			+ eye.Rotation.Right * FirstPersonCameraRightOffset
			+ eye.Rotation.Up * FirstPersonCameraUpOffset;
		camera.WorldRotation = eye.Rotation;
	}
}
