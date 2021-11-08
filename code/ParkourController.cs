using Sandbox;
using System;

namespace Facepunch.Parkour
{
	partial class ParkourController : BasePlayerController
	{

		[Net] public float SprintSpeed { get; set; } = 325f;
		[Net] public float WalkSpeed { get; set; } = 150.0f;
		[Net] public float DefaultSpeed { get; set; } = 325f;
		[Net] public float DuckSpeed { get; set; } = 110f;
		[Net] public float SlideThreshold { get; set; } = 130f;
		[Net] public float Acceleration { get; set; } = 2f;
		[Net] public float DuckAcceleration { get; set; } = 5f;
		[Net] public float MomentumGain { get; set; } = .5f;
		[Net] public float MomentumLose { get; set; } = 3f;
		[Net] public float AirAcceleration { get; set; } = 35.0f;
		[Net] public float GroundFriction { get; set; } = 4.0f;
		[Net] public float SlideFriction { get; set; } = 100.0f;
		[Net] public float StopSpeed { get; set; } = 100.0f;
		[Net] public float GroundAngle { get; set; } = 46.0f;
		[Net] public float StepSize { get; set; } = 18.0f;
		[Net] public float MaxNonJumpVelocity { get; set; } = 140.0f;
		[Net] public float BodyGirth { get; set; } = 32.0f;
		[Net] public float BodyHeight { get; set; } = 72.0f;
		[Net] public float EyeHeight { get; set; } = 64.0f;
		[Net] public float Gravity { get; set; } = 800.0f;
		[Net] public float AirControl { get; set; } = 30.0f;
		[Net] public bool AutoJump { get; set; } = false;
		[Net] public float VaultTime { get; set; } = .5f;
		[Net] public float WallRunTime { get; set; } = 3f;
		[Net] public float WallRunThreshold { get; set; } = 200f;
		[Net, Predicted] public TimeSince TimeSinceVault { get; set; }
		[Net, Predicted] public Vector3 VaultStart { get; set; }
		[Net, Predicted] public Vector3 VaultEnd { get; set; }
		[Net, Predicted] public TimeSince TimeSinceWallRun { get; set; }
		[Net, Predicted] public Vector3 WallNormal { get; set; }

		public bool Swimming { get; set; } = false;
		public bool WallRunning => TimeSinceWallRun < WallRunTime;

		public ParkourDuck Duck { get; private set; }
		public Unstuck Unstuck { get; private set; }

		private float _momentum;
		private Vector3 _previousVelocity;
		private bool _wasGrounded;
		private Vector3 _mins;
		private Vector3 _maxs;
		private bool _isTouchingLadder;
		private Vector3 _ladderNormal;

		public ParkourController()
		{
			Duck = new ParkourDuck( this );
			Unstuck = new Unstuck( this );
		}

		public override BBox GetHull()
		{
			var girth = BodyGirth * 0.5f;
			var mins = new Vector3( -girth, -girth, 0 );
			var maxs = new Vector3( +girth, +girth, BodyHeight );

			return new BBox( mins, maxs );
		}

		public virtual void SetBBox( Vector3 mins, Vector3 maxs )
		{
			if ( this._mins == mins && this._maxs == maxs )
				return;

			this._mins = mins;
			this._maxs = maxs;
		}

		/// <summary>
		/// Update the size of the bbox. We should really trigger some shit if this changes.
		/// </summary>
		public virtual void UpdateBBox()
		{
			var girth = BodyGirth * 0.5f;

			var mins = new Vector3( -girth, -girth, 0 ) * Pawn.Scale;
			var maxs = new Vector3( +girth, +girth, BodyHeight ) * Pawn.Scale;

			Duck.UpdateBBox( ref mins, ref maxs, Pawn.Scale );

			SetBBox( mins, maxs );
		}

		protected float SurfaceFriction;


		public override void FrameSimulate()
		{
			base.FrameSimulate();

			EyeRot = Input.Rotation;
		}

		public override void Simulate()
		{
			CheckFallDamage();

			EyePosLocal = Vector3.Up * (EyeHeight * Pawn.Scale);
			UpdateBBox();

			EyePosLocal += TraceOffset;
			EyeRot = Input.Rotation;

			RestoreGroundPos();

			if ( TimeSinceVault < VaultTime )
			{
				Position = Vector3.Lerp( VaultStart, VaultEnd, TimeSinceVault / VaultTime );
				return;
			}

			if ( Unstuck.TestAndFix() )
				return;

			CheckLadder();
			Swimming = Pawn.WaterLevel.Fraction > 0.6f;

			//
			// Start Gravity
			//
			if ( !Swimming && !_isTouchingLadder && !WallRunning )
			{
				Velocity -= new Vector3( 0, 0, Gravity * 0.5f ) * Time.Delta;
				Velocity += new Vector3( 0, 0, BaseVelocity.z ) * Time.Delta;

				BaseVelocity = BaseVelocity.WithZ( 0 );
			}

			if ( AutoJump ? Input.Down( InputButton.Jump ) : Input.Pressed( InputButton.Jump ) )
			{
				CheckJumpButton();
			}

			if ( GroundEntity != null )
			{
				Velocity = Velocity.WithZ( 0 );
				ApplyFriction( GroundFriction * SurfaceFriction );
			}

			//
			// Work out wish velocity.. just take input, rotate it to view, clamp to -1, 1
			//
			WishVelocity = new Vector3( Input.Forward, Input.Left, 0 );
			var inSpeed = WishVelocity.Length.Clamp( 0, 1 );
			WishVelocity *= Input.Rotation;

			if ( !Swimming && !_isTouchingLadder && !WallRunning )
			{
				WishVelocity = WishVelocity.WithZ( 0 );
			}

			WishVelocity = WishVelocity.Normal * inSpeed;
			WishVelocity *= GetWishSpeed();

			Duck.PreTick();

			_previousVelocity = Velocity;

			bool bStayOnGround = false;
			if ( Swimming )
			{
				ApplyFriction( 1 );
				WaterMove();
			}
			else if ( _isTouchingLadder )
			{
				LadderMove();
			}
			else if ( WallRunning )
			{
				WallRunMove();
			}
			else if ( GroundEntity != null )
			{
				bStayOnGround = true;
				WalkMove();
			}
			else
			{
				AirMove();
			}

			CategorizePosition( bStayOnGround );

			// FinishGravity
			if ( !Swimming && !_isTouchingLadder && !WallRunning )
			{
				Velocity -= new Vector3( 0, 0, Gravity * 0.5f ) * Time.Delta;
			}

			if ( GroundEntity != null )
			{
				Velocity = Velocity.WithZ( 0 );
			}

			SaveGroundPos();

			if ( Debug && Pawn.IsLocalPawn )
			{
				DebugOverlay.Box( Position + TraceOffset, _mins, _maxs, Color.Red );
				DebugOverlay.Box( Position, _mins, _maxs, Color.Blue );

				var lineOffset = 0;
				if ( Host.IsServer ) lineOffset = 10;

				DebugOverlay.ScreenText( lineOffset + 0, $"        Position: {Position}", .05f );
				DebugOverlay.ScreenText( lineOffset + 1, $"        Velocity: {Velocity}", .05f );
				DebugOverlay.ScreenText( lineOffset + 2, $"           Speed: {Velocity.Length}", .05f );
				DebugOverlay.ScreenText( lineOffset + 3, $"    BaseVelocity: {BaseVelocity}", .05f );
				DebugOverlay.ScreenText( lineOffset + 4, $"    GroundEntity: {GroundEntity} [{GroundEntity?.Velocity}]", .05f );
				DebugOverlay.ScreenText( lineOffset + 5, $" SurfaceFriction: {SurfaceFriction}", .05f );
				DebugOverlay.ScreenText( lineOffset + 6, $"    WishVelocity: {WishVelocity}", .05f );
				DebugOverlay.ScreenText( lineOffset + 7, $"        Momentum: {_momentum}", .05f );
			}
		}

		public virtual float GetWishSpeed()
		{
			var ws = Duck.GetWishSpeed();
			if ( ws >= 0 ) return ws;

			if ( Input.Down( InputButton.Run ) ) return SprintSpeed;
			if ( Input.Down( InputButton.Walk ) ) return WalkSpeed;

			return DefaultSpeed;
		}

		private bool StillWallRunning()
		{
			if ( GroundEntity != null )
				return false;

			var trStart = Position + WallNormal * 5;
			var trEnd = trStart - WallNormal * 7;
			var tr = TraceBBox( trStart, trEnd );

			if ( !tr.Hit || tr.StartedSolid || tr.Normal != WallNormal )
				return false;

			return true;
		}

		public virtual void WallRunMove()
		{
			if ( !StillWallRunning() )
			{
				TimeSinceWallRun = float.MaxValue;

				if( GroundEntity != null )
					Velocity = Velocity.WithZ( 0 );
				return;
			}

			var wishspeed = WishVelocity.Length;

			WishVelocity = WishVelocity.WithZ( 0 );
			WishVelocity = WishVelocity.Normal * wishspeed;

			if ( WishVelocity.Length < 1.0f )
			{
				TimeSinceWallRun = float.MaxValue;
				return;
			}

			if ( Velocity.Length < 1.0f )
			{
				TimeSinceWallRun = float.MaxValue;
				Velocity = Vector3.Zero;
				return;
			}

			var gravity = TimeSinceWallRun / WallRunTime * Gravity;
			Velocity = Velocity.WithZ( Velocity.z - gravity * Time.Delta );

			// first try just moving to the destination
			var dest = Position + Velocity * Time.Delta;
			var pm = TraceBBox( Position, dest );

			if ( pm.Fraction == 1 )
			{
				Position = pm.EndPos;
				return;
			}

			StepMove();
		}

		public virtual void WalkMove()
		{
			var wishdir = WishVelocity.Normal;
			var wishspeed = WishVelocity.Length;

			WishVelocity = WishVelocity.WithZ( 0 );
			WishVelocity = WishVelocity.Normal * wishspeed;

			if ( Duck.Sliding )
			{
				if ( GroundNormal.z < 1 )
				{
					var slopeDir = Vector3.Cross( Vector3.Up, Vector3.Cross( Vector3.Up, GroundNormal ) );
					var dot = Vector3.Dot( Velocity.Normal, slopeDir );
					var slopeForward = Vector3.Cross( GroundNormal, Pawn.Rotation.Right );
					var spdGain = 200f.LerpTo( 500f, 1f - GroundNormal.z );

					if ( dot > 0 )
						spdGain *= -1;

					Velocity += spdGain * slopeForward * Time.Delta;
				}
			}

			var accel = Duck.IsActive && !Duck.Sliding ? DuckAcceleration : Acceleration;

			if ( Duck.Sliding )
				accel = .5f;

			Velocity = Velocity.WithZ( 0 );
			Accelerate( wishdir, wishspeed, 0, accel + _momentum );
			Velocity = Velocity.WithZ( 0 );

			DoMomentum();

			// Add in any base velocity to the current velocity.
			Velocity += BaseVelocity;

			try
			{
				if ( Velocity.Length < 1.0f )
				{
					Velocity = Vector3.Zero;
					return;
				}

				// first try just moving to the destination
				var dest = (Position + Velocity * Time.Delta).WithZ( Position.z );

				var pm = TraceBBox( Position, dest );

				if ( pm.Fraction == 1 )
				{
					Position = pm.EndPos;
					StayOnGround();
					return;
				}

				StepMove();
			}
			finally
			{

				// Now pull the base velocity back out.   Base velocity is set if you are on a moving object, like a conveyor (or maybe another monster?)
				Velocity -= BaseVelocity;
			}

			StayOnGround();
		}

		private float _prevSpd;
		private void DoMomentum()
		{
			if ( WishVelocity.IsNearZeroLength || Duck.Sliding )
			{
				if ( _momentum > 0 )
					_momentum -= Time.Delta * MomentumLose;
				else
					_momentum = 0;
				return;
			}

			var spd = Velocity.WithZ( 0 ).Length;
			var lossFactor = spd / _prevSpd;
			_prevSpd = spd;

			if ( lossFactor < .9f )
			{
				_momentum *= lossFactor;
			}
			else
			{
				if ( spd < GetWishSpeed() )
					_momentum += Time.Delta * MomentumGain;
			}
		}

		private void CheckFallDamage()
		{
			var grounded = GroundEntity != null;
			var justLanded = !_wasGrounded && grounded;
			_wasGrounded = grounded;

			if ( !justLanded )
				return;

			var willSlide = Input.Down( InputButton.Duck ) && Velocity.WithZ( 0 ).Length > SlideThreshold;
			var fallSpeed = Math.Abs( _previousVelocity.z );
			var fallSpeedMaxLoss = willSlide ? 5000 : 2000;
			var a = 1f - MathF.Min( fallSpeed / fallSpeedMaxLoss, 1 );

			Velocity = Velocity.ClampLength( Velocity.Length * a );
			_momentum *= a;
		}

		public virtual void StepMove()
		{
			MoveHelper mover = new MoveHelper( Position, Velocity );
			mover.Trace = mover.Trace.Size( _mins, _maxs ).Ignore( Pawn );
			mover.MaxStandableAngle = GroundAngle;

			mover.TryMoveWithStep( Time.Delta, StepSize );

			Position = mover.Position;
			Velocity = mover.Velocity;
		}

		public virtual void Move()
		{
			MoveHelper mover = new MoveHelper( Position, Velocity );
			mover.Trace = mover.Trace.Size( _mins, _maxs ).Ignore( Pawn );
			mover.MaxStandableAngle = GroundAngle;

			mover.TryMove( Time.Delta );

			Position = mover.Position;
			Velocity = mover.Velocity;
		}

		/// <summary>
		/// Add our wish direction and speed onto our velocity
		/// </summary>
		public virtual void Accelerate( Vector3 wishdir, float wishspeed, float speedLimit, float acceleration )
		{
			if ( speedLimit > 0 && wishspeed > speedLimit )
				wishspeed = speedLimit;

			// See if we are changing direction a bit
			var currentspeed = Velocity.Dot( wishdir );

			// Reduce wishspeed by the amount of veer.
			var addspeed = wishspeed - currentspeed;

			// If not going to add any speed, done.
			if ( addspeed <= 0 )
				return;

			// Determine amount of acceleration.
			var accelspeed = acceleration * Time.Delta * wishspeed * SurfaceFriction;

			// Cap at addspeed
			if ( accelspeed > addspeed )
				accelspeed = addspeed;

			Velocity += wishdir * accelspeed;
		}

		/// <summary>
		/// Remove ground friction from velocity
		/// </summary>
		public virtual void ApplyFriction( float frictionAmount = 1.0f )
		{
			var speed = Velocity.Length;
			if ( speed < 0.1f ) return;

			var control = (speed < StopSpeed) ? StopSpeed : speed;
			var drop = control * Time.Delta * frictionAmount;

			if ( Duck.Sliding )
				drop = SlideFriction * Time.Delta;

			// scale the velocity
			float newspeed = speed - drop;
			if ( newspeed < 0 ) newspeed = 0;

			if ( newspeed != speed )
			{
				newspeed /= speed;
				Velocity *= newspeed;
			}
		}

		public virtual void CheckJumpButton()
		{
			float flGroundFactor = 1.0f;
			float flMul = 268.3281572999747f * 1.2f;
			float startz = Velocity.z;
			var jumpPower = startz + flMul * flGroundFactor;

			if ( WallRunning )
			{
				TimeSinceWallRun = float.MaxValue;
				Velocity = Velocity + WallNormal * 200 + Vector3.Up * jumpPower;
				return;
			}

			if ( Swimming )
			{
				ClearGroundEntity();
				Velocity = Velocity.WithZ( 100 );
				return;
			}

			if ( TryVault() )
			{
				AddEvent( "vault" );
				return;
			}

			if ( GroundEntity == null )
			{
				if ( TryWallRun() )
				{
					AddEvent( "wallrun" );
				}
				return;
			}

			ClearGroundEntity();

			if ( Duck.IsActive )
				flMul *= 0.8f;

			Velocity = Velocity.WithZ( jumpPower );
			Velocity -= new Vector3( 0, 0, Gravity * 0.5f ) * Time.Delta;

			AddEvent( "jump" );
		}

		private bool TryWallRun()
		{
			var startPos = Position;
			var endPos = startPos + Rotation.Forward * BodyGirth * 2;

			var trace = TraceBBox( startPos, endPos );

			if ( !trace.Hit )
			{
				endPos = startPos + Velocity.Normal * BodyGirth * 2;
				trace = TraceBBox( startPos, endPos );

				if ( !trace.Hit ) return false;
			}

			if ( trace.Normal.z != 0 ) return false;

			Velocity = Velocity.WithZ( 0 );
			WallNormal = trace.Normal;
			TimeSinceWallRun = 0;

			return true;
		}

		private bool TryVault()
		{
			var startPos = Position + Rotation.Forward * BodyGirth;
			startPos.z += 100;
			var endPos = startPos.WithZ( Position.z + StepSize + 1 );

			var trace = TraceBBox( startPos, endPos );
			if ( !trace.Hit ) return false;
			if ( trace.StartedSolid ) return false;

			DebugOverlay.Line( startPos, endPos, 10f, false );
			DebugOverlay.Line( startPos, startPos + Vector3.Right * 50, 10f, false );

			var vaultHeight = trace.EndPos.z - Position.z + 10;
			TimeSinceVault = 0;
			VaultStart = Position;
			VaultEnd = Position.WithZ( Position.z + vaultHeight ) + Rotation.Forward * BodyGirth;

			return true;
		}

		public virtual void AirMove()
		{
			var wishdir = WishVelocity.Normal;
			var wishspeed = WishVelocity.Length;

			Accelerate( wishdir, wishspeed, AirControl, AirAcceleration );

			Velocity += BaseVelocity;

			Move();

			Velocity -= BaseVelocity;
		}

		public virtual void WaterMove()
		{
			var wishdir = WishVelocity.Normal;
			var wishspeed = WishVelocity.Length;

			wishspeed *= 0.8f;

			Accelerate( wishdir, wishspeed, 100, Acceleration );

			Velocity += BaseVelocity;

			Move();

			Velocity -= BaseVelocity;
		}

		public virtual void CheckLadder()
		{
			if ( _isTouchingLadder && Input.Pressed( InputButton.Jump ) )
			{
				Velocity = _ladderNormal * 100.0f;
				_isTouchingLadder = false;

				return;
			}

			const float ladderDistance = 1.0f;
			var start = Position;
			Vector3 end = start + (_isTouchingLadder ? (_ladderNormal * -1.0f) : WishVelocity.Normal) * ladderDistance;

			var pm = Trace.Ray( start, end )
						.Size( _mins, _maxs )
						.HitLayer( CollisionLayer.All, false )
						.HitLayer( CollisionLayer.LADDER, true )
						.Ignore( Pawn )
						.Run();

			_isTouchingLadder = false;

			if ( pm.Hit )
			{
				_isTouchingLadder = true;
				_ladderNormal = pm.Normal;
			}
		}

		public virtual void LadderMove()
		{
			var velocity = WishVelocity;
			float normalDot = velocity.Dot( _ladderNormal );
			var cross = _ladderNormal * normalDot;
			Velocity = (velocity - cross) + (-normalDot * _ladderNormal.Cross( Vector3.Up.Cross( _ladderNormal ).Normal ));

			Move();
		}


		public virtual void CategorizePosition( bool bStayOnGround )
		{
			SurfaceFriction = 1.0f;

			// Doing this before we move may introduce a potential latency in water detection, but
			// doing it after can get us stuck on the bottom in water if the amount we move up
			// is less than the 1 pixel 'threshold' we're about to snap to.	Also, we'll call
			// this several times per frame, so we really need to avoid sticking to the bottom of
			// water on each call, and the converse case will correct itself if called twice.
			//CheckWater();

			var point = Position - Vector3.Up * 2;
			var vBumpOrigin = Position;

			//
			//  Shooting up really fast.  Definitely not on ground trimed until ladder shit
			//
			bool bMovingUpRapidly = Velocity.z > MaxNonJumpVelocity;
			bool bMovingUp = Velocity.z > 0;

			bool bMoveToEndPos = false;

			if ( GroundEntity != null ) // and not underwater
			{
				bMoveToEndPos = true;
				point.z -= StepSize;
			}
			else if ( bStayOnGround )
			{
				bMoveToEndPos = true;
				point.z -= StepSize;
			}

			if ( bMovingUpRapidly || Swimming ) // or ladder and moving up
			{
				ClearGroundEntity();
				return;
			}

			var pm = TraceBBox( vBumpOrigin, point, 4.0f );

			if ( pm.Entity == null || Vector3.GetAngle( Vector3.Up, pm.Normal ) > GroundAngle )
			{
				ClearGroundEntity();
				bMoveToEndPos = false;

				if ( Velocity.z > 0 )
					SurfaceFriction = 0.25f;
			}
			else
			{
				UpdateGroundEntity( pm );
			}

			if ( bMoveToEndPos && !pm.StartedSolid && pm.Fraction > 0.0f && pm.Fraction < 1.0f )
			{
				Position = pm.EndPos;
			}

		}

		/// <summary>
		/// We have a new ground entity
		/// </summary>
		public virtual void UpdateGroundEntity( TraceResult tr )
		{
			GroundNormal = tr.Normal;

			// VALVE HACKHACK: Scale this to fudge the relationship between vphysics friction values and player friction values.
			// A value of 0.8f feels pretty normal for vphysics, whereas 1.0f is normal for players.
			// This scaling trivially makes them equivalent.  REVISIT if this affects low friction surfaces too much.
			SurfaceFriction = tr.Surface.Friction * 1.25f;
			if ( SurfaceFriction > 1 ) SurfaceFriction = 1;

			//if ( tr.Entity == GroundEntity ) return;

			Vector3 oldGroundVelocity = default;
			if ( GroundEntity != null ) oldGroundVelocity = GroundEntity.Velocity;

			bool wasOffGround = GroundEntity == null;

			GroundEntity = tr.Entity;

			if ( GroundEntity != null )
			{
				BaseVelocity = GroundEntity.Velocity;
			}
		}

		/// <summary>
		/// We're no longer on the ground, remove it
		/// </summary>
		public virtual void ClearGroundEntity()
		{
			if ( GroundEntity == null ) return;

			GroundEntity = null;
			GroundNormal = Vector3.Up;
			SurfaceFriction = 1.0f;
		}

		/// <summary>
		/// Traces the current bbox and returns the result.
		/// liftFeet will move the start position up by this amount, while keeping the top of the bbox at the same
		/// position. This is good when tracing down because you won't be tracing through the ceiling above.
		/// </summary>
		public override TraceResult TraceBBox( Vector3 start, Vector3 end, float liftFeet = 0.0f )
		{
			return TraceBBox( start, end, _mins, _maxs, liftFeet );
		}

		/// <summary>
		/// Try to keep a walking player on the ground when running down slopes etc
		/// </summary>
		public virtual void StayOnGround()
		{
			var start = Position + Vector3.Up * 2;
			var end = Position + Vector3.Down * StepSize;

			// See how far up we can go without getting stuck
			var trace = TraceBBox( Position, start );
			start = trace.EndPos;

			// Now trace down from a known safe position
			trace = TraceBBox( start, end );

			if ( trace.Fraction <= 0 ) return;
			if ( trace.Fraction >= 1 ) return;
			if ( trace.StartedSolid ) return;
			if ( Vector3.GetAngle( Vector3.Up, trace.Normal ) > GroundAngle ) return;

			// This is incredibly hacky. The real problem is that trace returning that strange value we can't network over.
			// float flDelta = fabs( mv->GetAbsOrigin().z - trace.m_vEndPos.z );
			// if ( flDelta > 0.5f * DIST_EPSILON )

			Position = trace.EndPos;
		}

		void RestoreGroundPos()
		{
			if ( GroundEntity == null || GroundEntity.IsWorld )
				return;

			//var Position = GroundEntity.Transform.ToWorld( GroundTransform );
			//Pos = Position.Position;
		}

		void SaveGroundPos()
		{
			if ( GroundEntity == null || GroundEntity.IsWorld )
				return;

			//GroundTransform = GroundEntity.Transform.ToLocal( new Transform( Pos, Rot ) );
		}

		private Vector3 ClipVector( Vector3 input, Vector3 normal, float overbounce )
		{
			var backoff = Vector3.Dot( input, normal ) * overbounce;

			for ( int i = 0; i < 3; i++ )
			{
				var change = normal[i] * backoff;
				input[i] -= change;
			}

			return input;
		}

	}
}
