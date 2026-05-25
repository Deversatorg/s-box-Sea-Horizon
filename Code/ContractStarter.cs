namespace Sandbox;

[Title( "Contract Starter" )]
[Category( "Gameplay" )]
[Icon( "assignment" )]
public sealed class ContractStarter : Component, IInteractable
{
	[Property] public ObjectiveManager Manager { get; set; }
	[Property] public string ObjectiveName { get; set; } = "Scrap recovery";
	[Property] public string ContractTarget { get; set; } = "Bring 3 scrap to base";
	[Property] public int RequiredCount { get; set; } = 3;
	[Property] public int TargetValue { get; set; } = 75;
	[Property] public float ContractDuration { get; set; } = 180f;
	[Property] public bool RestartAllowed { get; set; }

	public bool CanInteract( InteractionContext context )
	{
		var manager = FindManager();
		var raidActive = Scene.GetAllComponents<PirateRaidController>().Any( x => x.IsValid() && x.IsRaidActive );
		if ( raidActive )
			return false;

		return !manager.IsValid()
			|| manager.ContractState is ObjectiveContractState.Ready or ObjectiveContractState.Complete or ObjectiveContractState.Failed;
	}

	public void Interact( InteractionContext context )
	{
		FindManager()?.StartConfiguredContract( ContractTarget, RequiredCount, TargetValue, ContractDuration, ObjectiveName );
	}

	ObjectiveManager FindManager()
	{
		if ( Manager.IsValid() )
			return Manager;

		Manager = Scene.GetAllComponents<ObjectiveManager>().FirstOrDefault();
		return Manager;
	}
}
