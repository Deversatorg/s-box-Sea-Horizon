namespace Sandbox;

[Title( "Ping Tool" )]
[Category( "Gameplay" )]
[Icon( "location_on" )]
public sealed class PingTool : Component
{
	[Property] public string PingAction { get; set; } = "ping";
	[Property] public float PingDistance { get; set; } = 2000f;
	[Property] public float PingDuration { get; set; } = 4f;
	[Property] public float PingRadius { get; set; } = 10f;
	[Property] public Color PingColor { get; set; } = Color.Cyan;

	PlayerController controller;
	Vector3 pingPosition;
	RealTimeSince timeSincePing;
	bool hasPing;

	protected override void OnStart()
	{
		controller = GetComponent<PlayerController>();
	}

	protected override void OnUpdate()
	{
		if ( IsProxy )
			return;

		if ( !controller.IsValid() )
			return;

		if ( Input.Pressed( PingAction ) )
			PlacePing();

		DrawPing();
	}

	void PlacePing()
	{
		var eye = controller.EyeTransform;
		var start = eye.Position;
		var end = start + eye.Rotation.Forward * PingDistance;

		var trace = Scene.Trace
			.Ray( start, end )
			.IgnoreGameObjectHierarchy( GameObject )
			.UseHitPosition( true )
			.Run();

		pingPosition = trace.Hit ? trace.HitPosition : end;
		timeSincePing = 0;
		hasPing = true;
	}

	void DrawPing()
	{
		if ( !hasPing )
			return;

		if ( timeSincePing > PingDuration )
		{
			hasPing = false;
			return;
		}

		var sphere = new Sphere { Center = pingPosition, Radius = PingRadius };
		DebugOverlay.Sphere( sphere, PingColor, 0.05f, default, false );
		DebugOverlay.Text( pingPosition + Vector3.Up * 24f, "PING", 14f, TextFlag.Center, PingColor, 0.05f, false );
	}
}
