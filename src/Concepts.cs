using System.IO;
using System.Reflection;

namespace DeepTipEpicLoot;

/// <summary>The general EpicLoot concepts (glossary\begriffe.de.json and .en.json), registered once at start.</summary>
internal static class Concepts
{
	private const string Owner = "DeepTip_EpicLoot.Begriffe";

	internal static void Register()
	{
		foreach (string locale in new[] { "de", "en" })
		{
			string? json = Resource("begriffe." + locale + ".json");
			if (json == null)
			{
				Plugin.Log.LogWarning($"begriffe.{locale}.json fehlt in der DLL");
				continue;
			}
			DeepTip.Api.Register(Owner, json, locale);
		}
	}

	/// <summary>An embedded file of this mod (LogicalName DeepTipEpicLoot.&lt;file&gt;), or null.</summary>
	internal static string? Resource(string file)
	{
		using Stream? stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("DeepTipEpicLoot." + file);
		if (stream == null)
		{
			return null;
		}
		using StreamReader reader = new(stream);
		return reader.ReadToEnd();
	}
}
