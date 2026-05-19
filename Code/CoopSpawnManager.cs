using System;
using System.Threading.Tasks;

namespace Sandbox;

[Title( "Co-op Spawn Manager" )]
[Category( "Networking" )]
[Icon( "groups" )]
public sealed class CoopSpawnManager : Component, Component.INetworkListener
{
	[Property] public bool StartServer { get; set; } = true;
	[Property] public GameObject PlayerTemplate { get; set; }
	[Property] public List<GameObject> SpawnPoints { get; set; } = new();
	[Property] public bool HideTemplateInGame { get; set; } = true;

	protected override async Task OnLoad()
	{
		if ( Scene.IsEditor )
			return;

		if ( HideTemplateInGame && PlayerTemplate.IsValid() )
			PlayerTemplate.Enabled = false;

		if ( StartServer && !Networking.IsActive )
		{
			LoadingScreen.Title = "Creating Lobby";
			await Task.DelayRealtimeSeconds( 0.1f );
			Networking.CreateLobby( new() );
		}
	}

	public void OnActive( Connection channel )
	{
		if ( !PlayerTemplate.IsValid() )
			return;

		var player = PlayerTemplate.Clone( FindSpawnLocation().WithScale( 1f ), name: $"Player - {channel.DisplayName}" );
		player.Enabled = true;
		player.NetworkSpawn( channel );
	}

	Transform FindSpawnLocation()
	{
		if ( SpawnPoints is not null && SpawnPoints.Count > 0 )
			return Random.Shared.FromList( SpawnPoints, default ).WorldTransform;

		var spawnPoints = Scene.GetAllComponents<CoopSpawnPoint>().ToArray();
		if ( spawnPoints.Length > 0 )
			return Random.Shared.FromArray( spawnPoints ).WorldTransform;

		return WorldTransform;
	}
}

[Title( "Co-op Spawn Point" )]
[Category( "Networking" )]
[Icon( "person_pin_circle" )]
public sealed class CoopSpawnPoint : Component
{
}
