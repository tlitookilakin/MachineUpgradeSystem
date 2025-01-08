using Newtonsoft.Json;
using StardewModdingAPI;
using StardewValley;
using System.Text;

namespace MachineUpgradeSystem
{
	public class ModEntry : Mod
	{
		private const string command_desc = "Generates upgrade files. Arguments: <modid> <type> <output> ( [tier_prefix] [tier_item] )+ . <type> can be 'raw' (plain json), 'entry' (CP patch), or 'field' (CP patch using TargetFields). output is the output file. If not specified, tier will default to the built-in tiers that come with MUS.";

		private static readonly string[] defaultTiers =
			["Steel", "Gold", "Diamond", "Iridium", "Radioactive", "Prismatic"];

		private static readonly string[] defaultTierItems =
			["(O)tlitookilakin.mus_steelUpgrade", "(O)tlitookilakin.mus_goldUpgrade", "(O)tlitookilakin.mus_diamondUpgrade", "(O)tlitookilakin.mus_iridiumUpgrade", "(O)tlitookilakin.mus_radioactiveUpgrade", "(O)tlitookilakin.mus_prismaticUpgrade"];

		public override void Entry(IModHelper helper)
		{
			Config.Init(helper, ModManifest);
			Assets.Init(helper, Monitor);
			
			try
			{
				Patches.Patch(ModManifest, Monitor);
			}
			catch (Exception ex)
			{
				Monitor.Log("Failed to install patches:\n" + ex.ToString(), LogLevel.Error);
			}

			Helper.Events.GameLoop.GameLaunched += OnLaunch;
			Helper.ConsoleCommands.Add("mus_generate", command_desc, CommandRegenerate);
		}

		private void OnLaunch(object? sender, StardewModdingAPI.Events.GameLaunchedEventArgs e)
		{
			Assets.RequireReload();
		}

		private void CommandRegenerate(string cmd, string[] args)
		{
			if (args.Length < 3)
			{
				Monitor.Log("Required parameters missing.", LogLevel.Info);
				Monitor.Log(command_desc, LogLevel.Info);
				return;
			}

			if (args[1] is not ("raw" or "entry" or "field"))
			{
				Monitor.Log("Invalid type specified: must be 'raw', 'entry', or 'field'.");
				return;
			}

			string modid = args[0];
			string mod_prefix = modid + '_';
			string[] tiers, tierItems;

			if (args.Length is 3)
			{
				tiers = defaultTiers;
				tierItems = defaultTierItems;
			}
			else
			{
				int c = (args.Length - 3) / 2;
				tiers = new string[c];
				tierItems = new string[c];
				for (int i = 0; i < c; i++)
				{
					tiers[i] = args[i * 2 + 3];
					tierItems[i] = args[i * 2 + 4];
				}
			}

			Monitor.Log("Generating upgrade map...", LogLevel.Info);

			HashSet<string> ids = [];
			var bcData = DataLoader.BigCraftables(Game1.content);


			foreach (var id in bcData.Keys)
				if (id.StartsWith(mod_prefix))
					ids.Add(id);

			Monitor.Log($"Found {ids.Count} craftables", LogLevel.Info);

			Dictionary<string, string>[] map = new Dictionary<string, string>[tiers.Length];
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

				string from = ModUtilities.TryGetIdByName(name, modid, bcData, out var fid) ? fid : "ERROR";

				for (int i = mindex; i < tiers.Length; i++)
				{
					from = map[i][from] = mod_prefix + tiers[i] + name;
					ids.Remove(from);
				}
			}

			var filepath = Path.Combine(Directory.GetParent(Helper.DirectoryPath)!.FullName, args[2]);
			if (!filepath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
				filepath += ".json";

			File.WriteAllText(filepath, GetOutput(map, tierItems, tiers, args[1]));

			Monitor.Log("Done", LogLevel.Info);
		}

		private static string GetOutput(IReadOnlyList<Dictionary<string, string>> entries, IReadOnlyList<string> upgrades, IReadOnlyList<string> tierNames, string type)
		{
			if (type is "raw" or "entry")
			{
				Dictionary<string, Dictionary<string, string>> mapped = [];

				for (int i = 0; i < upgrades.Count; i++)
					mapped[upgrades[i]] = entries[i];

				var serialized = JsonConvert.SerializeObject(mapped);
				if (type is "raw")
					return serialized;

				return 
					"""{"Changes":[{"LogName":"Register Upgrades","Action":"EditData","Target":"Mods/tlitookilakin.mus/Upgrades","Entries":""" +
					serialized + """}]}""";
			}
			
			if (type is "field")
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
