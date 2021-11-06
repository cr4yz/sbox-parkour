using Sandbox;

namespace Facepunch.Parkour
{
    class ParkourCamera : Camera
	{

		Vector3 lastPos;
		float minFov => 100;
		float maxFov => 115;

		public override void Activated()
		{
			var pawn = Local.Pawn;
			if ( pawn == null ) return;

			Position = pawn.EyePos;
			Rotation = pawn.EyeRot;

			lastPos = Position;
		}

		public override void Update()
		{
			if ( Local.Pawn is not ParkourPlayer pawn )
				return;

			var controller = pawn.Controller as ParkourController;
			var eyePos = pawn.EyePos;

			Position = eyePos.WithZ( lastPos.z.LerpTo( eyePos.z, 50f * Time.Delta ) );
			//Position = eyePos;
			Rotation = pawn.EyeRot;

			Viewer = pawn;
			lastPos = Position;

			var spdA = controller.Velocity.WithZ(0).Length / controller.DefaultSpeed;

			FieldOfView = minFov.LerpTo( maxFov, spdA * spdA * spdA * spdA );
		}

	}
}
