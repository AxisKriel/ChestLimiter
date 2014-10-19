using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria;

namespace ChestLimiter
{
	public class Tools
	{
		public static int GetCreateChestIndex(int x, int y)
		{
			Chest chest;
			for (int i = 0; i < Main.maxChests; i++)
			{
				chest = Main.chest[i];
				if (chest == null)
					return i;
			}

			return -1;
		}
	}
}
