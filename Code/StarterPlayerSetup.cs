namespace Sandbox;

public sealed class StarterPlayerSetup : Component
{
	[Property] public float WalkSpeed { get; set; } = 150f;
	[Property] public float RunSpeed { get; set; } = 320f;
	[Property] public float DuckSpeed { get; set; } = 80f;
	[Property] public float JumpSpeed { get; set; } = 300f;
	[Property] public bool ThirdPerson { get; set; } = true;
	[Property] public Vector3 CameraOffset { get; set; } = new Vector3( 220f, 0f, 18f );

	protected override void OnStart()
	{
		var controller = GetComponent<PlayerController>();
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
		controller.CameraOffset = CameraOffset;
		controller.RunByDefault = false;

		controller.AltMoveButton = "run";
		controller.ToggleCameraModeButton = "view";
		controller.UseButton = "use";
	}
}
