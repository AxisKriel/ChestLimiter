using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using ChestLimiter.Extensions;
using MySql.Data.MySqlClient;
using TShockAPI;
using TShockAPI.DB;

namespace ChestLimiter.DB
{
	public class LimiterManager
	{
		private IDbConnection db;
		private object syncLock = new object();
		private List<Limiter> limiters = new List<Limiter>();

		public LimiterManager(IDbConnection db)
		{
			this.db = db;

			var sql = new SqlTableCreator(db, db.GetSqlType() == SqlType.Sqlite ?
				(IQueryBuilder)new SqliteQueryCreator() : (IQueryBuilder)new MysqlQueryCreator());

			sql.EnsureTableStructure(new SqlTable("Limiters",
				new SqlColumn("AccountName", MySqlDbType.VarChar) { Primary = true, Unique = true },
				new SqlColumn("Chests", MySqlDbType.Text),
				new SqlColumn("Limit", MySqlDbType.Int32)));

			using (var result = db.QueryReader("SELECT * FROM `Limiters`"))
			{
				while (result.Read())
				{
					Limiter limiter = new Limiter(result.Get<string>("AccountName"), result.Get<int>("Limit"));
					limiter.LoadChests(result.Get<string>("Chests"));
					limiters.Add(limiter);
				}
			}
		}

		public Task<ReturnTypes> AddAsync(Limiter value)
		{
			return Task.Run(() =>
				{
					try
					{
						lock (syncLock)
						{
							limiters.Add(value);
							return db.Query("INSERT INTO `Limiters` (`AccountName`, `Chests`, `Limit`) VALUES (@0, @1, @2)",
								value.AccountName, value.Chests.Serialize(), value.Limit) == 1 ? ReturnTypes.Success : ReturnTypes.NullOrCorrupt;
						}
					}
					catch (Exception ex)
					{
						TShock.Log.Error(ex.ToString());
						return ReturnTypes.Exception;
					}
				});
		}

		public Task<ReturnTypes> DelAsync(string accountName)
		{
			return Task.Run(() =>
				{
					try
					{
						lock (syncLock)
						{
							limiters.RemoveAll(l => l.AccountName == accountName);
							return db.Query("DELETE FROM `Limiters` WHERE `AccountName` = @0", accountName) > 0 ?
								ReturnTypes.Success : ReturnTypes.NullOrCorrupt;
						}
					}
					catch (Exception ex)
					{
						TShock.Log.Error(ex.ToString());
						return ReturnTypes.Exception;
					}
				});
		}

		public Task<List<Limiter>> GetAllAsync()
		{
			return Task.Run(() => { return limiters; });
		}

		public Task<Limiter> GetAsync(string accountName)
		{
			return Task.Run(() =>
				{
					lock (syncLock)
						return limiters.Find(l => l.AccountName == accountName);
				});
		}

		public Task<Limiter> GetByChestAsync(int chestID)
		{
			return Task.Run(() =>
				{
					lock (syncLock)
						return limiters.Find(l => l.Chests.Contains(chestID));
				});
		}

		public Task<ReturnTypes> ReloadAsync()
		{
			return Task.Run(() =>
				{
					try
					{
						lock (syncLock)
						{
							limiters.Clear();
							using (var result = db.QueryReader("SELECT * FROM `Limiters`"))
							{
								while (result.Read())
								{
									Limiter limiter = new Limiter(result.Get<string>("AccountName"), result.Get<int>("Limit"));
									limiter.LoadChests(result.Get<string>("Chests"));
									limiters.Add(limiter);
								}
							}
							return ReturnTypes.Success;
						}
					}
					catch (Exception ex)
					{
						TShock.Log.ConsoleError(ex.ToString());
						return ReturnTypes.Exception;
					}
				});
		}

		public Task<ReturnTypes> UpdateChests(string accountName, List<int> value)
		{
			return Task.Run<ReturnTypes>(() =>
				{
					try
					{
						lock (syncLock)
						{
							Limiter limiter = limiters.Find(l => l.AccountName == accountName);
							if (limiter == null)
								return ReturnTypes.NullOrCorrupt;

							limiter.Chests = value;
							return db.Query("UPDATE `Limiters` SET `Chests` = @1 WHERE `AccountName` = @0",
								accountName, value.Serialize()) == 1 ? ReturnTypes.Success : ReturnTypes.NullOrCorrupt;
						}
					}
					catch (Exception ex)
					{
						TShock.Log.Error(ex.ToString());
						return ReturnTypes.Exception;
					}
				});
		}

		public Task<ReturnTypes> UpdateLimit(string accountName, int value)
		{
			return Task.Run(() =>
				{
					try
					{
						lock (syncLock)
						{
							Limiter limiter = limiters.Find(l => l.AccountName == accountName);
							if (limiter == null)
								return ReturnTypes.NullOrCorrupt;

							limiter.Limit = value;
							return db.Query("UPDATE `Limiters` SET `Limit` = @1 WHERE `AccountName` = @0",
								accountName, value) == 1 ? ReturnTypes.Success : ReturnTypes.NullOrCorrupt;
						}
					}
					catch (Exception ex)
					{
						TShock.Log.ConsoleError(ex.ToString());
						return ReturnTypes.Exception;
					}
				});
		}

		#region Obsolete
		[Obsolete("Use GetByChestAsync instead.")]
		public Limiter GetLimiterByChest(int chestID)
		{
			return limiters.Find(l => l.Chests.Contains(chestID));
		}

		[Obsolete("Not functional. Use ReloadAsync instead.")]
		public bool Reload()
		{
			try
			{
				limiters.Clear();
				using (var result = db.QueryReader("SELECT * FROM Limiters"))
				{
					while (result.Read())
					{
						//Limiters.Add(new Limiter()
						//{
						//	AccountName = result.Get<string>("AccountName"),
						//	Chests = Limiter.Parse(result.Get<string>("Chests")),
						//	Limit = result.Get<int>("Limit")
						//});
					}
				}
				return true;
			}
			catch (Exception)
			{
				return false;
			}
		}
		#endregion
	}

	public enum ReturnTypes
	{
		/// <summary>
		/// Returned when a null limiter is passed down or db.Query fails.
		/// </summary>
		NullOrCorrupt = 0,
		/// <summary>
		/// Returned when the Task finishes successfully.
		/// </summary>
		Success = 1,
		/// <summary>
		/// Returned when the Task finishes abruptly due to an unhandled exception.
		/// </summary>
		Exception = 2
	}
}
