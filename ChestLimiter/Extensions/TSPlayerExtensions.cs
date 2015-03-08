using TShockAPI;

namespace ChestLimiter.Extensions
{
	public static class TSPlayerExtensions
	{
		public static bool IsExempt(this TSPlayer player)
		{
			return player.Group.HasPermission(Permissions.Exempt);
		}
	}
}
