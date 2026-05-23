using Sandbox;

namespace Sandbox;

[Title( "Player Grabber" )]
[Category( "Gameplay" )]
[Icon( "back_hand" )]
public sealed class PlayerGrabber : Component
{
    [Property] public float HoldDistance { get; set; } = 75f;
    [Property] public float HoldHeight { get; set; } = -8f;
    [Property] public float HoldSpring { get; set; } = 12f;
    [Property] public float MaxHoldSpeed { get; set; } = 750f;
    [Property] public float BreakDistance { get; set; } = 260f;
    [Property] public float ThrowSpeed { get; set; } = 700f;

    PlayerController controller;
    
    public Rigidbody HeldBody { get; private set; }
    public bool IsHoldingHeavy { get; private set; }
    public float CurrentSpeedMultiplier { get; private set; } = 1f;

    public bool IsHoldingSomething => HeldBody.IsValid();

    // Сохранение состояния физики для восстановления при дропе
    bool savedPhysics;
    bool savedGravity;
    float savedLinearDamping;
    float savedAngularDamping;

    protected override void OnStart()
    {
        controller = GetComponent<PlayerController>();
    }

    public void Grab( Rigidbody body, bool isHeavy, float speedMultiplier = 1f )
    {
        if ( IsHoldingSomething ) Drop( false );

        HeldBody = body;
        IsHoldingHeavy = isHeavy;
        CurrentSpeedMultiplier = speedMultiplier;

        var physicsBody = body.PhysicsBody;
        if ( physicsBody != null )
        {
            if ( !savedPhysics )
            {
                savedGravity = body.Gravity;
                savedLinearDamping = physicsBody.LinearDamping;
                savedAngularDamping = physicsBody.AngularDamping;
                savedPhysics = true;
            }

            body.Gravity = false;
            physicsBody.LinearDamping = 5f;
            physicsBody.AngularDamping = 8f;
            physicsBody.Sleeping = false;
        }
    }

    public void Drop( bool throwForward )
    {
        if ( !HeldBody.IsValid() ) return;

        var body = HeldBody;
        HeldBody = null;
        IsHoldingHeavy = false;
        CurrentSpeedMultiplier = 1f;

        var physicsBody = body.PhysicsBody;
        if ( physicsBody != null )
        {
            if ( savedPhysics )
            {
                body.Gravity = savedGravity;
                physicsBody.LinearDamping = savedLinearDamping;
                physicsBody.AngularDamping = savedAngularDamping;
                savedPhysics = false;
            }
            
            physicsBody.Sleeping = false;

            if ( throwForward && controller.IsValid() )
            {
                physicsBody.Velocity += controller.EyeTransform.Rotation.Forward * ThrowSpeed;
            }
        }
    }

    protected override void OnFixedUpdate()
    {
        if ( IsProxy ) return;
        if ( !HeldBody.IsValid() ) return;

        var physicsBody = HeldBody.PhysicsBody;
        if ( physicsBody is null )
        {
            HeldBody = null;
            return;
        }

        var targetPosition = GetHoldPosition();
        var toTarget = targetPosition - HeldBody.WorldPosition;
        var distance = toTarget.Length;

        if ( distance > BreakDistance )
        {
            Drop( false );
            return;
        }

        var speed = System.MathF.Min( distance * HoldSpring * CurrentSpeedMultiplier, MaxHoldSpeed * CurrentSpeedMultiplier );
        physicsBody.Velocity = distance > 1f ? toTarget.Normal * speed : Vector3.Zero;
        physicsBody.AngularVelocity *= 0.75f;
        physicsBody.Sleeping = false;
    }

    Vector3 GetHoldPosition()
    {
        if ( !controller.IsValid() ) return WorldPosition;
        var eye = controller.EyeTransform;
        return eye.Position + eye.Rotation.Forward * HoldDistance + Vector3.Up * HoldHeight;
    }
}
