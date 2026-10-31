using System;
using System.Reflection;
using System.Text.RegularExpressions;
using EpicLoot;
using HarmonyLib;

namespace DeepTipEpicLoot;

/// <summary>
/// Links every effect line of EpicLoot's item tooltip to its explanation, rare effects included. EpicLoot draws a rare
/// effect inside &lt;link="elrare1"&gt;…&lt;/link&gt; so the line can shimmer („Effect Rarity Flare = Animated“, the default), and
/// DeepTip never links inside another link: with link words alone exactly the starred lines (e.g. Duelist ★) stayed
/// plain. While EpicLoot renders the effect list, the decorated line becomes a DeepTip link on the effect text, followed
/// by the stars, which keep their colour and shimmer. The link is finished TMP rich text (DeepTip.Api.Render), so a
/// window that never runs DeepTip over its text still shows a coloured line and no raw markup.
/// </summary>
internal static class TooltipLinks
{
	private static readonly Regex Shimmer = new("^<link=\"(elrare\\d+)\">(.*)</link>$", RegexOptions.Singleline);
	private static readonly char[] Unsafe = { '[', ']', '|', '*', '`', '{', '}' };

	[ThreadStatic]
	private static int rendering;

	internal static void Patch(Harmony harmony)
	{
		try
		{
			Type? renderer = AccessTools.TypeByName("EpicLoot.TooltipRenderer");
			MethodInfo? render = renderer != null ? AccessTools.Method(renderer, "RenderEffects") : null;
			MethodInfo? decorate = AccessTools.Method(typeof(MagicEffectRarity), nameof(MagicEffectRarity.Decorate), new[] { typeof(string), typeof(string), typeof(bool) });
			if (render == null || decorate == null)
			{
				Plugin.Log.LogWarning("EpicLoot-Effektzeilen nicht direkt verlinkt (TooltipRenderer.RenderEffects oder MagicEffectRarity.Decorate fehlt), DeepTip verlinkt nur die Effektnamen.");
				return;
			}
			harmony.Patch(render, prefix: new HarmonyMethod(typeof(TooltipLinks), nameof(Enter)), finalizer: new HarmonyMethod(typeof(TooltipLinks), nameof(Leave)));
			harmony.Patch(decorate, postfix: new HarmonyMethod(typeof(TooltipLinks), nameof(Link)));
		}
		catch (Exception e)
		{
			Plugin.Log.LogWarning($"EpicLoot-Effektzeilen nicht direkt verlinkt: {e.Message}");
		}
	}

	private static void Enter() => rendering++;

	private static Exception? Leave(Exception? __exception)
	{
		rendering--;
		return __exception;
	}

	/// <summary>Only inside RenderEffects: the enchanting table decorates the same lines and never runs DeepTip over them.</summary>
	private static void Link(string effectType, string effectText, ref string __result)
	{
		if (rendering <= 0 || string.IsNullOrEmpty(effectType) || string.IsNullOrEmpty(effectText) || __result == null)
		{
			return;
		}
		string key = EffectGlossary.Key(effectType);
		string label = EffectGlossary.Plain(effectText);
		if (label.Length == 0 || label.IndexOfAny(Unsafe) >= 0 || !DeepTip.Api.Has(key))
		{
			return;
		}
		string link = DeepTip.Api.Render("[[" + key + "|" + label + "]]");
		if (__result.StartsWith(effectText, StringComparison.Ordinal))
		{
			__result = link + __result.Substring(effectText.Length); // no stars, or stars without shimmer
			return;
		}
		Match shimmer = Shimmer.Match(__result);
		if (shimmer.Success && shimmer.Groups[2].Value.StartsWith(effectText, StringComparison.Ordinal))
		{
			__result = link + "<link=\"" + shimmer.Groups[1].Value + "\">" + shimmer.Groups[2].Value.Substring(effectText.Length) + "</link>";
		}
	}
}
