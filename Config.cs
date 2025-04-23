using MachineUpgradeSystem.Integration;
using StardewModdingAPI;

namespace MachineUpgradeSystem
{
    public class Config
	{
		public static Config config => _config ??= Helper.ReadConfig<Config>();
		private static Config? _config;
		private static IModHelper Helper;

		internal static void Init(IModHelper helper, IManifest man)
		{
			Helper = helper;
			config.Populate(helper.ModContent.Load<Dictionary<string, string>>("assets/recipes.json"));

			helper.Events.GameLoop.GameLaunched += (s, e) => config.Register(man);
		}

		public class Ingredient(string itemId, uint count, uint resultCount)
		{
			public string ItemId { get; set; } = itemId;
			public uint Count { get; set; } = count;
			public uint ResultCount { get; set; } = resultCount;

			public override string ToString()
			{
				return ItemId + ' ' + Count;
			}

			public Ingredient(string itemId) : this(itemId, 1, 1) { }

			public Ingredient() : this("(O)0") { }
		}

		public Dictionary<string, Ingredient> Recipes { get; set; } = new();

		public Config()
		{

		}

		internal void Populate(Dictionary<string, string> recipes)
		{
			foreach (var pair in recipes)
				Recipes.TryAdd(pair.Key, new(pair.Value));
		}

		public void Reset()
		{
			var data = Helper.ModContent.Load<Dictionary<string, string>>("assets/recipes.json");
			foreach (var pair in data)
			{
				if (Recipes.TryGetValue(pair.Key, out var recipe))
				{
					recipe.ResultCount = 1;
					recipe.Count = 1;
					recipe.ItemId = pair.Value;
				}
			}
		}

		public void Apply()
		{
			Helper.WriteConfig(this);
			Helper.GameContent.InvalidateCache("Data/CraftingRecipes");
		}

		internal void Register(IManifest man)
		{
			if (!Helper.ModRegistry.IsLoaded("spacechase0.GenericModConfigMenu"))
				return;

			var gmcm = Helper.ModRegistry.GetApi<IGMCM>("spacechase0.GenericModConfigMenu")!;

			gmcm.Register(man, Reset, Apply);

			foreach (var pair in Recipes)
				BindEntry(pair, gmcm, man);
		}

		private void BindEntry(KeyValuePair<string, Ingredient> entry, IGMCM gmcm, IManifest man)
		{
			(var key, var val) = entry;
			gmcm.AddSectionTitle(man,
				() => Helper.Translation.Get($"item.{key}.name"),
				() => Helper.Translation.Get($"item.{key}.desc")
			);
			gmcm.AddTextOption(man,
				() => val.ItemId,
				(v) => val.ItemId = v,
				() => Helper.Translation.Get($"cfg.itemid.name")
			);
			gmcm.AddNumberOption(man,
				() => (int)val.Count,
				(v) => val.Count = (uint)v,
				() => Helper.Translation.Get($"cfg.inputcount.name"),
				null, 1, 99
			);
			gmcm.AddNumberOption(man,
				() => (int)val.ResultCount,
				(v) => val.ResultCount = (uint)v,
				() => Helper.Translation.Get($"cfg.outputcount.name"),
				null, 1, 99
			);
		}
	}
}
