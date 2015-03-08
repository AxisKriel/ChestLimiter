using System.IO;
using System.Threading.Tasks;
using ChestLimiter.DB;
using ChestLimiter.Extensions;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;

namespace ChestLimiter
{
	public class GetDataHandlers
	{
		public static async void HandleGetChestContents(GetDataEventArgs e)
		{
			int ply = e.Msg.whoAmI;
			using (var reader = new BinaryReader(new MemoryStream(e.Msg.readBuffer, e.Index, e.Length)))
			{
				int x = reader.ReadInt16();
				int y = reader.ReadInt16();

				int chestID = Chest.FindChest(x, y);
				if (chestID != -1)
				{
					Limiter limiter = await ChestLimiter.Limiters.GetByChestAsync(chestID);
					TShock.Players[ply].SendInfoMessage("(X: {0} Y: {1}) ChestID: {2}.{3}", x, y, chestID,
						limiter != null ? " The owner is {0}.".SFormat(limiter.AccountName) : "");
					ChestLimiter.AwaitingOwner[ply] = false;
				}
			}
		}

		public static async void HandleTileKill(GetDataEventArgs e)
		{
			int ply = e.Msg.whoAmI;

			using (var reader = new BinaryReader(new MemoryStream(e.Msg.readBuffer, e.Index, e.Length)))
			{
				byte action = reader.ReadByte();
				int x = reader.ReadInt16();
				int y = reader.ReadInt16();
				int style = reader.ReadInt16();

				if (action == 0 && TShock.Regions.CanBuild(x, y, TShock.Players[ply]))
				{
					string accountName = TShock.Players[ply].UserAccountName;
					Limiter limiter = await ChestLimiter.Limiters.GetAsync(accountName);

					if (limiter == null)
					{
						limiter = new Limiter(accountName, ChestLimiter.Config.BaseLimit);
						var result = await ChestLimiter.Limiters.AddAsync(limiter);
						switch (result)
						{
							case ReturnTypes.NullOrCorrupt:
								TShock.Players[ply].SendErrorMessage("An error occurred during ChestLimiter's database update.");
								return;
							case ReturnTypes.Exception:
								TShock.Players[ply].SendErrorMessage(
									"An exception was thrown during ChestLimiter's execution. Check logs for details.");
								return;
						}
					}

					bool unorex = limiter.Unlimited || TShock.Players[ply].IsExempt();
					bool canPlace = unorex || limiter.Chests.Count < limiter.Limit;
					if (!canPlace)
					{
						TShock.Players[ply].SendErrorMessage("You've reached your chest limit ({0})!", limiter.Limit);
						TShock.Players[ply].SendTileSquare(x, y, 3);
						return;
					}

					// Places the chest
					int chestID = await Task.Run(() => WorldGen.PlaceChest(x, y, 21, false, style));
					NetMessage.SendData((int)PacketTypes.TileKill, -1, ply, "", 0, x, y, style, 1);
					NetMessage.SendData((int)PacketTypes.TileKill, ply, -1, "", 0, x, y, style, 0);

					//TShock.Log.ConsoleInfo("chestID: " + chestID);
					if (chestID > -1 && !limiter.Chests.Contains(chestID))
					{
						limiter.Chests.Add(chestID);
						var result = await ChestLimiter.Limiters.UpdateChests(accountName, limiter.Chests);
						switch (result)
						{
							case ReturnTypes.NullOrCorrupt:
								TShock.Players[ply].SendErrorMessage("An error occurred during ChestLimiter's database update.");
								break;
							case ReturnTypes.Success:
								if (!unorex && !string.IsNullOrWhiteSpace(ChestLimiter.Config.AnnounceOnPlacement))
									TShock.Players[ply].SendInfoMessage(
										ChestLimiter.Config.AnnounceOnPlacement, limiter.Chests.Count, limiter.Limit);
								break;
							case ReturnTypes.Exception:
								TShock.Players[ply].SendErrorMessage(
									"An exception was thrown during ChestLimiter's execution. Check logs for details.");
								break;
						}
					}
				}
				else if (TShock.Regions.CanBuild(x, y, TShock.Players[ply]) && Main.tile[x, y].type == 21)
				{
					if (Main.tile[x, y].frameY % 36 != 0)
						y--;
					if (Main.tile[x, y].frameX % 36 != 0)
						x--;

					string accountName = TShock.Players[ply].UserAccountName;
					int chestID = Chest.FindChest(x, y);

					// Why bother doing all the limiter stuff if the chest no longer effectively exists?
					if (chestID > -1)
					{
						Limiter limiter = await ChestLimiter.Limiters.GetByChestAsync(chestID);
						if (limiter != null)
						{
							limiter.Chests.Remove(chestID);
							var result = await ChestLimiter.Limiters.UpdateChests(accountName, limiter.Chests);
							switch (result)
							{
								case ReturnTypes.NullOrCorrupt:
									TShock.Players[ply].SendErrorMessage("An error occurred during ChestLimiter's database update.");
									break;
								case ReturnTypes.Success:
									// No message to send at this point...
									break;
								case ReturnTypes.Exception:
									TShock.Players[ply].SendErrorMessage(
										"An exception was thrown during ChestLimiter's execution. Check logs for details.");
									break;
							}
						}
					}

					// Kills the chest
					WorldGen.KillTile(x, y);
					TSPlayer.All.SendData(PacketTypes.Tile, "", 0, x, y + 1);
				}
			}
		}
	}
}
