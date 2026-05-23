using System;

namespace Sandbox;

public sealed class InventorySlotData
{
	public PickupableItem Item;
	public Rigidbody Body;
	public bool StoredHidden;
}

[Title( "Player Inventory" )]
[Category( "Gameplay" )]
[Icon( "inventory" )]
public sealed class PlayerInventory : Component
{
	[Property] public int InventorySize { get; set; } = 3;
	[Property] public bool ShowActiveItemPreview { get; set; } = true;
	[Property] public Vector3 PreviewOffset { get; set; } = new( 82f, 24f, -28f );
	[Property] public Vector3 PreviewRotationOffset { get; set; } = new( 12f, 34f, -18f );
	[Property] public float PreviewScale { get; set; } = 0.22f;
	[Property] public float PreviewMaxWorldSize { get; set; } = 18f;

	public InventorySlotData[] Slots { get; private set; }
	public int ActiveSlotIndex { get; private set; }

	PlayerGrabber grabber;
	PlayerController controller;
	GameObject previewObject;
	ModelRenderer previewRenderer;
	Model previewModel;

	protected override void OnStart()
	{
		grabber = GetComponent<PlayerGrabber>();
		controller = GetComponent<PlayerController>();
		Slots = new InventorySlotData[InventorySize];

		for ( var i = 0; i < InventorySize; i++ )
		{
			Slots[i] = new InventorySlotData();
		}
	}

	protected override void OnUpdate()
	{
		if ( IsProxy )
			return;

		if ( grabber.IsValid() && grabber.IsHoldingSomething && !IsActiveBody( grabber.HeldBody ) )
		{
			SetPreviewEnabled( false );
			return;
		}

		if ( Input.MouseWheel.y != 0 )
		{
			var direction = Math.Sign( Input.MouseWheel.y );
			var nextSlot = ActiveSlotIndex - direction;

			if ( nextSlot < 0 )
				nextSlot = InventorySize - 1;

			if ( nextSlot >= InventorySize )
				nextSlot = 0;

			SwitchSlot( nextSlot );
		}

		if ( Input.Pressed( "Slot1" ) )
			SwitchSlot( 0 );

		if ( Input.Pressed( "Slot2" ) && InventorySize > 1 )
			SwitchSlot( 1 );

		if ( Input.Pressed( "Slot3" ) && InventorySize > 2 )
			SwitchSlot( 2 );

		UpdateActiveItemPreview();
	}

	public bool TryAddItem( PickupableItem item, Rigidbody body, bool storeHidden = false )
	{
		if ( Slots is null || item is null || item.ShouldUseHandsOnly )
			return false;

		if ( !grabber.IsValid() )
			return false;

		var slotIndex = storeHidden ? FindFirstEmptySlot() : ActiveSlotIndex;
		if ( slotIndex < 0 )
			return false;

		var slot = Slots[slotIndex];
		if ( slot.Item.IsValid() || (!storeHidden && grabber.IsHoldingSomething) )
			return false;

		slot.Item = item;
		slot.Body = body;
		slot.StoredHidden = storeHidden;

		if ( storeHidden )
		{
			StopBody( body );
			body.GameObject.Enabled = false;
			return true;
		}

		grabber.Grab( body, false );
		return true;
	}

	int FindFirstEmptySlot()
	{
		if ( Slots is null )
			return -1;

		for ( var i = 0; i < Slots.Length; i++ )
		{
			if ( !Slots[i].Item.IsValid() )
				return i;
		}

		return -1;
	}

	public void SwitchSlot( int index )
	{
		if ( index < 0 || index >= InventorySize || index == ActiveSlotIndex )
			return;

		var currentSlot = Slots[ActiveSlotIndex];
		if ( currentSlot.Item.IsValid() && currentSlot.Body.IsValid() )
		{
			if ( !currentSlot.StoredHidden && grabber.IsValid() && IsActiveBody( currentSlot.Body ) )
				grabber.Drop( false );

			currentSlot.Body.GameObject.Enabled = false;
		}

		ActiveSlotIndex = index;

		var newSlot = Slots[ActiveSlotIndex];
		if ( newSlot.Item.IsValid() && newSlot.Body.IsValid() )
		{
			if ( newSlot.StoredHidden )
			{
				newSlot.Body.GameObject.Enabled = false;
				return;
			}

			newSlot.Body.GameObject.Enabled = true;

			if ( grabber.IsValid() )
			{
				newSlot.Body.WorldPosition = grabber.WorldPosition;
				grabber.Grab( newSlot.Body, false );
			}

			return;
		}

		if ( grabber.IsValid() && !grabber.IsHoldingHeavy )
			grabber.Drop( false );
	}

	public bool IsActiveBody( Rigidbody body )
	{
		if ( !body.IsValid() || Slots is null || ActiveSlotIndex < 0 || ActiveSlotIndex >= Slots.Length )
			return false;

		var slot = Slots[ActiveSlotIndex];
		return !slot.StoredHidden && slot.Body == body;
	}

	public void DropActiveItem()
	{
		ClearSlot( ActiveSlotIndex, false );
	}

	public bool RemoveItem( PickupableItem item, Rigidbody body )
	{
		if ( Slots is null )
			return false;

		for ( var i = 0; i < Slots.Length; i++ )
		{
			var slot = Slots[i];
			var itemMatches = item.IsValid() && slot.Item == item;
			var bodyMatches = body.IsValid() && slot.Body == body;

			if ( !itemMatches && !bodyMatches )
				continue;

			ClearSlot( i, true );
			return true;
		}

		return false;
	}

	public bool TryRemoveFirstMatchingItem( string requiredTag, out int value )
	{
		value = 0;

		if ( Slots is null )
			return false;

		for ( var i = 0; i < Slots.Length; i++ )
		{
			var slot = Slots[i];
			if ( !slot.Item.IsValid() )
				continue;

			if ( !string.IsNullOrWhiteSpace( requiredTag ) && !slot.Item.GameObject.Tags.Has( requiredTag ) )
				continue;

			value = Math.Max( slot.Item.Value, 0 );
			ClearSlot( i, true );
			return true;
		}

		return false;
	}

	void ClearSlot( int index, bool disableObject )
	{
		if ( Slots is null || index < 0 || index >= Slots.Length )
			return;

		var slot = Slots[index];
		var body = slot.Body;

		if ( grabber.IsValid() && body.IsValid() && grabber.HeldBody == body )
			grabber.Drop( false );

		if ( disableObject )
		{
			if ( body.IsValid() )
			{
				StopBody( body );
				body.GameObject.Enabled = false;
			}
			else if ( slot.Item.IsValid() )
			{
				slot.Item.GameObject.Enabled = false;
			}
		}

		slot.Item = null;
		slot.Body = null;
		slot.StoredHidden = false;
	}

	void UpdateActiveItemPreview()
	{
		if ( !ShowActiveItemPreview || Slots is null || !controller.IsValid() )
		{
			SetPreviewEnabled( false );
			return;
		}

		if ( ActiveSlotIndex < 0 || ActiveSlotIndex >= Slots.Length )
		{
			SetPreviewEnabled( false );
			return;
		}

		var slot = Slots[ActiveSlotIndex];
		if ( !slot.Item.IsValid() || !slot.StoredHidden )
		{
			SetPreviewEnabled( false );
			return;
		}

		var model = GetPreviewModel( slot.Item );
		if ( model is null )
		{
			SetPreviewEnabled( false );
			return;
		}

		EnsurePreviewObject();

		if ( !previewRenderer.IsValid() )
			return;

		if ( previewModel != model )
		{
			previewModel = model;
			previewRenderer.Model = previewModel;
		}

		var eye = controller.EyeTransform;
		previewObject.WorldPosition = eye.Position
			+ eye.Rotation.Forward * PreviewOffset.x
			+ eye.Rotation.Right * PreviewOffset.y
			+ eye.Rotation.Up * PreviewOffset.z;
		previewObject.WorldRotation = eye.Rotation
			* Rotation.FromPitch( PreviewRotationOffset.x )
			* Rotation.FromYaw( PreviewRotationOffset.y )
			* Rotation.FromRoll( PreviewRotationOffset.z );
		var previewScale = GetPreviewScale( model );
		previewObject.WorldScale = new Vector3( previewScale, previewScale, previewScale );
		SetPreviewEnabled( true );
	}

	Model GetPreviewModel( PickupableItem item )
	{
		if ( !item.IsValid() )
			return null;

		var modelRenderer = item.GameObject.GetComponentInChildren<ModelRenderer>( true );
		if ( modelRenderer.IsValid() && modelRenderer.Model is not null )
			return modelRenderer.Model;

		var skinnedRenderer = item.GameObject.GetComponentInChildren<SkinnedModelRenderer>( true );
		if ( skinnedRenderer.IsValid() && skinnedRenderer.Model is not null )
			return skinnedRenderer.Model;

		return null;
	}

	float GetPreviewScale( Model model )
	{
		var scale = MathF.Max( PreviewScale, 0.01f );
		if ( model is null || PreviewMaxWorldSize <= 0f )
			return scale;

		var boundsSize = model.RenderBounds.Size;
		var maxSize = MathF.Max( boundsSize.x, MathF.Max( boundsSize.y, boundsSize.z ) );
		if ( maxSize <= 0.01f )
			return scale;

		return MathF.Min( scale, PreviewMaxWorldSize / maxSize );
	}

	void EnsurePreviewObject()
	{
		if ( previewObject.IsValid() && previewRenderer.IsValid() )
			return;

		previewObject = new GameObject( true, "Inventory Active Item Preview" );
		previewRenderer = previewObject.AddComponent<ModelRenderer>();
		previewObject.Enabled = false;
	}

	void SetPreviewEnabled( bool enabled )
	{
		if ( previewObject.IsValid() )
			previewObject.Enabled = enabled;
	}

	static void StopBody( Rigidbody body )
	{
		if ( !body.IsValid() )
			return;

		var physicsBody = body.PhysicsBody;
		if ( physicsBody is null )
			return;

		physicsBody.Velocity = Vector3.Zero;
		physicsBody.AngularVelocity = Vector3.Zero;
		physicsBody.Sleeping = true;
	}
}
