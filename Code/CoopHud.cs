namespace Sandbox;

[Title( "Co-op HUD" )]
[Category( "Gameplay" )]
[Icon( "dashboard" )]
public sealed class CoopHud : Component
{
	[Property] public float ScreenX { get; set; } = 32f;
	[Property] public float ScreenY { get; set; } = 32f;
	[Property] public float LineHeight { get; set; } = 24f;
	[Property] public bool ShowControls { get; set; } = true;

	HealthComponent health;
	CarryInteractor carry;

	protected override void OnStart()
	{
		health = GetComponent<HealthComponent>();
		carry = GetComponent<CarryInteractor>();
	}

	protected override void OnUpdate()
	{
		if ( IsProxy )
			return;

		var y = ScreenY;

		if ( health.IsValid() )
		{
			var status = health.IsDead ? "DOWN" : $"{health.CurrentHealth:0}/{health.MaxHealth:0}";
			DrawLine( ref y, $"Health: {status}", health.IsDead ? Color.Red : Color.White );
		}

		var objective = Scene.GetAllComponents<ObjectiveManager>().FirstOrDefault();
		if ( objective.IsValid() )
		{
			var objectiveText = $"{objective.ObjectiveName}: {objective.CompletedCount}/{objective.RequiredCount}";
			DrawLine( ref y, objectiveText, objective.IsComplete ? Color.Green : Color.White );
		}

		if ( carry.IsValid() && !string.IsNullOrWhiteSpace( carry.InteractionPrompt ) )
		{
			var prompt = carry.InteractionPrompt;
			if ( carry.InteractionProgress > 0f )
				prompt += $" {BuildProgressBar( carry.InteractionProgress )}";

			DrawLine( ref y, prompt, Color.Cyan );
		}

		if ( ShowControls )
			DrawLine( ref y, "WASD move | Shift run | Space jump | E use/pick/drop | G drop | Mouse1 throw | Mouse3 ping", Color.White.WithAlpha( 0.65f ) );
	}

	void DrawLine( ref float y, string text, Color color )
	{
		DebugOverlay.ScreenText( new Vector2( ScreenX, y ), text, 16f, TextFlag.LeftTop, color, 0.05f );
		y += LineHeight;
	}

	string BuildProgressBar( float value )
	{
		var filled = (int)System.MathF.Min( value * 12f, 12f );
		return "[" + new string( '|', filled ).PadRight( 12, '.' ) + "]";
	}
}
