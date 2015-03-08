using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using ChestLimiter.DB;
using ChestLimiter.Extensions;
using Mono.Data.Sqlite;
using MySql.Data.MySqlClient;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.Hooks;

namespace ChestLimiter
{
	[ApiVersion(1, 17)]
    public class ChestLimiter : TerrariaPlugin
    {
		public static bool[] AwaitingOwner { get; private set; }

		public static Config Config { get; private set; }
		public static string ConfigPath { get; private set; }

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
			get { return Assembly.GetExecutingAssembly().GetName().Version; }
		}

		public ChestLimiter(Main game)
			: base(game)
		{
			AwaitingOwner = new bool[256];

			Order = 2;
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				ServerApi.Hooks.GameInitialize.Deregister(this, OnInitialize);
				ServerApi.Hooks.NetGetData.Deregister(this, OnGetData);
				ServerApi.Hooks.ServerLeave.Deregister(this, OnLeave);

				AccountHooks.AccountDelete -= OnAccountDelete;
				GeneralHooks.ReloadEvent -= OnReload;

				Db.Dispose();
			}
		}

		public override void Initialize()
		{
			ServerApi.Hooks.GameInitialize.Register(this, OnInitialize);
			ServerApi.Hooks.NetGetData.Register(this, OnGetData);
			ServerApi.Hooks.ServerLeave.Register(this, OnLeave);

			AccountHooks.AccountDelete += OnAccountDelete;
			GeneralHooks.ReloadEvent += OnReload;
		}

		async void OnAccountDelete(AccountDeleteEventArgs e)
		{
			Limiter limiter = await Limiters.GetAsync(e.User.Name);
			if (limiter != null)
			{
				var result = await Limiters.DelAsync(e.User.Name);
				switch (result)
				{
					case ReturnTypes.NullOrCorrupt:
						TShock.Log.ConsoleError("chestlimiter: error deleting chest limiter for '{0}'", e.User.Name);
						break;
					case ReturnTypes.Success:
						TShock.Log.ConsoleInfo("chestlimiter: deleted '{0}' chest limiter", e.User.Name);
						break;
					case ReturnTypes.Exception:
						TShock.Log.ConsoleError("chestlimiter: exception thrown while deleting '{0}' chest limiter.", e.User.Name);
						break;
				}
			}
		}

		void OnGetData(GetDataEventArgs e)
		{
			if (e.Handled)
				return;

			int ply = e.Msg.whoAmI;
			if (ply < 0 || ply > 255 || TShock.Players[ply] == null || !TShock.Players[ply].RealPlayer || !TShock.Players[ply].IsLoggedIn)
				return;

			#region Packet 31 - Get Chest Contents

			if (e.MsgID == PacketTypes.ChestGetContents && AwaitingOwner[ply])
			{
				Task.Run(() => GetDataHandlers.HandleGetChestContents(e));
				e.Handled = true;
			}

			#endregion

			#region Packet 34 - Place/Kill Chest

			else if (e.MsgID == PacketTypes.TileKill)
			{
				Task.Run(() => GetDataHandlers.HandleTileKill(e));
				e.Handled = true;
			}

			#endregion
		}

		void OnInitialize(EventArgs e)
		{
			#region Config
			ConfigPath = Path.Combine(TShock.SavePath, "ChestLimiter", "ChestLimiter.json");
			Config = Config.Read(ConfigPath);
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
			},
			Commands.ChestLimit, "climit", "chestlimit"),
			"Syntax: {0}climit <user name> (-l *|[+-]digit)".SFormat(TShockAPI.Commands.Specifier));

			Add(new Command(Permissions.Owner, Commands.ChestOwner, "cowner", "chestowner") { AllowServer = false },
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
					Path.Combine(TShock.SavePath, "ChestLimiter", "ChestLimiter.sqlite")));
			else
				throw new InvalidOperationException("Invalid storage type!");

			#endregion

			Limiters = new LimiterManager(Db);
		}

		void OnLeave(LeaveEventArgs e)
		{
			AwaitingOwner[e.Who] = false;
		}

		async void OnReload(ReloadEventArgs e)
		{
			await Task.Run(() => Config = Config.Read(ConfigPath));
			var result = await Limiters.ReloadAsync();
			switch (result)
			{
				case ReturnTypes.NullOrCorrupt:
					e.Player.SendErrorMessage("Failed to reload ChestLimiter's database.");
					break;
				case ReturnTypes.Success:
					e.Player.SendSuccessMessage("[ChestLimiter] Reloaded config and database!");
					break;
				case ReturnTypes.Exception:
					e.Player.SendErrorMessage("An exception was thrown during ChestLimiter's database reload. Check logs for details.");
					break;
			}
		}
    }
}
