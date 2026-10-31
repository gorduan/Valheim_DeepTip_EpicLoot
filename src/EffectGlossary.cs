using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using EpicLoot;
using UnityEngine;

namespace DeepTipEpicLoot;

/// <summary>
/// One DeepTip entry per EpicLoot magic effect (key epicloot.effect.&lt;type&gt;), in German and English, built from the
/// definitions EpicLoot has loaded right now (values per rarity, requirements and exclusions of the server's config) and
/// the explanations checked against EpicLoot's code (data\effekte.*.json: what the value means, when it applies, how
/// values from several items combine, where EpicLoot's own description is wrong). Names come from EpicLoot's own German
/// and English language files, so the aliases match what the tooltips show; DeepTip links them there.
/// </summary>
internal static class EffectGlossary
{
	private const string Owner = "DeepTip_EpicLoot.Effekte";
	private static readonly Regex Placeholder = new(@"\{[^}]*\}");
	private static readonly Regex Tags = new("<[^>]+>");
	private static readonly Regex Spaces = new(@"\s+");
	private static readonly Regex Brackets = new(@"\([^)]*\)");
	private static readonly char[] NameTrim = { ' ', '+', '-', '%', ':', '(', ')', '[', ']', '/', ',', '.', '0', '1', '2', '3', '4', '5', '6', '7', '8', '9' };
	private static readonly Regex Markup = new(@"\*\*|\[\[(?:[^\]|]+\|)?([^\]]+)\]\]");

	private static Dictionary<string, object?>? epicLootDe;
	private static Dictionary<string, object?>? epicLootEn;
	private static Dictionary<string, object?>? textsDe;
	private static Dictionary<string, object?>? textsEn;

	internal static void Rebuild()
	{
		try
		{
			epicLootDe ??= EpicLootLanguage("German");
			epicLootEn ??= EpicLootLanguage("English");
			textsDe ??= Json.Parse(Concepts.Resource("effekte.de.json") ?? "{}");
			textsEn ??= Json.Parse(Concepts.Resource("effekte.en.json") ?? "{}");
			List<MagicItemEffectDefinition> defs = MagicItemEffectDefinitions.AllDefinitions.Values.Where(d => d != null).OrderBy(d => d.Type, StringComparer.Ordinal).ToList();
			DeepTip.Api.Unregister(Owner);
			DeepTip.Api.Register(Owner, Build(Lang.De, defs, epicLootDe, textsDe), "de");
			DeepTip.Api.Register(Owner, Build(Lang.En, defs, epicLootEn, textsEn), "en");
			int checkedTexts = defs.Count(d => textsDe.ContainsKey(d.Type));
			Plugin.Log.LogInfo($"{defs.Count} EpicLoot-Effekte erklärt, davon {checkedTexts} mit am Code geprüftem Text");
		}
		catch (Exception e)
		{
			Plugin.Log.LogError($"EpicLoot-Effekte nicht erklärt: {e}");
		}
	}

	internal static string Key(string type) => "epicloot.effect." + type.ToLowerInvariant();

	/// <summary>EpicLoot's language file (embedded in EpicLoot.dll as EpicLoot.localizations.&lt;Language&gt;.json).</summary>
	private static Dictionary<string, object?> EpicLootLanguage(string language)
	{
		using System.IO.Stream? stream = typeof(MagicItemEffectDefinitions).Assembly.GetManifestResourceStream($"EpicLoot.localizations.{language}.json");
		if (stream == null)
		{
			Plugin.Log.LogWarning($"EpicLoot hat keine Sprachdatei {language}");
			return new Dictionary<string, object?>();
		}
		using System.IO.StreamReader reader = new(stream);
		return Json.Parse(reader.ReadToEnd());
	}

	private static string Build(Lang lang, List<MagicItemEffectDefinition> defs, Dictionary<string, object?> epicLoot, Dictionary<string, object?> texts)
	{
		Dictionary<string, string> titles = defs.GroupBy(d => d.Type).ToDictionary(g => g.Key, g => Title(Display(g.First(), epicLoot)));
		Dictionary<string, List<string>> candidates = defs.GroupBy(d => d.Type).ToDictionary(g => g.Key, g => Aliases(g.First(), Display(g.First(), epicLoot), epicLoot));
		Dictionary<string, string> owners = Owners(candidates);
		StringBuilder json = new("{\n\"$locale\": ");
		json.Append(Json.Quote(lang.Code));
		List<string> see = new[] { "epicloot.rarity", "epicloot.rollrange", "epicloot.stars" }.Where(DeepTip.Api.Has).ToList();
		foreach (MagicItemEffectDefinition def in defs)
		{
			string display = Display(def, epicLoot);
			Dictionary<string, object?>? text = texts.TryGetValue(def.Type, out object? found) ? found as Dictionary<string, object?> : null;
			string exact = Field(text, "text");
			string deviation = Field(text, "abweichung");
			string main = exact.Length > 0 ? exact : Plain(Lookup(epicLoot, DescriptionKey(def)) ?? "");
			List<string> body = new();
			if (main.Length > 0)
			{
				body.Add(main);
			}
			if (deviation.Length > 0)
			{
				body.Add($"**{lang.Note}** {deviation}");
			}
			if (Values(def, display, lang) is string values)
			{
				body.Add(values);
			}
			body.AddRange(Requirements(def, lang, titles));
			if (TableFlags(def, lang) is string table)
			{
				body.Add(table);
			}
			json.Append(",\n").Append(Json.Quote(Key(def.Type))).Append(": {\"title\": ").Append(Json.Quote(titles[def.Type]));
			json.Append(", \"short\": ").Append(Json.Quote(Short(main, titles[def.Type])));
			json.Append(", \"body\": ").Append(Json.Quote(string.Join("\n\n", body)));
			List<string> aliases = candidates[def.Type].Where(a => owners.TryGetValue(a, out string? owner) && owner == def.Type).ToList();
			if (aliases.Count > 0)
			{
				json.Append(", \"aliases\": [").Append(string.Join(", ", aliases.Select(Json.Quote))).Append(']');
			}
			if (see.Count > 0)
			{
				json.Append(", \"see\": [").Append(string.Join(", ", see.Select(Json.Quote))).Append(']');
			}
			json.Append('}');
		}
		return json.Append("\n}").ToString();
	}

	// ------------------------------------------------------------------ Texte aus EpicLoot

	private static string? Lookup(Dictionary<string, object?> table, string key) =>
		table.TryGetValue(key.TrimStart('$'), out object? value) ? value as string : null;

	private static string DisplayKey(MagicItemEffectDefinition def) =>
		string.IsNullOrEmpty(def.DisplayText) ? "mod_epicloot_me_" + def.Type.ToLowerInvariant() + "_display" : def.DisplayText.TrimStart('$');

	private static string DescriptionKey(MagicItemEffectDefinition def)
	{
		if (!string.IsNullOrEmpty(def.Description))
		{
			return def.Description.TrimStart('$');
		}
		string display = DisplayKey(def);
		return (display.EndsWith("_display") ? display.Substring(0, display.Length - "_display".Length) : display) + "_desc";
	}

	/// <summary>The effect line as EpicLoot shows it, with its value placeholder ({0:0.#} …).</summary>
	private static string Display(MagicItemEffectDefinition def, Dictionary<string, object?> table)
	{
		string? text = Lookup(table, DisplayKey(def));
		if (string.IsNullOrEmpty(text) && Localization.instance != null && !string.IsNullOrEmpty(def.DisplayText))
		{
			string localized = Localization.instance.Localize(def.DisplayText);
			text = localized.StartsWith("[") ? null : localized;
		}
		return string.IsNullOrEmpty(text) ? def.Type : text!;
	}

	/// <summary>Rich text and line breaks out, single spaces.</summary>
	internal static string Plain(string text) => Spaces.Replace(Tags.Replace(text, ""), " ").Trim();

	/// <summary>„Duellist +X%“: the value placeholder becomes X.</summary>
	private static string Title(string display) => Spaces.Replace(Placeholder.Replace(Plain(display), "X"), " ").Trim();

	/// <summary>
	/// Words that DeepTip links outside the effect lines (those link directly, see TooltipLinks): the effect's name in its
	/// line (e.g. „Duellist“), and the name parts EpicLoot builds item names from (e.g. „Duellanten …“, „… des Duellierens“).
	/// </summary>
	private static List<string> Aliases(MagicItemEffectDefinition def, string display, Dictionary<string, object?> table)
	{
		List<string> aliases = new();
		Add(aliases, Name(display));
		string display0 = DisplayKey(def);
		string stem = display0.EndsWith("_display") ? display0.Substring(0, display0.Length - "_display".Length) : display0;
		for (int i = 1; i <= 9; i++)
		{
			Add(aliases, Lookup(table, $"{stem}_prefix{i}"));
			Add(aliases, Lookup(table, $"{stem}_suffix{i}"));
		}
		return aliases;
	}

	/// <summary>
	/// The effect's name in its line: the text before the value or a colon, without parts in brackets („Rüstung +X%
	/// (unter Y% Gesundheit)“ → „Rüstung“, „Frostkegel: +X% Waffenschaden“ → „Frostkegel“). A line that starts with its
	/// value („+X% Feuerschaden“) gives its longest part without the value. EpicLoot 0.15.5's German lines made the old
	/// rule (always the longest part) pick words like „Sekunden“ or „Gesundheit“.
	/// </summary>
	private static string Name(string display)
	{
		string plain = Brackets.Replace(Plain(display), " ");
		int value = plain.IndexOf('{');
		string name = value >= 0 ? plain.Substring(0, value) : plain;
		int colon = name.IndexOf(':');
		name = (colon > 0 ? name.Substring(0, colon) : name).Trim(NameTrim);
		return name.Length > 0 ? name : Placeholder.Split(plain).Select(p => p.Trim(NameTrim)).OrderByDescending(p => p.Length).FirstOrDefault() ?? "";
	}

	/// <summary>
	/// Which effect each link word belongs to. A word several effects share links nowhere („Geschwindigkeit“), except a word
	/// only an effect and its low-health variant share, which goes to the plain effect („Lebensraub“ → LifeSteal).
	/// </summary>
	private static Dictionary<string, string> Owners(Dictionary<string, List<string>> candidates)
	{
		Dictionary<string, string> owners = new(StringComparer.OrdinalIgnoreCase);
		foreach (IGrouping<string, string> word in candidates.SelectMany(c => c.Value.Select(alias => (alias, type: c.Key))).GroupBy(x => x.alias, x => x.type, StringComparer.OrdinalIgnoreCase))
		{
			List<string> types = word.Distinct().ToList();
			string? plain = types.Count == 2 ? types.FirstOrDefault(t => types.Contains(t + "LowHealth")) : null;
			if (types.Count == 1 || plain != null)
			{
				owners[word.Key] = plain ?? types[0];
			}
		}
		return owners;
	}

	private static void Add(List<string> aliases, string? alias)
	{
		if (alias == null)
		{
			return;
		}
		string clean = Plain(alias);
		if (clean.Length >= 4 && !aliases.Contains(clean, StringComparer.OrdinalIgnoreCase))
		{
			aliases.Add(clean);
		}
	}

	private static string Field(Dictionary<string, object?>? text, string name) =>
		text != null && text.TryGetValue(name, out object? value) && value is string s ? s.Trim() : "";

	/// <summary>First sentence without markup, at most 140 characters (DeepTip's one-line summary).</summary>
	private static string Short(string main, string title)
	{
		string plain = Markup.Replace(main, m => m.Groups[1].Success ? m.Groups[1].Value : "");
		int end = plain.IndexOf(". ", StringComparison.Ordinal);
		string first = (end > 0 ? plain.Substring(0, end + 1) : plain).Trim();
		if (first.Length == 0)
		{
			return title;
		}
		return first.Length <= 140 ? first : first.Substring(0, 137).TrimEnd() + "…";
	}

	// ------------------------------------------------------------------ Werte und Bedingungen

	private static string? Values(MagicItemEffectDefinition def, string display, Lang lang)
	{
		if (MagicItemEffectDefinitions.IsValuelessEffect(def.Type) || def.ValuesPerRarity == null)
		{
			return null;
		}
		MagicItemEffectDefinition.ValuesPerRarityDef v = def.ValuesPerRarity;
		MagicItemEffectDefinition.ValueDef?[] all = { v.Magic, v.Rare, v.Epic, v.Legendary, v.Mythic, v.Ancient };
		bool percent = display.Contains("%");
		List<string> parts = new();
		float? step = null;
		bool sameStep = true;
		for (int r = 0; r < all.Length; r++)
		{
			if (all[r] is not MagicItemEffectDefinition.ValueDef d)
			{
				continue;
			}
			string range = Mathf.Approximately(d.MinValue, d.MaxValue) ? Num(d.MinValue, lang) : Num(d.MinValue, lang) + "–" + Num(d.MaxValue, lang);
			parts.Add(lang.Rarities[r] + " " + range + (percent ? lang.Percent : ""));
			if (step == null)
			{
				step = d.Increment;
			}
			else if (!Mathf.Approximately(step.Value, d.Increment))
			{
				sameStep = false;
			}
		}
		if (parts.Count == 0)
		{
			return null;
		}
		string line = $"**{lang.Values}** " + string.Join(", ", parts);
		if (sameStep && step is float s && s > 0f && !Mathf.Approximately(s, 1f))
		{
			line += " (" + string.Format(lang.Steps, Num(s, lang)) + ")";
		}
		return line + ".";
	}

	private static string Num(float value, Lang lang) => value.ToString("0.##", lang.Culture);

	private static IEnumerable<string> Requirements(MagicItemEffectDefinition def, Lang lang, Dictionary<string, string> titles)
	{
		MagicItemEffectRequirements? q = def.Requirements;
		if (q == null)
		{
			yield break;
		}
		if (q.NoRoll)
		{
			yield return lang.NoRoll;
		}
		List<string> on = new();
		if (q.AllowedItemTypes?.Count > 0)
		{
			on.Add(string.Join(", ", q.AllowedItemTypes.Select(lang.ItemType)));
		}
		if (q.AllowedSkillTypes?.Count > 0)
		{
			on.Add(lang.SkillPrefix + string.Join(", ", q.AllowedSkillTypes.Select(t => lang.Skill(t.ToString()))));
		}
		if (q.AllowedItemNames?.Count > 0)
		{
			on.Add(string.Join(", ", q.AllowedItemNames.Select(ItemName)));
		}
		if (q.AllowedRarities?.Count > 0)
		{
			on.Add(lang.RarityPrefix + string.Join(", ", q.AllowedRarities.Select(r => lang.Rarities[(int)r])));
		}
		foreach (FieldInfo field in typeof(MagicItemEffectRequirements).GetFields(BindingFlags.Public | BindingFlags.Instance))
		{
			if (field.FieldType == typeof(bool?) && field.GetValue(q) is bool flag)
			{
				on.Add(lang.Flag(field.Name, flag));
			}
		}
		if (on.Count > 0)
		{
			yield return $"**{lang.Appears}** " + string.Join("; ", on) + ".";
		}
		List<string> not = new();
		if (q.ExcludedItemTypes?.Count > 0)
		{
			not.Add(string.Join(", ", q.ExcludedItemTypes.Select(lang.ItemType)));
		}
		if (q.ExcludedSkillTypes?.Count > 0)
		{
			not.Add(lang.SkillPrefix + string.Join(", ", q.ExcludedSkillTypes.Select(t => lang.Skill(t.ToString()))));
		}
		if (q.ExcludedItemNames?.Count > 0)
		{
			not.Add(string.Join(", ", q.ExcludedItemNames.Select(ItemName)));
		}
		if (q.ExcludedRarities?.Count > 0)
		{
			not.Add(lang.RarityPrefix + string.Join(", ", q.ExcludedRarities.Select(r => lang.Rarities[(int)r])));
		}
		if (not.Count > 0)
		{
			yield return $"**{lang.NotOn}** " + string.Join("; ", not) + ".";
		}
		if (q.ExclusiveEffectTypes?.Count > 0)
		{
			yield return $"**{lang.NotWith}** " + string.Join(", ", q.ExclusiveEffectTypes.Select(t => Link(t, titles))) + ".";
		}
		if (q.MustHaveEffectTypes?.Count > 0)
		{
			yield return $"**{lang.OnlyWith}** " + string.Join(", ", q.MustHaveEffectTypes.Select(t => Link(t, titles))) + ".";
		}
		if (!q.ExclusiveSelf)
		{
			yield return lang.Multiple;
		}
	}

	private static string Link(string type, Dictionary<string, string> titles) =>
		titles.TryGetValue(type, out string? title) ? $"[[{Key(type)}|{title}]]" : type;

	/// <summary>Item name from Valheim (prefab name otherwise; ObjectDB may not exist yet when EpicLoot loads).</summary>
	private static string ItemName(string prefab)
	{
		GameObject? item = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(prefab) : null;
		string? token = item != null ? item.GetComponent<ItemDrop>()?.m_itemData.m_shared.m_name : null;
		return token != null && Localization.instance != null ? Localization.instance.Localize(token) : prefab;
	}

	private static string? TableFlags(MagicItemEffectDefinition def, Lang lang)
	{
		List<string> no = new();
		if (!def.CanBeAugmented)
		{
			no.Add(lang.NoAugment);
		}
		if (!def.CanBeDisenchanted)
		{
			no.Add(lang.NoDisenchant);
		}
		if (!def.CanBeRunified)
		{
			no.Add(lang.NoRune);
		}
		return no.Count == 0 ? null : $"**{lang.Table}** " + string.Join(", ", no) + ".";
	}

	// ------------------------------------------------------------------ Sprache

	private sealed class Lang
	{
		public string Code = "";
		public CultureInfo Culture = CultureInfo.InvariantCulture;
		public string Percent = "%";
		public string[] Rarities = new string[6];
		public string Values = "", Steps = "", Appears = "", NotOn = "", NotWith = "", OnlyWith = "", Multiple = "", NoRoll = "";
		public string Note = "", Table = "", NoAugment = "", NoDisenchant = "", NoRune = "", SkillPrefix = "", RarityPrefix = "";
		public Dictionary<string, string> ItemTypes = new();
		public Dictionary<string, string> Skills = new();
		public Dictionary<string, (string Yes, string No)> Flags = new();

		public string ItemType(string type) => ItemTypes.TryGetValue(type, out string? name) ? name : type;

		public string Skill(string skill) => Skills.TryGetValue(skill, out string? name) ? name : skill;

		public string Flag(string field, bool value) =>
			Flags.TryGetValue(field, out (string Yes, string No) text) ? (value ? text.Yes : text.No) : field + (value ? "" : " = false");

		internal static readonly Lang De = new()
		{
			Code = "de",
			Culture = CultureInfo.GetCultureInfo("de-DE"),
			Percent = " %",
			Rarities = new[] { "Magie", "Selten", "Episch", "Legendär", "Mythisch", "Uralt" },
			Values = "Werte je Seltenheit:",
			Steps = "in Schritten von {0}",
			Appears = "Kommt vor auf:",
			NotOn = "Nicht auf:",
			NotWith = "Nicht zusammen mit:",
			OnlyWith = "Nur zusammen mit:",
			Multiple = "Kann mehrfach auf demselben Item vorkommen.",
			NoRoll = "Wird nie zufällig ausgewürfelt; nur auf festen Stücken (z. B. legendären Items).",
			Note = "Abweichung von EpicLoots Beschreibung:",
			Table = "Am Verzauberungstisch:",
			NoAugment = "nicht verstärkbar",
			NoDisenchant = "nicht entzauberbar",
			NoRune = "nicht auf eine Rune übertragbar",
			SkillPrefix = "Waffen der Fertigkeit ",
			RarityPrefix = "nur Seltenheit ",
			ItemTypes = new()
			{
				["OneHandedWeapon"] = "Einhandwaffen", ["TwoHandedWeapon"] = "Zweihandwaffen", ["TwoHandedWeaponLeft"] = "Zweihandwaffen (linke Hand)",
				["Bow"] = "Bögen und Armbrüste", ["Shield"] = "Schilde", ["Helmet"] = "Helme", ["Chest"] = "Brustrüstungen", ["Legs"] = "Beinrüstungen",
				["Shoulder"] = "Umhänge", ["Utility"] = "Gürtel und Zubehör", ["Tool"] = "Werkzeuge", ["Torch"] = "Fackeln", ["Ammo"] = "Munition",
				["Trinket"] = "Schmuckstücke", ["Hands"] = "Handschuhe", ["Consumable"] = "Verbrauchsgüter", ["Material"] = "Materialien",
			},
			Skills = new()
			{
				["Swords"] = "Schwerter", ["Knives"] = "Messer", ["Clubs"] = "Keulen", ["Polearms"] = "Stangenwaffen", ["Spears"] = "Speere",
				["Blocking"] = "Blocken", ["Axes"] = "Äxte", ["Bows"] = "Bögen", ["Crossbows"] = "Armbrüste", ["ElementalMagic"] = "Elementarmagie",
				["BloodMagic"] = "Blutmagie", ["Unarmed"] = "Faustkampf", ["Pickaxes"] = "Spitzhacken", ["WoodCutting"] = "Holzfällen", ["Fishing"] = "Angeln",
			},
			Flags = new()
			{
				["ItemHasPhysicalDamage"] = ("Item macht physischen Schaden", "Item macht keinen physischen Schaden"),
				["ItemHasElementalDamage"] = ("Item macht Elementarschaden", "Item macht keinen Elementarschaden"),
				["ItemHasChopDamage"] = ("Item macht Hackschaden", "Item macht keinen Hackschaden"),
				["ItemUsesDurability"] = ("Item nutzt sich ab", "Item nutzt sich nicht ab"),
				["ItemHasNegativeMovementSpeedModifier"] = ("Item verlangsamt beim Tragen", "Item verlangsamt nicht"),
				["ItemHasBlockPower"] = ("Item hat Blockrüstung", "Item hat keine Blockrüstung"),
				["ItemHasParryPower"] = ("Item hat einen Parierbonus", "Item hat keinen Parierbonus"),
				["ItemHasNoParryPower"] = ("Item hat keinen Parierbonus", "Item hat einen Parierbonus"),
				["ItemHasArmor"] = ("Item hat Rüstung", "Item hat keine Rüstung"),
				["ItemHasBackstabBonus"] = ("Item hat einen Hinterhalt-Bonus", "Item hat keinen Hinterhalt-Bonus"),
				["ItemUsesStaminaOnAttack"] = ("Angriff kostet Ausdauer", "Angriff kostet keine Ausdauer"),
				["ItemUsesEitrOnAttack"] = ("Angriff kostet Eitr", "Angriff kostet kein Eitr"),
				["ItemUsesHealthOnAttack"] = ("Angriff kostet Leben", "Angriff kostet kein Leben"),
				["ItemUsesDrawStaminaOnAttack"] = ("Spannen kostet Ausdauer", "Spannen kostet keine Ausdauer"),
				["ItemGivesAdrenaline"] = ("Item gibt Adrenalin", "Item gibt kein Adrenalin"),
				["ItemHasAdrenaline"] = ("Item hat Adrenalin", "Item hat kein Adrenalin"),
			},
		};

		internal static readonly Lang En = new()
		{
			Code = "en",
			Culture = CultureInfo.InvariantCulture,
			Percent = "%",
			Rarities = new[] { "Magic", "Rare", "Epic", "Legendary", "Mythic", "Ancient" },
			Values = "Values per rarity:",
			Steps = "in steps of {0}",
			Appears = "Appears on:",
			NotOn = "Not on:",
			NotWith = "Not together with:",
			OnlyWith = "Only together with:",
			Multiple = "Can appear more than once on the same item.",
			NoRoll = "Never rolled at random; only on fixed pieces (e.g. legendary items).",
			Note = "Differs from EpicLoot's description:",
			Table = "At the enchanting table:",
			NoAugment = "cannot be augmented",
			NoDisenchant = "cannot be disenchanted",
			NoRune = "cannot be put on a rune",
			SkillPrefix = "weapons of the skill ",
			RarityPrefix = "only rarity ",
			ItemTypes = new()
			{
				["OneHandedWeapon"] = "one-handed weapons", ["TwoHandedWeapon"] = "two-handed weapons", ["TwoHandedWeaponLeft"] = "two-handed weapons (left hand)",
				["Bow"] = "bows and crossbows", ["Shield"] = "shields", ["Helmet"] = "helmets", ["Chest"] = "chest armor", ["Legs"] = "leg armor",
				["Shoulder"] = "capes", ["Utility"] = "belts and utility items", ["Tool"] = "tools", ["Torch"] = "torches", ["Ammo"] = "ammunition",
				["Trinket"] = "trinkets", ["Hands"] = "gloves", ["Consumable"] = "consumables", ["Material"] = "materials",
			},
			Skills = new()
			{
				["Swords"] = "Swords", ["Knives"] = "Knives", ["Clubs"] = "Clubs", ["Polearms"] = "Polearms", ["Spears"] = "Spears",
				["Blocking"] = "Blocking", ["Axes"] = "Axes", ["Bows"] = "Bows", ["Crossbows"] = "Crossbows", ["ElementalMagic"] = "Elemental Magic",
				["BloodMagic"] = "Blood Magic", ["Unarmed"] = "Unarmed", ["Pickaxes"] = "Pickaxes", ["WoodCutting"] = "Woodcutting", ["Fishing"] = "Fishing",
			},
			Flags = new()
			{
				["ItemHasPhysicalDamage"] = ("item deals physical damage", "item deals no physical damage"),
				["ItemHasElementalDamage"] = ("item deals elemental damage", "item deals no elemental damage"),
				["ItemHasChopDamage"] = ("item deals chop damage", "item deals no chop damage"),
				["ItemUsesDurability"] = ("item wears out", "item does not wear out"),
				["ItemHasNegativeMovementSpeedModifier"] = ("item slows you down", "item does not slow you down"),
				["ItemHasBlockPower"] = ("item has block armor", "item has no block armor"),
				["ItemHasParryPower"] = ("item has a parry bonus", "item has no parry bonus"),
				["ItemHasNoParryPower"] = ("item has no parry bonus", "item has a parry bonus"),
				["ItemHasArmor"] = ("item has armor", "item has no armor"),
				["ItemHasBackstabBonus"] = ("item has a backstab bonus", "item has no backstab bonus"),
				["ItemUsesStaminaOnAttack"] = ("attacks cost stamina", "attacks cost no stamina"),
				["ItemUsesEitrOnAttack"] = ("attacks cost eitr", "attacks cost no eitr"),
				["ItemUsesHealthOnAttack"] = ("attacks cost health", "attacks cost no health"),
				["ItemUsesDrawStaminaOnAttack"] = ("drawing costs stamina", "drawing costs no stamina"),
				["ItemGivesAdrenaline"] = ("item gives adrenaline", "item gives no adrenaline"),
				["ItemHasAdrenaline"] = ("item has adrenaline", "item has no adrenaline"),
			},
		};
	}
}
