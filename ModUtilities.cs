using StardewValley.GameData.BigCraftables;

namespace MachineUpgradeSystem
{
	internal static class ModUtilities
	{
		public static bool TryGetIdByName(string name, string modid, Dictionary<string, BigCraftableData> data, out string id)
		{
			id = $"{modid}_{name}";
			if (data.ContainsKey(id))
				return true;

			name = name.CamelToHuman();

			id = data.FirstOrDefault(p => p.Value.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).Key ?? "";
			return id.Length > 0;
		}

		public static string CamelToHuman(this string source)
		{
			Span<char> chars = stackalloc char[source.Length * 2];
			var src = source.AsSpan();

			int last = 0;
			int cursor = 0;

			for (int i = 0; i < src.Length; i++)
			{
				if (char.IsUpper(src[i]))
				{
					if (last < i)
					{
						src[last..i].CopyTo(chars[cursor..]);
						cursor += i - last;
					}

					if (i > 0)
						chars[cursor++] = ' ';
					chars[cursor++] = char.ToLowerInvariant(src[i]);
					last = i + 1;
				}
			}

			if (last < src.Length)
			{
				src[last..].CopyTo(chars[cursor..]);
				cursor += src.Length - last;
			}

			return new string(chars[..cursor]);
		}
	}
}
