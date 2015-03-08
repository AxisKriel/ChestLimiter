using System.Collections.Generic;
using System.Text.RegularExpressions;
using ChestLimiter.DB;
using ChestLimiter.Extensions;
using Terraria;
using TShockAPI;
using TShockAPI.DB;

namespace ChestLimiter
{
	public class Commands
	{
		private static string Specifier = TShockAPI.Commands.Specifier;

		#region Strings

		private static string m_chests(int count)
		{
			return "This player owns {0} chests.".SFormat(count);
		}

		//private static string m_exempt_self = "Your group is exempt from the chest limit.";

		//private static string m_exempt_other(string accountName)
		//{
		//	return "{0}'s group is exempt from the chest limit.".SFormat(accountName);
		//}

		private static string m_none_self = "You have no chests placed counting towards your limit.";

		private static string m_none_other(string accountName)
		{
			return "{0} has no chests placed counting towards their limit.".SFormat(accountName);
		}

		private static string m_format_self(Limiter limiter, bool exempt = false)
		{
			return "You have placed {0}{1} chests.".SFormat(limiter.Chests.Count, limiter.Unlimited || exempt ?
				"" : " out of " + limiter.Limit);
		}

		private static string m_format_other(string accountName, Limiter limiter, bool exempt = false)
		{
			return "{0} has placed {1}{2} chests.".SFormat(accountName, limiter.Chests.Count, limiter.Unlimited || exempt ?
				"" : " out of " + limiter.Limit);
		}

		#endregion

		public static async void ChestLimit(CommandArgs args)
		{
			var regex = new Regex(@"^\w+ (?:(-h(?:elp)?|help)|""(.+)""|(\S+?))(?: (-l(?:imit)?) (\*|[+-]?\d+?))?$");
			Match match = regex.Match(args.Message);
			if (args.Parameters.Count < 1)
			{
				if (!args.Player.Group.HasPermission(Permissions.CheckSelf))
					args.Player.SendErrorMessage("You don't have the permission to check your own limit.");
				else
				{
					Limiter limiter = await ChestLimiter.Limiters.GetAsync(args.Player.UserAccountName);
					args.Player.SendInfoMessage((limiter == null || limiter.Chests.Count < 1) ?
						m_none_self : m_format_self(limiter, args.Player.IsExempt()));

				}
			}
			else if (!match.Success || !string.IsNullOrWhiteSpace(match.Groups[1].Value))
			{
				args.Player.SendInfoMessage("Syntax: {0}climit <user name> [params...]", Specifier);
				args.Player.SendInfoMessage(
					"Params: -l *|[+-]digit - Sets the user's limit. * is unlimited. Use [+-] to increase or decrease their limit.");
			}
			else
			{
				string accountName = string.IsNullOrEmpty(match.Groups[2].Value) ? match.Groups[3].Value : match.Groups[2].Value;
				bool lset = !string.IsNullOrWhiteSpace(match.Groups[4].Value);
				string lvalue = match.Groups[5].Value;

				if ((lset && !args.Player.Group.HasPermission(Permissions.Modify)))
					args.Player.SendErrorMessage("You don't have the permission to modify user limits.");
				else if (!args.Player.Group.HasPermission(Permissions.CheckOthers))
					args.Player.SendErrorMessage("You don't have the permission to check user limits.");
				else
				{
					User user = TShock.Users.GetUserByName(accountName);
					Limiter limiter;
					if (user == null)
						args.Player.SendErrorMessage("Invalid user!");
					else if ((limiter = await ChestLimiter.Limiters.GetAsync(user.Name)) == null)
						args.Player.SendErrorMessage("{0} hasn't placed any chests.", user.Name);
					else if (!lset)
					{
						// Display a string based on the user's limiter status and permissions
						var group = TShock.Groups.GetGroupByName(user.Group);
						bool exempt = group != null && group.HasPermission(Permissions.Exempt);
						args.Player.SendInfoMessage(m_format_other(user.Name, limiter, exempt));
					}
					else
					{
						if (lvalue[0] == '*')
							limiter.Unlimited = true;
						else
						{
							int value;
							if (!int.TryParse(lvalue, out value))
							{
								args.Player.SendErrorMessage("Invalid value!");
								return;
							}
							if (lvalue[0] == '+' || lvalue[0] == '-')
								limiter.Limit += value;
							else
								limiter.Limit = value;
						}
						var result = await ChestLimiter.Limiters.UpdateLimit(limiter.AccountName, limiter.Limit);
						switch (result)
						{
							case ReturnTypes.NullOrCorrupt:
								args.Player.SendErrorMessage("An error occurred during ChestLimiter's database update.");
								return;
							case ReturnTypes.Success:
								args.Player.SendSuccessMessage(
									"Set {0}'s limit to {1}.", limiter.AccountName, limiter.Unlimited ? "unlimited" : limiter.Limit.ToString());
								return;
							case ReturnTypes.Exception:
								args.Player.SendErrorMessage(
									"An exception was thrown during ChestLimiter's execution. Check logs for details.");
								break;

						}
					}
				}
			}
		}

		#region ChestLimitOld
		//public static void ChestLimitOld(CommandArgs args)
		//{
		//	if (args.Parameters.Count < 1)
		//	{
		//		if (!args.Player.Group.HasPermission(Permissions.CheckSelf))
		//		{
		//			args.Player.SendErrorMessage("You do not have access to this command.");
		//			return;
		//		}

		//		Limiter limiter = ChestLimiter.Limiters.GetAsync(args.Player.UserAccountName);
		//		bool group = args.Player.Group.HasPermission(Permissions.Exempt);
		//		args.Player.SendInfoMessage((limiter == null || limiter.Chests.Count < 1) ? m_none_self :
		//			(limiter.Unlimited || group) ? m_exempt_self(group) : m_format_self(limiter));
		//		if (limiter.Unlimited || group)
		//			args.Player.SendInfoMessage(m_chests(limiter.Chests.Count));
		//	}
		//	else
		//	{
		//		if (!args.Player.Group.HasPermission(Permissions.CheckOthers))
		//		{
		//			args.Player.SendErrorMessage("You do not have access to this command.");
		//			return;
		//		}

		//		string modify = "";
		//		var sb = new StringBuilder();

		//		for (int i = 0; i < args.Parameters.Count; i++)
		//		{
		//			if (i > 0 && (args.Parameters[i].StartsWith("+") || args.Parameters[i].StartsWith("-") ||
		//				args.Parameters[i].StartsWith("*") || Char.IsNumber(args.Parameters[i][0])))
		//			{
		//				if (!args.Player.Group.HasPermission(Permissions.Modify))
		//				{
		//					args.Player.SendErrorMessage("You do not have the permission to modify chest limits.");
		//					return;
		//				}

		//				modify = args.Parameters[i];
		//				break;
		//			}
		//			else
		//				sb.Append((sb.Length > 0 ? " " : "") + args.Parameters[i]);
		//		}

		//		string accountName = sb.ToString();
		//		var user = TShock.Users.GetUserByName(accountName);

		//		if (user == null)
		//			args.Player.SendErrorMessage("Invalid user!");
		//		else
		//		{
		//			Limiter limiter = ChestLimiter.Limiters.GetAsync(accountName);
		//			bool group = TShock.Groups.GetGroupByName(user.Group).HasPermission(Permissions.Exempt);
		//			if (String.IsNullOrEmpty(modify))
		//			{
		//				args.Player.SendInfoMessage(limiter == null ? m_none_other(accountName) :
		//					(limiter.Unlimited || group) ? m_exempt_other(accountName, group) : m_format_other(accountName, limiter));
		//				if (limiter.Unlimited || group)
		//				{
		//					args.Player.SendInfoMessage(m_chests(limiter.Chests.Count));
		//				}
		//			}
		//			else
		//			{
		//				// 0 = set, 1 = increase, 2 = decrease, 3 = infinite
		//				byte action = 0;
		//				int value;

		//				if (modify.StartsWith("+"))
		//				{
		//					action = 1;
		//					modify = modify.Substring(1);
		//				}
		//				else if (modify.StartsWith("-"))
		//				{
		//					action = 2;
		//					modify = modify.Substring(1);
		//				}
		//				else if (modify.StartsWith("*"))
		//				{
		//					action = 3;
		//					modify = "0";
		//				}

		//				if (!int.TryParse(modify, out value))
		//				{
		//					args.Player.SendErrorMessage("Invalid value!");
		//					return;
		//				}

		//				int limit = limiter == null ? ChestLimiter.Config.BaseLimit : limiter.Limit;
		//				if (action == 0)
		//					limit = Math.Max(1, value);
		//				if (action == 1)
		//					limit += value;
		//				if (action == 2)
		//					limit = Math.Max(1, limit - value);
		//				if (action == 3)
		//					limit = -1;

		//				if (limiter == null)
		//				{
		//					limiter = new Limiter(accountName, limit);
		//					ChestLimiter.Limiters.AddAsync(limiter);
		//				}
		//				else
		//					ChestLimiter.Limiters.UpdateLimit(accountName, limit);

		//				args.Player.SendInfoMessage("Set {0}'s chest limit to {1}.",
		//					accountName, limit == -1 ? "unlimited" : limit.ToString());
		//			}
		//		}
		//	}
		//}
		#endregion

		public static void ChestOwner(CommandArgs args)
		{
				ChestLimiter.AwaitingOwner[args.Player.Index] = true;
				args.Player.SendInfoMessage("Open a chest to get its info.");
		}

		public static async void ChestPrune(CommandArgs args)
		{
			args.Player.SendInfoMessage("Starting chest pruning. This might take a while...");
			try
			{
				int changeCount;
				int chestID;
				int chestsPruned = 0;
				int failCount = 0;	// no. of UpdateChests returning false/exception
				List<Limiter> limiters = await ChestLimiter.Limiters.GetAllAsync();
				for (int i = 0; i < limiters.Count; i++)
				{
					changeCount = 0;
					for (int j = 0; j < limiters[i].Chests.Count; j++)
					{
						chestID = limiters[i].Chests[j];
						if (Main.chest[chestID] == null)
						{
							limiters[i].Chests.Remove(chestID);
							changeCount++;
							chestsPruned++;
						}
					}
					if (changeCount > 0)
					{
						var result = await ChestLimiter.Limiters.UpdateChests(limiters[i].AccountName, limiters[i].Chests);
						if (result == ReturnTypes.NullOrCorrupt || result == ReturnTypes.Exception)
							failCount++;
					}
				}
				args.Player.SendSuccessMessage("Pruned {0} chest(s).", chestsPruned);
				if (failCount > 0)
					TShock.Log.ConsoleInfo("chestlimiter: prunechests failed {0} times", failCount);
			}
			catch
			{
				args.Player.SendErrorMessage("Unable to automatically prune chests. Consider doing this manually on your database.");
			}
		}

		#region ChestPruneOld
		//public static async void ChestPruneOld(CommandArgs args)
		//{
		//	try
		//	{
		//		int count = 0;
		//		int chestID = 0;
		//		for (int i = 0; i < ChestLimiter.Limiters.limiters.Count; i++)
		//		{
		//			for (int j = 0; j < ChestLimiter.Limiters.limiters[i].Chests.Count; j++)
		//			{
		//				chestID = ChestLimiter.Limiters.limiters[i].Chests[j];

		//				if (Main.chest[chestID] == null)
		//				{
		//					ChestLimiter.Limiters.limiters[i].Chests.RemoveAll(k => k == chestID);
		//					ChestLimiter.Limiters.UpdateChests(ChestLimiter.Limiters.limiters[i].AccountName,
		//						ChestLimiter.Limiters.limiters[i].Chests);
		//					count++;
		//				}
		//			}
		//		}

		//		args.Player.SendSuccessMessage("[ChestLimiter] Pruned {0} chests.", count);
		//	}
		//	catch (Exception)
		//	{
		//		args.Player.SendErrorMessage("[ChestLimiter] Unable to prune chests.");
		//	}
		//}
		#endregion
	}
}
