using StardewModdingAPI;

namespace MachineUpgradeSystem
{
	public class ModEntry : Mod
	{
		private const string command_desc = "Generates upgrade files. Arguments: <modid> <type> <output> ( [tier_prefix] [tier_item] )+ . <type> can be 'raw' (plain json), 'entry' (CP patch), or 'field' (CP patch using TargetFields). output is the output file. If not specified, tier will default to the built-in tiers that come with MUS.";

		public override void Entry(IModHelper helper)
		{
			Config.Init(helper, ModManifest);
			Assets.Init(helper, Monitor);
			DataGen.Init(Monitor, helper);
			
			try
			{
				Patches.Patch(ModManifest, Monitor, helper);
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

			DataGen.EntryType type = args[1] switch
			{
				"raw" => DataGen.EntryType.Raw,
				"entry" => DataGen.EntryType.Entry,
				"field" => DataGen.EntryType.Field,
				_ => DataGen.EntryType.Error
			};

			if (type is DataGen.EntryType.Error)
			{
				Monitor.Log("Invalid type specified: must be 'raw', 'entry', or 'field'.");
				return;
			}

			DataGen.GenerateJson(args[0], type, args[2], args.Length > 3 ? args[3..] : null);
		}
	}
}
