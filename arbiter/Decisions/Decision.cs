using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Arbiter
{
	/// <summary>Random numbers from the game, so a game whose randomness is seeded stays repeatable.</summary>
	public interface IRandom
	{
		/// <summary>A number from 0 up to, but not including, 1.</summary>
		float Value();
	}

	/// <summary>
	/// One kind of choice an agent makes, such as which way to move or what to pick: options, numbered from 0,
	/// scored by adding up weighted terms, the best taken. The terms are set up once; their weights can change from
	/// one choice to the next, as the agent's situation changes what matters. Each choice is kept, with every
	/// option's value for every term, so it can be explained, and fitted to someone else's choices later.
	/// </summary>
	public sealed class Decision
	{
		/// <summary>A term's value for one option. Higher is better; a cost is a term with a negative weight.</summary>
		public delegate float TermValue(int option);

		private readonly List<string> _names = new();
		private readonly List<TermValue> _values = new();
		private float[] _weights = Array.Empty<float>();

		// The last choice: each option's value for each term (option-major), its total, and which was taken.
		private float[] _table = Array.Empty<float>();
		private float[] _totals = Array.Empty<float>();
		private int[] _order = Array.Empty<int>();

		public string Name { get; }

		public Decision(string name) => Name = name;

		public int TermCount => _names.Count;
		public string TermName(int term) => _names[term];

		/// <summary>Adds a term, at weight 0, and returns its number.</summary>
		public int AddTerm(string name, TermValue value)
		{
			_names.Add(name);
			_values.Add(value);
			Array.Resize(ref _weights, _names.Count);
			return _names.Count - 1;
		}

		public void SetWeight(int term, float weight) => _weights[term] = weight;
		public float WeightOf(int term) => _weights[term];

		/// <summary>How many options the last choice had.</summary>
		public int OptionCount { get; private set; }
		/// <summary>The option the last choice took, or -1 if it had none.</summary>
		public int Chosen { get; private set; } = -1;

		public float ValueOf(int option, int term) => _table[option * TermCount + term];
		public float TotalOf(int option) => _totals[option];

		/// <summary>
		/// Scores <paramref name="optionCount"/> options and returns the best, or -1 if there are none. Terms at
		/// weight 0 aren't worked out. <paramref name="noise"/> adds up to that much at random to each option's
		/// total, drawn from <paramref name="random"/>, so a less skilled agent sometimes takes a worse option; 0
		/// draws nothing.
		/// </summary>
		public int Choose(int optionCount, float noise = 0f, IRandom random = null)
		{
			int terms = TermCount;
			OptionCount = optionCount;
			if (_table.Length < optionCount * terms)
				_table = new float[optionCount * terms * 2];
			if (_totals.Length < optionCount)
			{
				_totals = new float[optionCount * 2];
				_order = new int[optionCount * 2];
			}
			Chosen = -1;
			float best = float.NegativeInfinity;
			for (int o = 0; o < optionCount; o++)
			{
				float total = 0f;
				for (int t = 0; t < terms; t++)
				{
					float value = _weights[t] != 0f ? _values[t](o) : 0f;
					_table[o * terms + t] = value;
					total += value * _weights[t];
				}
				if (noise > 0f && random != null)
					total += noise * random.Value();
				_totals[o] = total;
				if (total > best)
				{
					best = total;
					Chosen = o;
				}
			}
			return Chosen;
		}

		/// <summary>
		/// The last choice in a line a person can read: the best few options, the chosen first, each with its total
		/// and the terms that made it, as value × weight. <paramref name="describe"/> names an option.
		/// </summary>
		public string Explain(int best = 3, Func<int, string> describe = null)
		{
			if (Chosen < 0)
				return $"{Name}: no options";
			for (int o = 0; o < OptionCount; o++)
				_order[o] = o;
			Array.Sort(_order, 0, OptionCount, Comparer<int>.Create((a, b) => _totals[b].CompareTo(_totals[a])));
			var text = new StringBuilder();
			CultureInfo culture = CultureInfo.InvariantCulture;
			for (int i = 0; i < Math.Min(best, OptionCount); i++)
			{
				int o = _order[i];
				text.Append(i == 0 ? "" : " ; ").Append(describe != null ? describe(o) : o.ToString(culture))
					.Append(' ').Append(_totals[o].ToString("+0.00;-0.00;0.00", culture)).Append(" (");
				bool first = true;
				for (int t = 0; t < TermCount; t++)
				{
					float contribution = ValueOf(o, t) * _weights[t];
					if (Math.Abs(contribution) < 0.005f)
						continue;
					text.Append(first ? "" : " ").Append(_names[t]).Append(' ').Append(contribution.ToString("+0.00;-0.00;0.00", culture));
					first = false;
				}
				text.Append(')');
			}
			return text.ToString();
		}
	}
}
