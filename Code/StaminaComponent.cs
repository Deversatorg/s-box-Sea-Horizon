namespace Sandbox;

[Title( "Stamina" )]
[Category( "Gameplay" )]
[Icon( "directions_run" )]
public sealed class StaminaComponent : Component
{
	[Property] public float MaxStamina { get; set; } = 100f;
	[Property] public float CurrentStamina { get; set; } = 100f;
	[Property] public float DrainPerSecond { get; set; } = 24f;
	[Property] public float RegenPerSecond { get; set; } = 18f;
	[Property] public float RegenDelay { get; set; } = 0.7f;
	[Property] public float ExhaustedRecovery { get; set; } = 18f;
	[Property] public string RunAction { get; set; } = "run";

	PlayerController controller;
	float configuredRunSpeed;
	float timeSinceDrain;
	bool exhausted;

	public bool IsSprinting { get; private set; }
	public bool IsExhausted => exhausted;
	public float Fraction => MaxStamina <= 0f ? 0f : CurrentStamina / MaxStamina;

	protected override void OnStart()
	{
		controller = GetComponent<PlayerController>();
		configuredRunSpeed = controller.IsValid() ? controller.RunSpeed : 0f;
		CurrentStamina = System.Math.Clamp( CurrentStamina, 0f, MaxStamina );
	}

	protected override void OnUpdate()
	{
		if ( IsProxy )
			return;

		if ( !controller.IsValid() || !controller.Enabled )
		{
			IsSprinting = false;
			return;
		}

		if ( configuredRunSpeed <= 0f )
			configuredRunSpeed = controller.RunSpeed;

		var wantsRun = Input.Down( RunAction ) || Input.Down( "Run" );
		IsSprinting = wantsRun && !exhausted && CurrentStamina > 0f;

		if ( IsSprinting )
		{
			CurrentStamina = System.MathF.Max( CurrentStamina - DrainPerSecond * Time.Delta, 0f );
			timeSinceDrain = 0f;

			if ( CurrentStamina <= 0f )
				exhausted = true;
		}
		else
		{
			timeSinceDrain += Time.Delta;

			if ( timeSinceDrain >= RegenDelay )
				CurrentStamina = System.MathF.Min( CurrentStamina + RegenPerSecond * Time.Delta, MaxStamina );

			if ( exhausted && CurrentStamina >= ExhaustedRecovery )
				exhausted = false;
		}

		controller.RunSpeed = exhausted ? controller.WalkSpeed : configuredRunSpeed;
	}
}
