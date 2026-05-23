using Sandbox;

namespace Sandbox;

[Title( "Player Interactor" )]
[Category( "Gameplay" )]
[Icon( "visibility" )]
public sealed class PlayerInteractor : Component
{
    [Property] public float ReachDistance { get; set; } = 120f;
    [Property] public string PickupAction { get; set; } = "use";
    [Property] public string DropAction { get; set; } = "drop";
    [Property] public string ThrowAction { get; set; } = "attack1";

    public string InteractionPrompt { get; private set; }
    public float InteractionProgress { get; private set; }
    public string FeedbackText { get; private set; }
    public string FeedbackClass { get; private set; } = "neutral";
    public float FeedbackRemaining { get; private set; }

    PlayerController controller;
    PlayerGrabber grabber;
    PlayerInventory inventory;
    
    IHoldInteractable holdTarget;
    float holdTimer;

    protected override void OnStart()
    {
        controller = GetComponent<PlayerController>();
        grabber = GetComponent<PlayerGrabber>();
        inventory = GetComponent<PlayerInventory>();
    }

    protected override void OnUpdate()
    {
        if ( IsProxy ) return;

        InteractionPrompt = null;
        InteractionProgress = 0f;
        UpdateFeedback();

        if ( IsBlockedBySpecialEventOffer() )
        {
            ResetHold();
            return;
        }

        if ( !controller.IsValid() ) return;

        UpdateInteractionState();

        // Drop / Throw
        if ( grabber.IsValid() && grabber.IsHoldingSomething )
        {
            if ( Input.Pressed( DropAction ) || Input.Pressed( PickupAction ) )
            {
                var shouldDropInventoryItem = inventory.IsValid() && inventory.IsActiveBody( grabber.HeldBody );
                grabber.Drop( false );
                if ( shouldDropInventoryItem )
                    inventory.DropActiveItem();
            }
            else if ( Input.Pressed( ThrowAction ) )
            {
                var shouldDropInventoryItem = inventory.IsValid() && inventory.IsActiveBody( grabber.HeldBody );
                grabber.Drop( true );
                if ( shouldDropInventoryItem )
                    inventory.DropActiveItem();
            }
        }
        else
        {
            // Если ничего не держим, обрабатываем обычный клик 'E'
            if ( Input.Pressed( PickupAction ) )
            {
                TryInteract( false );
            }
        }
    }

    void UpdateInteractionState()
    {
        if ( grabber.IsValid() && grabber.IsHoldingSomething )
        {
            if ( grabber.IsHoldingHeavy )
                InteractionPrompt = "E/G: Drop  |  Mouse1: Throw  |  Hands full (Heavy)";
            else
                InteractionPrompt = "G: Drop  |  Mouse1: Throw  |  Hands full";
                
            ResetHold();
            return;
        }

        if ( TryFindInteractable( out var interactable, out var context ) )
        {
            if ( interactable is IHoldInteractable holdInteractable )
            {
                UpdateHoldInteraction( holdInteractable, context );
                return;
            }

            // Кастомные подсказки для предметов
            if ( interactable is PickupableItem item )
            {
                if ( item.ShouldUseHandsOnly )
                    InteractionPrompt = $"E: Grab {item.ItemName}";
                else
                    InteractionPrompt = $"E: Pick up {item.ItemName}";
            }
            else if ( interactable is ContractStarter )
            {
                InteractionPrompt = "E: Start contract";
            }
            else if ( interactable is SpecialEventStarter eventStarter )
            {
                InteractionPrompt = eventStarter.GetPromptText();
            }
            else if ( interactable is LootChest chest )
            {
                InteractionPrompt = chest.Opened ? "E: Check chest" : "E: Open chest";
            }
            else
            {
                InteractionPrompt = "E: Use";
            }
            
            ResetHold();
            return;
        }

        ResetHold();
    }

    bool TryInteract( bool allowHoldInteractables )
    {
        if ( !TryFindInteractable( out var interactable, out var context ) )
            return false;

        if ( !allowHoldInteractables && interactable is IHoldInteractable )
            return false;

        interactable.Interact( context );
        return true;
    }

    public void ShowFeedback( string text, string feedbackClass = "neutral", float duration = 2.2f )
    {
        if ( string.IsNullOrWhiteSpace( text ) )
            return;

        FeedbackText = text;
        FeedbackClass = string.IsNullOrWhiteSpace( feedbackClass ) ? "neutral" : feedbackClass;
        FeedbackRemaining = System.MathF.Max( duration, 0.1f );
    }

    void UpdateFeedback()
    {
        if ( FeedbackRemaining <= 0f )
            return;

        FeedbackRemaining = System.MathF.Max( FeedbackRemaining - Time.Delta, 0f );
        if ( FeedbackRemaining <= 0f )
        {
            FeedbackText = string.Empty;
            FeedbackClass = "neutral";
        }
    }

    void UpdateHoldInteraction( IHoldInteractable interactable, InteractionContext context )
    {
        var duration = System.MathF.Max( interactable.HoldDuration, 0.05f );
        InteractionPrompt = interactable.HoldPrompt;

        if ( holdTarget != interactable )
        {
            holdTarget = interactable;
            holdTimer = 0f;
        }

        if ( !Input.Down( PickupAction ) )
        {
            InteractionProgress = 0f;
            return;
        }

        holdTimer += Time.Delta;
        InteractionProgress = System.MathF.Min( holdTimer / duration, 1f );

        if ( InteractionProgress < 1f )
            return;

        interactable.Interact( context );
        ResetHold();
    }

    bool TryFindInteractable( out IInteractable interactable, out InteractionContext context )
    {
        interactable = null;
        context = default;

        var eye = controller.EyeTransform;
        var start = eye.Position;
        var end = start + eye.Rotation.Forward * ReachDistance;

        var trace = Scene.Trace
            .Ray( start, end )
            .IgnoreGameObjectHierarchy( GameObject )
            .UseHitPosition( true )
            .Run();

        if ( !trace.Hit )
            return false;

        var hitObject = trace.Collider?.GameObject ?? trace.GameObject;
        if ( !hitObject.IsValid() )
            return false;

        context = new InteractionContext( GameObject, this, trace.HitPosition, trace.Normal );

        foreach ( var candidate in hitObject.GetComponentsInParent<IInteractable>( true, true )
            .Concat( hitObject.GetComponentsInChildren<IInteractable>( true ) ) )
        {
            if ( candidate is null || !candidate.CanInteract( context ) )
                continue;

            interactable = candidate;
            return true;
        }

        return false;
    }

    void ResetHold()
    {
        holdTarget = null;
        holdTimer = 0f;
        InteractionProgress = 0f;
    }

    bool IsBlockedBySpecialEventOffer()
    {
        return Scene.GetAllComponents<PirateRaidController>().Any( x => x.IsValid() && x.IsOfferVisible );
    }
}
