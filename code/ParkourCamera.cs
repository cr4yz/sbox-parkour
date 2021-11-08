using Sandbox;

namespace Facepunch.Parkour
{
    class ParkourCamera : Camera
	{

		private Vector3 _lastPos;
		private float _targetFov;
		private float _minFov => 100;
		private float _maxFov => 115;

		public override void Activated()
		{
			var pawn = Local.Pawn;
			if ( pawn == null ) return;

			Position = pawn.EyePos;
			Rotation = pawn.EyeRot;

			_lastPos = Position;
		}

		public override void Update()
		{
			if ( Local.Pawn is not ParkourPlayer pawn )
				return;

			var controller = pawn.Controller as ParkourController;
			var eyePos = pawn.EyePos;

			Position = eyePos.WithZ( _lastPos.z.LerpTo( eyePos.z, 50f * Time.Delta ) );
			//Position = eyePos;
			Rotation = pawn.EyeRot;

			Viewer = pawn;
			_lastPos = Position;

			var spdA = controller.Velocity.WithZ(0).Length / controller.DefaultSpeed;

			_targetFov = _minFov.LerpTo( _maxFov, spdA * spdA * spdA * spdA );
			FieldOfView = FieldOfView.LerpTo( _targetFov, Time.Delta * 10 );
		}

	}
}
