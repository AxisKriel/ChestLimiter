using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ChestLimiter.DB;
using TShockAPI;

namespace ChestLimiter
{
	internal class Commands
	{
		#region Strings

		private static string m_chests(int count)
		{
			return "This player owns {0} chests.".SFormat(count);
		}

		private static string m_exempt(bool group)
		{
			return "{0} exempt from the chest limit.".SFormat(group ? "Your group is" : "You are");
		}

		private static string m_exempt2(string accountName, bool group)
		{
			return "{0}{1} is exempt from the chest limit.".SFormat(accountName, group ? "'s group" : "");
		}

		private static string m_none = "You have no chests placed counting towards your limit.";

		private static string m_nonep(string accountName)
		{
			return "{0} has no chests placed counting towards their limit.".SFormat(accountName);
		}

		private static string m_format(Limiter limiter)
		{
			return "You have placed {0} out of {1} chests.".SFormat(limiter.Chests.Count, limiter.Limit);
		}

		private static string m_format2(string accountName, Limiter limiter)
		{
			return "{0} has placed {1} out of {2} chests.".SFormat(accountName, limiter.Chests.Count, limiter.Limit);
		}

		#endregion

		public static void ChestLimit(CommandArgs args)
		{
			if (args.Parameters.Count < 1)
			{
				if (!args.Player.Group.HasPermission(Permissions.CheckSelf))
				{
					args.Player.SendErrorMessage("You do not have access to this command.");
					return;
				}

				Limiter limiter = ChestLimiter.Limiters.GetLimiter(args.Player.UserAccountName);
				bool group = args.Player.Group.HasPermission(Permissions.Exempt);
				args.Player.SendInfoMessage((limiter == null || limiter.Chests.Count < 1) ? m_none :
					(limiter.Unlimited || group) ? m_exempt(group) : m_format(limiter));
				if (limiter.Unlimited || group)
					args.Player.SendInfoMessage(m_chests(limiter.Chests.Count));
			}
			else
			{
				if (!args.Player.Group.HasPermission(Permissions.CheckOthers))
				{
					args.Player.SendErrorMessage("You do not have access to this command.");
					return;
				}

				string modify = "";
				var sb = new StringBuilder();

				for (int i = 0; i < args.Parameters.Count; i++)
				{
					if (i > 0 && (args.Parameters[i].StartsWith("+") || args.Parameters[i].StartsWith("-") ||
						args.Parameters[i].StartsWith("*") || Char.IsNumber(args.Parameters[i][0])))
					{
						if (!args.Player.Group.HasPermission(Permissions.Modify))
						{
							args.Player.SendErrorMessage("You do not have the permission to modify chest limits.");
							return;
						}

						modify = args.Parameters[i];
						break;
					}
					else
						sb.Append((sb.Length > 0 ? " " : "") + args.Parameters[i]);
				}

				string accountName = sb.ToString();
				var user = TShock.Users.GetUserByName(accountName);

				if (user == null)
					args.Player.SendErrorMessage("Invalid user!");
				else
				{
					Limiter limiter = ChestLimiter.Limiters.GetLimiter(accountName);
					bool group = TShock.Groups.GetGroupByName(user.Group).HasPermission(Permissions.Exempt);
					if (String.IsNullOrEmpty(modify))
					{
						args.Player.SendInfoMessage(limiter == null ? m_nonep(accountName) :
							(limiter.Unlimited || group) ? m_exempt2(accountName, group) : m_format2(accountName, limiter));
						if (limiter.Unlimited || group)
						{
							args.Player.SendInfoMessage(m_chests(limiter.Chests.Count));
						}
					}
					else
					{
						// 0 = set, 1 = increase, 2 = decrease, 3 = infinite
						byte action = 0;
						int value;

						if (modify.StartsWith("+"))
						{
							action = 1;
							modify = modify.Substring(1);
						}
						else if (modify.StartsWith("-"))
						{
							action = 2;
							modify = modify.Substring(1);
						}
						else if (modify.StartsWith("*"))
						{
							action = 3;
							modify = "0";
						}

						if (!int.TryParse(modify, out value))
						{
							args.Player.SendErrorMessage("Invalid value!");
							return;
						}

						int limit = limiter == null ? ChestLimiter.Config.BaseLimit : limiter.Limit;
						if (action == 0)
							limit = Math.Max(1, value);
						if (action == 1)
							limit += value;
						if (action == 2)
							limit = Math.Max(1, limit - value);
						if (action == 3)
							limit = -1;

						if (limiter == null)
						{
							limiter = new Limiter(accountName, limit);
							ChestLimiter.Limiters.Add(limiter);
						}
						else
							ChestLimiter.Limiters.UpdateLimit(accountName, limit);

						args.Player.SendInfoMessage("Set {0}'s chest limit to {1}.",
							accountName, limit == -1 ? "unlimited" : limit.ToString());
					}
				}
			}
		}

		public static void ChestOwner(CommandArgs args)
		{
			if (args.Parameters.Count < 1)
			{
				ChestLimiter.AwaitingOwner[args.Player.Index] = true;
				args.Player.SendInfoMessage("Open a chest to get its info.");
			}
		}
	}
}
