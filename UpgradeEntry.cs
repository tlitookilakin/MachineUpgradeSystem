using Newtonsoft.Json;

namespace MachineUpgradeSystem
{
	public class UpgradeEntry
	{
		public string ItemId { get; set; } = "(BC)0";

		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public string? Condition { get; set; }

		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public string? FailureMessage { get; set; }

		public UpgradeEntry() { }

		public UpgradeEntry(string id)
		{
			ItemId = id;
		}
	}
}
