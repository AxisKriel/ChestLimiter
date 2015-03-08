using System.Collections.Generic;
using Newtonsoft.Json;

namespace ChestLimiter.DB
{
	public class Limiter
	{
		public string AccountName { get; private set; }

		public List<int> Chests { get; set; }

		public int Limit { get; set; }

		public bool Unlimited
		{
			get { return Limit == -1; }
			set
			{
				if (value)
					Limit = -1;
				else
					Limit = ChestLimiter.Config.BaseLimit;
			}
		}

		public Limiter(string accountName, int limit)
		{
			AccountName = accountName;
			Chests = new List<int>();
			Limit = limit;
		}

		//public bool AddChest(int chestID, bool force = false)
		//{
		//	if (!force && !Unlimited && Chests.Count + 1 > Limit)
		//		return false;

		//	Chests.Add(chestID);
		//	return true;
		//}

		//public bool RemoveChest(int chestID)
		//{
		//	return Chests.Remove(chestID);
		//}

		public bool LoadChests(string json)
		{
			try
			{
				Chests = JsonConvert.DeserializeObject<List<int>>(json);
				return true;
			}
			catch
			{
				return false;
			}
		}

		/// <summary>
		/// Returns a JSON-encoded string of this object
		/// </summary>
		public override string ToString()
		{
			return JsonConvert.SerializeObject(this);
		}
	}
}
