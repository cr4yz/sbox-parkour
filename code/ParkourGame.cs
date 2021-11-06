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

		public override void DoPlayerSuicide( Client cl )
		{
			base.DoPlayerSuicide( cl );

			var player = new ParkourPlayer();
			cl.Pawn = player;

			player.Respawn();
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
