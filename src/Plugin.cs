using BepInEx;
using BepInEx.Logging;
using EpicLoot;
using HarmonyLib;

namespace DeepTipEpicLoot;

/// <summary>
/// DeepTip EpicLoot (Jan, 31.10.2026: „eine DeepTip-EpicLoot-Mod, in der alle EpicLoot-Eigenschaften innerhalb eines Items
/// genau erklärt werden, z. B. was bedeutet 8 % Duelist“). Registers a DeepTip glossary in German and English:
///  * general EpicLoot concepts (glossary\begriffe.*.json: rarity, stars, roll range, shard slots, runes, sets, the
///    enchanting table and its tabs, materials …), checked against EpicLoot's code and config;
///  * one entry per magic effect, rebuilt whenever EpicLoot (re)loads its effect definitions (also after the server sent
///    its config): what the value on the item means and what it does (data\effekte.*.json, checked against EpicLoot's
///    code), the value range per rarity, where it can appear, which effects exclude it, and where EpicLoot's own
///    description does not match the code.
/// Every effect line of an EpicLoot tooltip links to its explanation (TooltipLinks, also the starred rare lines), and
/// DeepTip highlights effect names and these terms elsewhere; resting the mouse opens the explanation.
/// </summary>
[BepInPlugin(Guid, Name, Version)]
[BepInDependency(DeepTip.Api.Guid)]
[BepInDependency(EpicLootGuid)]
public sealed class Plugin : BaseUnityPlugin
{
	public const string Guid = "gorduan.deeptip.epicloot";
	public const string Name = "DeepTip EpicLoot";
	public const string Version = "1.0.0";
	private const string EpicLootGuid = "randyknapp.mods.epicloot";

	internal static ManualLogSource Log = null!;

	private void Awake()
	{
		Log = Logger;
		TooltipLinks.Patch(new Harmony(Guid));
		Concepts.Register();
		MagicItemEffectDefinitions.OnSetupMagicItemEffectDefinitions += EffectGlossary.Rebuild;
		if (MagicItemEffectDefinitions.AllDefinitions.Count > 0)
		{
			EffectGlossary.Rebuild();
		}
	}
}
