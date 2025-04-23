using MachineUpgradeSystem.Framework;
using StardewValley.ItemTypeDefinitions;
using System.Diagnostics.CodeAnalysis;

namespace MachineUpgradeSystem.Integration
{
    public class API : IMachineUpgradeAPI
    {
        public IReadOnlyList<string> GetUpgrades(string itemId)
        {
            return
                Assets.UpgradeCache.TryGetValue(itemId, out var upgrades) ?
                upgrades : [];
        }

        public bool HasUpgrades(string itemId)
        {
            return Assets.UpgradeCache.ContainsKey(itemId);
        }

        public bool TryGetUpgradeItem(string itemId, [NotNullWhen(true)] out ParsedItemData? upgrade, int which = -1)
        {
            return Assets.TryGetIcon(itemId, out upgrade, which);
        }
    }
}
