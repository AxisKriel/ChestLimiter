using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ChestLimiter.DB;
using Mono.Data.Sqlite;
using MySql.Data.MySqlClient;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;

namespace ChestLimiter
{
	[ApiVersion(1, 16)]
    public class ChestLimiter : TerrariaPlugin
    {
		public static bool[] AwaitingOwner { get; private set; }

		public static Config Config { get; private set; }

		public static IDbConnection Db { get; private set; }
		public static LimiterManager Limiters { get; private set; }

		public override string Author
		{
			get { return "Enerdy"; }
		}

		public override string Description
		{
			get { return "Limits chest placement."; }
		}

		public override string Name
		{
			get { return "ChestLimiter"; }
		}

		public override Version Version
		{
			get { return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version; }
		}

		public ChestLimiter(Main game)
			: base(game)
		{
			AwaitingOwner = new bool[Main.maxPlayers];

			Order = 2;
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				ServerApi.Hooks.GameInitialize.Deregister(this, OnInitialize);
				ServerApi.Hooks.NetGetData.Deregister(this, OnGetData);
				ServerApi.Hooks.ServerLeave.Deregister(this, OnLeave);
			}
		}

		public override void Initialize()
		{
			ServerApi.Hooks.GameInitialize.Register(this, OnInitialize);
			ServerApi.Hooks.NetGetData.Register(this, OnGetData);
			ServerApi.Hooks.ServerLeave.Register(this, OnLeave);
		}

		void OnGetData(GetDataEventArgs e)
		{
			if (e.Handled)
				return;

			#region Packet 31 - Get Chest Contents

			if (e.MsgID == PacketTypes.ChestGetContents)
			{
				TSPlayer player = TShock.Players[e.Msg.whoAmI];
				if (player == null || !player.Active || !player.RealPlayer)
					return;

				int i = player.Index;
				if (AwaitingOwner[i])
				{
					using (var reader = new BinaryReader(new MemoryStream(e.Msg.readBuffer, e.Index, e.Length)))
					{
						int x = reader.ReadInt16();
						int y = reader.ReadInt16();

						int chestID = Chest.FindChest(x, y);
						if (chestID != -1)
						{
							Limiter limiter = Limiters.GetLimiterByChest(chestID);
							player.SendInfoMessage("({0},{1}) ChestID: {2}.{3}", x, y, chestID,
								limiter != null ? "The owner is {0}.".SFormat(player.UserAccountName) : "");
							AwaitingOwner[i] = false;
							e.Handled = true;
						}
					}
				}
			}

			#endregion

			#region Packet 34 - Place/Kill Chest

			else if (e.MsgID == PacketTypes.TileKill)
			{
				TSPlayer player = TShock.Players[e.Msg.whoAmI];
				if (player == null || !player.Active || !player.RealPlayer)
					return;

				string name = player.UserAccountName;
				using (var reader = new BinaryReader(new MemoryStream(e.Msg.readBuffer, e.Index, e.Length)))
				{
					byte id = reader.ReadByte();
					int x = reader.ReadInt16();
					int y = reader.ReadInt16();

					if (!TShock.Regions.CanBuild(x, y, player))
						return;

					if (id == 0)
					{
						// Places a chest

						int chestID = Tools.GetCreateChestIndex(x, y);

						bool add = false;
						Limiter limiter = Limiters.GetLimiter(name);
						if (limiter == null)
						{
							limiter = new Limiter(name, Config.BaseLimit);
							add = true;
						}

						if (chestID > -1 && chestID < Main.maxChests)
						{
							if (limiter.Add(chestID, player.Group.HasPermission(Permissions.Exempt)))
							{
								if (!limiter.Unlimited && !String.IsNullOrEmpty(Config.AnnounceOnPlacement))
									player.SendInfoMessage(Config.AnnounceOnPlacement, limiter.Chests.Count, limiter.Limit);
								if (add)
									Limiters.Add(limiter);
								else
									Limiters.AddChest(name, limiter.Chests.Last());
							}
							else
							{
								player.SendErrorMessage("You've reached your chest limit ({0}).", limiter.Limit);
								player.SendTileSquare(x, y, 4);
								e.Handled = true;
							}
						}
					}
					else if (id == 1)
					{
						// Kills a chest

						if (Main.tile[x, y].frameY % 36 != 0)
							y--;
						if (Main.tile[x, y].frameX % 36 != 0)
							x--;

						int chestID = Chest.FindChest(x, y);

						Limiter limiter = Limiters.GetLimiterByChest(chestID);
						if (limiter != null)
						{
							Limiters.DelChest(limiter.AccountName, chestID);
						}
					}
				}
			}

			#endregion
		}

		void OnInitialize(EventArgs e)
		{
			#region Config
			Config = Config.Read(Path.Combine(TShock.SavePath, "ChestLimiter", "Config.json"));
			#endregion

			#region Commands

			Action<Command, string> Add = (command, helpText) =>
				{
					command.HelpText = helpText;
					TShockAPI.Commands.ChatCommands.Add(command);
				};

			Add(new Command(new List<string>()
			{
				Permissions.CheckSelf,
				Permissions.CheckOthers,
				Permissions.Modify
			}, Commands.ChestLimit, "climit", "chestlimit"),
			"Syntax: {0}climit <user name> [* | +/-value]".SFormat(TShock.Config.CommandSpecifier));

			Add(new Command(Permissions.Owner, Commands.ChestOwner, "cowner", "chestowner"),
				"Returns information regarding a chest's coordinates and owner, if possible.");

			Add(new Command(Permissions.Prune, Commands.ChestPrune, "cprune", "chestprune"),
				"Prunes chests from the ChestLimiter Database which are no longer existant. " +
				"Useful after using a chest removal command.");

			#endregion

			#region DB

			if (Config.StorageType.Equals("mysql", StringComparison.OrdinalIgnoreCase))
			{
				string[] host = Config.MySqlHost.Split(':');
				Db = new MySqlConnection()
				{
					ConnectionString = String.Format("Server={0}; Port={1}; Database={2}; Uid={3}; Pwd={4};",
					host[0],
					host.Length == 1 ? "3306" : host[1],
					Config.MySqlDbName,
					Config.MySqlUsername,
					Config.MySqlPassword)
				};
			}
			else if (Config.StorageType.Equals("sqlite", StringComparison.OrdinalIgnoreCase))
				Db = new SqliteConnection(String.Format("uri=file://{0},Version=3",
					Path.Combine(TShock.SavePath, "ChestLimiter", "Database.sqlite")));
			else
				throw new InvalidOperationException("Invalid storage type!");

			#endregion

			Limiters = new LimiterManager(Db);
		}

		void OnLeave(LeaveEventArgs e)
		{
			AwaitingOwner[e.Who] = false;
		}
    }
}
