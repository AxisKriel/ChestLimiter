using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Mono.Data.Sqlite;
using MySql.Data.MySqlClient;
using TShockAPI;
using TShockAPI.DB;

namespace ChestLimiter.DB
{
	public class LimiterManager
	{
		private IDbConnection db;

		public List<Limiter> Limiters = new List<Limiter>();

		public LimiterManager(IDbConnection db)
		{
			this.db = db;

			var sql = new SqlTableCreator(db, db.GetSqlType() == SqlType.Sqlite ?
				(IQueryBuilder)new SqliteQueryCreator() : (IQueryBuilder)new MysqlQueryCreator());

			sql.EnsureExists(new SqlTable("Limiters",
				new SqlColumn("AccountName", MySqlDbType.VarChar) { Primary = true, Unique = true },
				new SqlColumn("Chests", MySqlDbType.Text),
				new SqlColumn("Limit", MySqlDbType.Int32)));

			using (var result = db.QueryReader("SELECT * FROM Limiters"))
			{
				while (result.Read())
				{
					Limiters.Add(new Limiter()
					{
						AccountName = result.Get<string>("AccountName"),
						Chests = Limiter.Parse(result.Get<string>("Chests")),
						Limit = result.Get<int>("Limit")
					});
				}
			}
		}

		public bool Add(Limiter value)
		{
			try
			{
				#region RemoveEmpties

				for (int i = 0; i < value.Chests.Count; i++)
				{
					Limiters.FindAll(l => l.Chests.Contains(value.Chests[i])).ForEach(l =>
						{
							UpdateChests(l.AccountName, l.Chests.FindAll(n => n != value.Chests[i]));
						});
				}

				#endregion
				Limiters.Add(value);
				return db.Query("INSERT INTO 'Limiters' ('AccountName', 'Chests', 'Limit') VALUES (@0, @1, @2)",
					value.AccountName, value.ToString(), value.Limit) == 1;
			}
			catch (Exception ex)
			{
				Log.Error(ex.ToString());
				return false;
			}
		}

		public bool AddChest(string accountName, int chestID)
		{
			try
			{
				// Remove empties
				Limiters.FindAll(l => l.Chests.Contains(chestID)).ForEach(l =>
					{
						l.Chests.Remove(chestID);
					});

				Limiter limiter = GetLimiter(accountName);
				if (limiter != null)
				{
					var list = limiter.Chests;
					list.Add(chestID);
					return UpdateChests(accountName, list);
				}
				return false;
			}
			catch (Exception ex)
			{
				Log.Error(ex.ToString());
				return false;
			}
		}

		public bool Del(string accountName)
		{
			try
			{
				Limiters.RemoveAll(l => l.AccountName == accountName);
				return db.Query("DELETE FROM 'Limiters' WHERE 'AccountName' = @0", accountName) == 1;
			}
			catch (Exception ex)
			{
				Log.Error(ex.ToString());
				return false;
			}
		}

		public bool DelChest(string accountName, int chestID)
		{
			try
			{
				Limiter limiter = GetLimiter(accountName);
				if (limiter != null)
				{
					var list = limiter.Chests;
					list.RemoveAll(c => c == chestID);
					return UpdateChests(accountName, list);
				}
				return false;
			}
			catch (Exception ex)
			{
				Log.Error(ex.ToString());
				return false;
			}
		}

		public Limiter GetLimiter(string accountName)
		{
			return Limiters.Find(l => l.AccountName == accountName);
		}

		public Limiter GetLimiterByChest(int chestID)
		{
			return Limiters.Find(l => l.Chests.Contains(chestID));
		}

		public bool Reload()
		{
			try
			{
				Limiters.Clear();
				using (var result = db.QueryReader("SELECT * FROM Limiters"))
				{
					while (result.Read())
					{
						Limiters.Add(new Limiter()
						{
							AccountName = result.Get<string>("AccountName"),
							Chests = Limiter.Parse(result.Get<string>("Chests")),
							Limit = result.Get<int>("Limit")
						});
					}
				}
				return true;
			}
			catch (Exception)
			{
				return false;
			}
		}

		public bool UpdateChests(string accountName, List<int> value)
		{
			try
			{
				Limiters.Find(l => l.AccountName == accountName).Chests = value;
				return db.Query("UPDATE Limiters SET Chests = @1 WHERE AccountName = @0",
					accountName,
					string.Join(",", value)) == 1;
			}
			catch (Exception ex)
			{
				Log.Error(ex.ToString());
				return false;
			}
		}

		public bool UpdateLimit(string accountName, int value)
		{
			try
			{
				Limiters.Find(l => l.AccountName == accountName).Limit = value;
				return db.Query("UPDATE Limiters SET Limit = @1 WHERE AccountName = @0",
					accountName,
					value) == 1;
			}
			catch (Exception ex)
			{
				Log.Error(ex.ToString());
				return false;
			}
		}
	}
}
