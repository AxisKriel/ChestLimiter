using System;
using System.ComponentModel;
using System.IO;
using Newtonsoft.Json;
using TShockAPI;

namespace ChestLimiter
{
	public class Config
	{
		[Description("Sets the message to display when a chest is placed. Will be hidden if the value is null or empty. Format: {0} - chestCount, {1} - chestLimit")]
		public string AnnounceOnPlacement = "You've placed {0} out of {1} chests.";

		[Description("Sets the base amount of chests a player can place.")]
		public int BaseLimit = 5;

		public string StorageType = "sqlite";

		public string MySqlHost = "localhost:3306";

		public string MySqlDbName = "";

		public string MySqlUsername = "";

		public string MySqlPassword = "";

		public static Config Read(string path)
		{
			try
			{
				Directory.CreateDirectory(Path.GetDirectoryName(path));
				if (!File.Exists(path))
				{
					Config config = new Config();
					File.WriteAllText(path, JsonConvert.SerializeObject(config, Formatting.Indented));
					return config;
				}
				else
					return JsonConvert.DeserializeObject<Config>(File.ReadAllText(path));
			}
			catch (Exception ex)
			{
				TShock.Log.ConsoleError("chestlimiter config.read: " + ex.Message);
				TShock.Log.Error(ex.ToString());
				return new Config();
			}
		}
	}
}
