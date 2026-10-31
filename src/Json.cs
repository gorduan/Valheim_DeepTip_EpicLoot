using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace DeepTipEpicLoot;

/// <summary>
/// Small JSON reader and string quoting: objects, arrays, strings and literals, enough for EpicLoot's language files
/// and this mod's effect texts. Numbers and literals stay strings.
/// </summary>
internal static class Json
{
	internal static Dictionary<string, object?> Parse(string text)
	{
		int i = 0;
		object? value = Value(text.TrimStart('﻿'), ref i);
		return value as Dictionary<string, object?> ?? new Dictionary<string, object?>();
	}

	private static void Skip(string s, ref int i)
	{
		while (i < s.Length && char.IsWhiteSpace(s[i]))
		{
			i++;
		}
	}

	private static object? Value(string s, ref int i)
	{
		Skip(s, ref i);
		if (i >= s.Length)
		{
			return null;
		}
		switch (s[i])
		{
			case '{':
				return Object(s, ref i);
			case '[':
				return Array(s, ref i);
			case '"':
				return String(s, ref i);
		}
		int start = i;
		while (i < s.Length && ",}] \t\r\n".IndexOf(s[i]) < 0)
		{
			i++;
		}
		string literal = s.Substring(start, i - start);
		return literal == "null" ? null : literal;
	}

	private static Dictionary<string, object?> Object(string s, ref int i)
	{
		Dictionary<string, object?> result = new();
		i++; // {
		while (true)
		{
			Skip(s, ref i);
			if (i >= s.Length || s[i] == '}')
			{
				i++;
				return result;
			}
			if (s[i] == ',')
			{
				i++;
				continue;
			}
			string key = String(s, ref i);
			Skip(s, ref i);
			i++; // :
			result[key] = Value(s, ref i);
		}
	}

	private static List<object?> Array(string s, ref int i)
	{
		List<object?> result = new();
		i++; // [
		while (true)
		{
			Skip(s, ref i);
			if (i >= s.Length || s[i] == ']')
			{
				i++;
				return result;
			}
			if (s[i] == ',')
			{
				i++;
				continue;
			}
			result.Add(Value(s, ref i));
		}
	}

	private static string String(string s, ref int i)
	{
		StringBuilder sb = new();
		i++; // "
		while (i < s.Length && s[i] != '"')
		{
			char c = s[i++];
			if (c != '\\' || i >= s.Length)
			{
				sb.Append(c);
				continue;
			}
			char e = s[i++];
			switch (e)
			{
				case 'n': sb.Append('\n'); break;
				case 'r': sb.Append('\r'); break;
				case 't': sb.Append('\t'); break;
				case 'b': sb.Append('\b'); break;
				case 'f': sb.Append('\f'); break;
				case 'u':
					if (i + 4 <= s.Length && int.TryParse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int code))
					{
						sb.Append((char)code);
						i += 4;
					}
					break;
				default: sb.Append(e); break;
			}
		}
		i++; // "
		return sb.ToString();
	}

	/// <summary>A JSON string literal.</summary>
	internal static string Quote(string value)
	{
		StringBuilder sb = new("\"");
		foreach (char c in value)
		{
			switch (c)
			{
				case '"': sb.Append("\\\""); break;
				case '\\': sb.Append("\\\\"); break;
				case '\n': sb.Append("\\n"); break;
				case '\r': break;
				case '\t': sb.Append("\\t"); break;
				default:
					if (c < ' ')
					{
						sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
					}
					else
					{
						sb.Append(c);
					}
					break;
			}
		}
		return sb.Append('"').ToString();
	}
}
