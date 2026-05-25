using Sandbox;

namespace Sandbox;

[Title( "Pickupable Item" )]
[Category( "Gameplay" )]
[Icon( "category" )]
public sealed class PickupableItem : Component, IInteractable, ICarryable
{
    [Property, Description( "Имя предмета для интерфейса" )] 
    public string ItemName { get; set; } = "Item";
    
    [Property, Description( "Тяжелый предмет (сокровище)? Тяжелые берутся в две руки и блокируют инвентарь." )] 
    public bool IsHeavy { get; set; } = false;

    [Property, Description( "If false, this item can only be carried in hands and never stored in inventory." )]
    public bool CanStoreInInventory { get; set; } = true;

    [Property, Description( "If true, inventory pickup hides this object instead of holding it with physics." )]
    public bool StoreHiddenInInventory { get; set; }
    
    [Property, Description( "Ценность для продажи" )] 
    public int Value { get; set; } = 10;
    
    // Co-op & Physics
    [Property, ShowIf( "IsHeavy", true ), Description("Количество игроков для нормальной скорости")] 
    public int RequiredCarriers { get; set; } = 1;
    
    [Property, ShowIf( "IsHeavy", true ), Description("Множитель скорости если тащит один")] 
    public float SpeedMultiplier { get; set; } = 0.5f;

    public bool ShouldUseHandsOnly => IsHeavy || !CanStoreInInventory || HasTreasureMarker();

    public bool CanInteract( InteractionContext context )
    {
        var grabber = context.User.GetComponent<PlayerGrabber>();
        if ( grabber.IsValid() && grabber.IsHoldingSomething )
            return false; // Руки заняты!

        return true;
    }

    public void Interact( InteractionContext context )
    {
        var body = GameObject.GetComponentInParent<Rigidbody>(true, true) ?? GameObject.GetComponentInChildren<Rigidbody>(true);
        if ( !body.IsValid() ) return;

        if ( body.GameObject.Network.Active && !body.GameObject.Network.IsOwner )
            body.GameObject.Network.TakeOwnership();

        var grabber = context.User.GetComponent<PlayerGrabber>();
        var inventory = context.User.GetComponent<PlayerInventory>();
        var interactor = context.User.GetComponent<PlayerInteractor>();

        if ( ShouldUseHandsOnly )
        {
            if ( grabber.IsValid() )
            {
                // Тяжелые предметы не кладутся в инвентарь, они просто берутся в руки
                grabber.Grab( body, true, SpeedMultiplier );
            }
        }
        else
        {
            if ( inventory.IsValid() )
            {
                // Пытаемся положить в инвентарь
                if ( !inventory.TryAddItem( this, body, StoreHiddenInInventory ) )
                    interactor?.ShowFeedback( "INVENTORY FULL", "warning" );
            }
            else
            {
                interactor?.ShowFeedback( "NO INVENTORY AVAILABLE", "danger" );
            }
        }
    }

    bool HasTreasureMarker()
    {
        return GameObject.GetComponentInParent<TreasureItem>( true, true ).IsValid()
            || GameObject.GetComponentInChildren<TreasureItem>( true ).IsValid();
    }
}
