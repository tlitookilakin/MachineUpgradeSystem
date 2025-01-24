namespace MachineUpgradeSystem
{
	public class UpgradeEntry
	{
		public string ItemId { get; set; } = "(BC)0";
		public string? Condition { get; set; }
		public string? FailureMessage { get; set; }
	}
}
