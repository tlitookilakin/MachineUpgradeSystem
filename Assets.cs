using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley.GameData.Objects;

namespace MachineUpgradeSystem
{
	public class Assets
	{
		public static Dictionary<string, Dictionary<string, string>> Data
			=> _data ??= Helper.GameContent.Load<Dictionary<string, Dictionary<string, string>>>(DataPath);
		private static Dictionary<string, Dictionary<string, string>>? _data;

		private static IAssetName DataPath;
		private static IAssetName ObjectData;
		private static IAssetName ItemSheetPath;
		private static IAssetName ObjectRecipes;

		private static IModHelper Helper;
		private static IMonitor Monitor;

		private static Dictionary<string, string>? recipeRequirements;

		const string MOD_ID = "tlitookilakin.mus";
		const string prefix = MOD_ID + "_";

		internal static void Init(IModHelper helper, IMonitor monitor)
		{
			Helper = helper;
			Monitor = monitor;

			DataPath = Helper.GameContent.ParseAssetName("Mods/" + MOD_ID + "/Upgrades");
			ObjectData = Helper.GameContent.ParseAssetName("Data/Objects");
			ItemSheetPath = Helper.GameContent.ParseAssetName("Mods/" + MOD_ID + "/Items");
			ObjectRecipes = Helper.GameContent.ParseAssetName("Data/CraftingRecipes");

			Helper.Events.Content.AssetsInvalidated += OnInvalidate;
			Helper.Events.Content.AssetRequested += OnRequested;
		}

		internal static void RequireReload()
		{
			Helper.GameContent.InvalidateCache(ObjectData);
			Helper.GameContent.InvalidateCache(ObjectRecipes);
		}

		private static void OnRequested(object? sender, AssetRequestedEventArgs e)
		{
			if (e.NameWithoutLocale.Equals(DataPath))
				e.LoadFromModFile<Dictionary<string, Dictionary<string, string>>>("assets/upgrades.json", AssetLoadPriority.Medium);
			else if (e.NameWithoutLocale.Equals(ObjectData))
				e.Edit(AddItems, AssetEditPriority.Default);
			else if (e.NameWithoutLocale.Equals(ItemSheetPath))
				e.LoadFromModFile<Texture2D>("assets/items.png", AssetLoadPriority.Medium);
			else if (e.NameWithoutLocale.Equals(ObjectRecipes))
				e.Edit(RemoveRecipes, (AssetEditPriority)10);
		}

		private static void OnInvalidate(object? sender, AssetsInvalidatedEventArgs e)
		{
			if (e.NamesWithoutLocale.Contains(DataPath))
				_data = null;
		}

		private static void AddItems(IAssetData asset)
		{

			if (asset.Data is not Dictionary<string, ObjectData> itemData)
			{
				Monitor.Log("Failed to edit object data: unexpected datatype", LogLevel.Error);
				return;
			}

			// must be regenerated to keep data fresh, since values are shared
			var augments = ReadJson<Dictionary<string, ObjectData>>("assets", "items.json");

			if (augments is null)
				return;

			foreach ((var id, var item) in augments)
			{
				item.Texture = ItemSheetPath.BaseName;
				item.Category = -17;
				item.Type = "Basic";
				item.DisplayName = Helper.Translation.Get($"item.{id}.name");
				item.Description = Helper.Translation.Get($"item.{id}.desc");
				item.CanBeGivenAsGift = false;
				item.ExcludeFromShippingCollection = true;
				itemData[prefix + id] = item;
			}
		}

		private static void RemoveRecipes(IAssetData asset)
		{
			if (asset.Data is not Dictionary<string, string> recipes)
			{
				Monitor.Log("Failed to edit recipe data: unexpected datatype", LogLevel.Error);
				return;
			}

			foreach ((var tier, var ingredient) in Config.config.Recipes)
				recipes[prefix + tier] = $"{ingredient.ItemId} {ingredient.Count}/Field/{prefix + tier} {ingredient.ResultCount}/false/default/";
		}

		private static T ReadJson<T>(params string[] path) where T : notnull
		{
			var file = Path.Join(path);
			return Helper.ModContent.Load<T>(file);
		}
	}
}
