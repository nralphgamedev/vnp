#if TOOLS
using Godot;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

/// <summary>
/// A tuning variant: a short text file of changed tuning values, applied in memory on top of the tuning files when
/// a fight starts, without editing them. Batches use variants to compare tunings side by side on the same fights.
/// Editor only, like the cheats.
///
/// Variants live in tools/variants/, one file per variant, named NAME.txt. One change per line:
///   levels.ExperienceForFirstLevel = 120             a value in res://tuning/levels.tres
///   enemies/goo/goo.tres:Health = 80                 a value in any .tres file, by its path from the project
/// Blank lines and lines starting with # are ignored. A value is read as the same kind (number, whole number,
/// true or false, or text) as the value it replaces.
/// </summary>
public static class TuningVariant
{
	/// <summary>The files a variant changed, held so Godot's resource cache can't drop them and load them again
	/// unchanged.</summary>
	private static readonly List<Resource> Changed = new();

	/// <summary>The folder variants are in, from the project folder.</summary>
	public const string Folder = "tools/variants";

	/// <summary>
	/// Applies the variant with this name. Returns its text, to fingerprint the tuning with, or null if it
	/// couldn't be applied; every problem is reported as an error.
	/// </summary>
	public static string Apply(string name)
	{
		string path = Path.Combine(ProjectSettings.GlobalizePath("res://"), Folder, name + ".txt");
		if (!File.Exists(path))
		{
			GD.PushError($"Tuning variant: no file {Folder}/{name}.txt");
			return null;
		}
		string text = File.ReadAllText(path);
		int problems = 0;
		int lineNumber = 0;
		foreach (string rawLine in text.Split('\n'))
		{
			lineNumber++;
			string line = rawLine.Trim();
			if (line.Length == 0 || line.StartsWith('#'))
				continue;
			if (!ApplyLine(line, out string problem))
			{
				GD.PushError($"Tuning variant {name}, line {lineNumber} ({line}): {problem}");
				problems++;
			}
		}
		if (problems == 0)
		{
			GD.Print($"TUNING VARIANT: {name} applied");
			return text;
		}
		GD.PushError($"Tuning variant {name}: {problems} line(s) couldn't be applied; the rest were.");
		return null;
	}

	private static bool ApplyLine(string line, out string problem)
	{
		problem = null;
		int equals = line.IndexOf('=');
		if (equals < 0)
		{
			problem = "expected NAME = VALUE";
			return false;
		}
		string target = line[..equals].Trim();
		string value = line[(equals + 1)..].Trim();

		// "file.Value" for res://tuning/file.tres, or "path/to/file.tres:Value" for any file.
		string resourcePath, property;
		int colon = target.LastIndexOf(':');
		if (colon >= 0)
		{
			resourcePath = "res://" + target[..colon].Trim().TrimStart('/');
			property = target[(colon + 1)..].Trim();
		}
		else
		{
			int dot = target.IndexOf('.');
			if (dot < 0)
			{
				problem = "name the value as file.Value or path/file.tres:Value";
				return false;
			}
			resourcePath = $"res://tuning/{target[..dot]}.tres";
			property = target[(dot + 1)..];
		}

		Resource resource = ResourceLoader.Exists(resourcePath) ? GD.Load<Resource>(resourcePath) : null;
		if (resource == null)
		{
			problem = $"no file {resourcePath}";
			return false;
		}
		Variant current = resource.Get(property);
		Variant changed;
		switch (current.VariantType)
		{
			case Variant.Type.Float when float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float f):
				changed = f;
				break;
			case Variant.Type.Int when int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i):
				changed = i;
				break;
			case Variant.Type.Bool when bool.TryParse(value, out bool b):
				changed = b;
				break;
			case Variant.Type.String:
			case Variant.Type.StringName:
				changed = value.Trim('"');
				break;
			case Variant.Type.Nil:
				problem = $"{resourcePath} has no value named {property}";
				return false;
			default:
				problem = $"{property} is a {current.VariantType}, which a variant can't change, or {value} isn't one";
				return false;
		}
		resource.Set(property, changed);
		Changed.Add(resource);
		return true;
	}
}
#endif
