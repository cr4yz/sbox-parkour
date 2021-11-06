using Sandbox;
using Sandbox.UI;

namespace Facepunch.Parkour
{
	[UseTemplate]
	public class ParkourHud : RootPanel
	{

		public float Speed { get; set; }

		public override void Tick()
		{
			base.Tick();

			if ( Local.Pawn is not ParkourPlayer player )
				return;

			Speed = (int)player.Velocity.WithZ( 0 ).Length;
		}

	}
}
