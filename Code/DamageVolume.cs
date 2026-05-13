namespace Sandbox;

[Title( "Damage Volume" )]
[Category( "Gameplay" )]
[Icon( "warning" )]
public sealed class DamageVolume : Component, Component.ITriggerListener
{
	[Property] public float DamagePerSecond { get; set; } = 25f;
	[Property] public bool DamagePlayersOnly { get; set; } = true;

	readonly HashSet<HealthComponent> touching = new();

	protected override void OnFixedUpdate()
	{
		foreach ( var health in touching.ToArray() )
		{
			if ( !health.IsValid() || !health.GameObject.Active )
			{
				touching.Remove( health );
				continue;
			}

			health.TakeDamage( DamagePerSecond * Time.Delta );
		}
	}

	public void OnTriggerEnter( Collider other )
	{
		var health = other.GameObject.GetComponentInParent<HealthComponent>( true, true );
		if ( !health.IsValid() )
			return;

		if ( DamagePlayersOnly && !health.GameObject.Tags.Has( "player" ) )
			return;

		touching.Add( health );
	}

	public void OnTriggerExit( Collider other )
	{
		var health = other.GameObject.GetComponentInParent<HealthComponent>( true, true );
		if ( health.IsValid() )
			touching.Remove( health );
	}
}
