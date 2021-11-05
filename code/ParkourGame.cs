using Sandbox;

namespace Facepunch.Parkour
{
	public partial class ParkourGame : Sandbox.Game
	{
		public ParkourGame()
		{
			if ( IsClient )
			{
				new ParkourHud();
			}
		}

		public override void ClientJoined( Client client )
		{
			base.ClientJoined( client );

			var player = new ParkourPlayer();
			client.Pawn = player;

			player.Respawn();
		}
	}

}
