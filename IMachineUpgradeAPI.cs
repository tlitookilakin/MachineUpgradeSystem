using StardewValley.ItemTypeDefinitions;
using System.Diagnostics.CodeAnalysis;

namespace MachineUpgradeSystem
{
    public interface IMachineUpgradeAPI
    {
        /// <summary>Gets whether or not the item can be upgraded</summary>
        /// <param name="itemId">The qualified item id to check</param>
        public bool HasUpgrades(string itemId);

        /// <summary>Gets a list of upgrades for an item</summary>
        /// <param name="itemId">The qualified item id to check</param>
        /// <returns>A list of qualified item ids for upgrades that can be applied, or an empty list if there are none.</returns>
        public IReadOnlyList<string> GetUpgrades(string itemId);

        /// <summary>Attempts to get item data for an upgrade which can be applied to the item</summary>
        /// <param name="itemId">The qualified item id to check</param>
        /// <param name="upgrade">The upgrade data, if at least one is found</param>
        /// <param name="which">
        /// The index to select, if multiple upgrades are available.<br/>
        /// Will be wrapped if it is greater than the number of upgrades available.<br/>
        /// If it is less than zero, it will be automatically cycled.
        /// </param>
        /// <returns></returns>
        public bool TryGetUpgradeItem(string itemId, [NotNullWhen(true)] out ParsedItemData? upgrade, int which = -1);
    }
}
