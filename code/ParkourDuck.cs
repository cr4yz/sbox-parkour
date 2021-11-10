using Sandbox;

namespace Facepunch.Parkour
{
	class ParkourDuck : BaseNetworkable
	{

		private ParkourController _controller;
		private Vector3 _originalMins;
		private Vector3 _originalMaxs;

		public TimeSince TimeSinceSlide { get; set; }
		public bool Sliding { get; private set; }
		public bool IsActive { get; private set; }

		public ParkourDuck( ParkourController controller )
		{
			_controller = controller;
		}

		public virtual void PreTick()
		{
			bool wants = Input.Down( InputButton.Duck );

			if ( wants != IsActive )
			{
				if ( wants ) TryDuck();
				else TryUnDuck();
			}

			if ( IsActive )
			{
				var wasSliding = Sliding;
				Sliding = _controller.GroundEntity != null && _controller.Velocity.Length > _controller.SlideThreshold;
				_controller.SetTag( Sliding ? "sitting" : "ducked" );
				_controller.EyePosLocal *= Sliding ? .35f : .5f;

				if ( Sliding && !wasSliding )
				{
					TimeSinceSlide = 0;

					var len = _controller.Velocity.WithZ( 0 ).Length;
					var newLen = len + _controller.SlideBoost;
					_controller.Velocity *= newLen / len;
				}
			}
		}

		protected void TryDuck()
		{
			IsActive = true;
		}

		protected void TryUnDuck()
		{
			var pm = _controller.TraceBBox( _controller.Position, _controller.Position, _originalMins, _originalMaxs );
			if ( pm.StartedSolid ) return;

			Sliding = false;
			IsActive = false;
		}

		public void UpdateBBox( ref Vector3 mins, ref Vector3 maxs, float scale )
		{
			_originalMins = mins;
			_originalMaxs = maxs;

			if ( IsActive )
			{
				maxs = maxs.WithZ( (Sliding ? 20 : 36) * scale );
			}
		}

		public float GetWishSpeed()
		{
			if ( !IsActive ) return -1;
			return Sliding ? _controller.DuckSpeed : _controller.DuckSpeed;
		}

	}
}
