using System.Collections.Generic;
using Newtonsoft.Json;

namespace ChestLimiter.Extensions
{
	public static class ListExtensions
	{
		public static string Serialize<T>(this List<T> list)
		{
			return JsonConvert.SerializeObject(list);
		}
	}
}
