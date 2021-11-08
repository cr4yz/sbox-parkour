using Sandbox;
using System;

namespace Facepunch.Parkour
{
	partial class ParkourPlayer : Player
	{
		public override void Respawn()
		{
			SetModel( "models/citizen/citizen.vmdl" );

			Controller = new ParkourController();
			Animator = new StandardPlayerAnimator();
			Camera = new ParkourCamera();

			EnableAllCollisions = true;
			EnableDrawing = true;
			EnableHideInFirstPerson = true;
			EnableShadowInFirstPerson = true;
			LagCompensation = false;

			base.Respawn();
		}

		public override void Simulate( Client cl )
		{
			base.Simulate( cl );

			SimulateActiveChild( cl, ActiveChild );
		}

		public override void OnKilled()
		{
			base.OnKilled();

			EnableDrawing = false;
			EnableAllCollisions = false;
		}

		public override void PostCameraSetup( ref CameraSetup setup )
		{
			base.PostCameraSetup( ref setup );

			AddCameraEffects( ref setup );
		}

		float walkBob = 0;
		float lean = 0;
		float fov = 0;

		private void AddCameraEffects( ref CameraSetup setup )
		{
			var controller = Controller as ParkourController;
			var wishSpd = controller.GetWishSpeed();
			var bobSpeed = controller.Duck.IsActive ? 10f : 25f;
			if ( controller.Duck.Sliding ) bobSpeed = 2;

			var bobSpeedAlpha = Velocity.Length.LerpInverse( 0, wishSpd );
			var forwardspeed = Velocity.Normal.Dot( setup.Rotation.Forward );

			var left = setup.Rotation.Left;
			var up = setup.Rotation.Up;

			if ( GroundEntity != null )
			{
				walkBob += Time.Delta * bobSpeed * bobSpeedAlpha;
			}

			setup.Position += up * MathF.Sin( walkBob ) * bobSpeedAlpha * 3;
			setup.Position += left * MathF.Sin( walkBob * 0.6f ) * bobSpeedAlpha * 2;

			// Camera lean
			lean = lean.LerpTo( Velocity.Dot( setup.Rotation.Right ) * 0.03f, Time.Delta * 15.0f );

			var appliedLean = lean;
			appliedLean += MathF.Sin( walkBob ) * bobSpeedAlpha * 0.2f;
			setup.Rotation *= Rotation.From( 0, 0, appliedLean );

			bobSpeedAlpha = (bobSpeedAlpha - 0.7f).Clamp( 0, 1 ) * 3.0f;

			fov = fov.LerpTo( bobSpeedAlpha * 20 * MathF.Abs( forwardspeed ), Time.Delta * 2.0f );

			setup.FieldOfView += fov;
		}

	}
}
