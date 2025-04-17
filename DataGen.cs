using Newtonsoft.Json;
using StardewModdingAPI;
using StardewValley;
using System.Text;

namespace MachineUpgradeSystem
{
	public static class DataGen
	{

		private static readonly string[] defaultTiers =
			["Steel", "Gold", "Diamond", "Iridium", "Radioactive", "Prismatic"];

		private static readonly string[] defaultTierItems =
			["(O)tlitookilakin.mus_steelUpgrade", "(O)tlitookilakin.mus_goldUpgrade", "(O)tlitookilakin.mus_diamondUpgrade", "(O)tlitookilakin.mus_iridiumUpgrade", "(O)tlitookilakin.mus_radioactiveUpgrade", "(O)tlitookilakin.mus_prismaticUpgrade"];

		private static IMonitor Monitor = null!;

		public enum EntryType {Error, Raw, Entry, Field}

		internal static void Init(IMonitor monitor)
		{
			Monitor = monitor;
		}

		public static void GenerateJson(string modid, EntryType type, params string[]? tier_args)
		{
			string mod_prefix = modid + '_';
			string[] tiers, tierItems;

			if (tier_args is null || tier_args.Length is 0)
			{
				tiers = defaultTiers;
				tierItems = defaultTierItems;
			}
			else
			{
				int c = tier_args.Length / 2;
				tiers = new string[c];
				tierItems = new string[c];
				for (int i = 0; i < c; i++)
				{
					tiers[i] = tier_args[i * 2];
					tierItems[i] = tier_args[i * 2 + 1];
				}
			}

			Monitor.Log("Generating upgrade map...", LogLevel.Info);

			HashSet<string> ids = [];
			var bcData = DataLoader.BigCraftables(Game1.content);


			foreach (var id in bcData.Keys)
				if (id.StartsWith(mod_prefix))
					ids.Add(id);

			Monitor.Log($"Found {ids.Count} craftables", LogLevel.Info);

			var map = new Dictionary<string, UpgradeEntry>[tiers.Length];
			for (int i = 0; i < map.Length; i++)
				map[i] = [];

			foreach (var id in ids)
			{
				string chopped = id[mod_prefix.Length..];

				int mindex = 0;
				for (; mindex < tiers.Length; mindex++)
					if (chopped.StartsWith(tiers[mindex]))
						break;

				if (mindex >= tiers.Length)
				{
					Monitor.Log($"Failed to find tier; upgrade type for '{chopped}' missing.", LogLevel.Debug);
					continue;
				}

				string name = chopped[tiers[mindex].Length..];

				for (; mindex >= 0; mindex--)
					if (!ids.Contains(mod_prefix + tiers[mindex] + name, StringComparer.OrdinalIgnoreCase))
						break;
				mindex++;

				string from = ModUtilities.TryGetIdByName(name, modid, bcData, out var fid) ? "(BC)" + fid : "ERROR";

				for (int i = mindex; i < tiers.Length; i++)
				{
					var n_id = "(BC)" + mod_prefix + tiers[i] + name;
					map[i][from] = new(n_id);
					ids.Remove(from = n_id);
				}
			}

			DesktopClipboard.SetText(GetOutput(map, tierItems, tiers, type));
			Monitor.Log("Copied to clipboard", LogLevel.Info);
		}

		private static string GetOutput(
			IReadOnlyList<Dictionary<string, UpgradeEntry>> entries, IReadOnlyList<string> upgrades, IReadOnlyList<string> tierNames, EntryType type
		)
		{
			if (type is EntryType.Raw or EntryType.Entry)
			{
				Dictionary<string, Dictionary<string, UpgradeEntry>> mapped = [];

				for (int i = 0; i < upgrades.Count; i++)
					mapped[upgrades[i]] = entries[i];

				var serialized = JsonConvert.SerializeObject(mapped);
				if (type is EntryType.Raw)
					return serialized;

				return
					"""{"Changes":[{"LogName":"Register Upgrades","Action":"EditData","Target":"Mods/tlitookilakin.mus/Upgrades","Entries":""" +
					serialized + """}]}""";
			}

			if (type is EntryType.Field)
			{
				StringBuilder sb = new();

				sb.Append("""{"Changes":[""");

				for (int i = 0; i < entries.Count; i++)
				{
					var set = entries[i];
					if (set.Count == 0)
						continue;

					sb.Append("""{"Action":"EditData","Target":"Mods/tlitookilakin.mus/Upgrades","LogName":"Register """);
					sb.Append(tierNames[i]).Append(" Upgrades\",\"TargetField\":[\"");
					sb.Append(upgrades[i]).Append("\"],\"Entries\":");
					sb.Append(JsonConvert.SerializeObject(set));
					sb.Append("},");
				}

				sb.Append("""]}""");
				return sb.ToString();
			}

			return "";
		}
	}
}
