using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChestLimiter.DB
{
	public class Limiter
	{
		public string AccountName { get; set; }

		public List<int> Chests { get; set; }

		public int Limit { get; set; }

		public Limiter()
		{
			Chests = new List<int>();
		}

		public Limiter(string accountName, int limit)
			: this()
		{
			AccountName = accountName;
			Limit = limit;
		}

		public bool Add(int chestID)
		{
			if (Chests.Count + 1 > Limit)
				return false;

			Chests.Add(chestID);
			return true;
		}


		public bool Remove(int chestID)
		{
			return Chests.Remove(chestID);
		}

		public static List<int> Parse(string data)
		{
			var list = new List<int>();
			string[] s = data.Split(',');

			for (int i = 0; i < s.Length; i++)
			{
				list.Add(int.Parse(s[i]));
			}

			return list;
		}

		/// <summary>
		/// Returns the list of chest IDs, separated by commas.
		/// </summary>
		public override string ToString()
		{
			return string.Join(",", Chests);
		}
	}
}
