namespace Tempo.Core.Runtime
{
    using System;
    using System.Linq;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Stable runtime provider key, for example <c>External.Rest</c>.
    /// </summary>
    [JsonConverter(typeof(RuntimeKeyJsonConverter))]
    public readonly record struct RuntimeKey
    {
        /// <summary>Maximum runtime key length.</summary>
        public const int MaxLength = 128;

        /// <summary>String value.</summary>
        public string Value { get; }

        /// <summary>Instantiate.</summary>
        public RuntimeKey(string value)
        {
            Value = Validate(value);
        }

        /// <summary>Whether this key is unset.</summary>
        public bool IsEmpty => string.IsNullOrWhiteSpace(Value);

        /// <inheritdoc/>
        public override string ToString()
        {
            return Value ?? string.Empty;
        }

        private static string Validate(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(value));
            string trimmed = value.Trim();
            if (trimmed.Length > MaxLength) throw new ArgumentOutOfRangeException(nameof(value), "Runtime keys must be 128 characters or fewer.");
            if (trimmed.Any(char.IsControl)) throw new ArgumentException("Runtime keys cannot contain control characters.", nameof(value));

            string[] parts = trimmed.Split('.');
            if (parts.Length < 2 || parts.Any(p => p.Length == 0)) throw new ArgumentException("Runtime keys must use dotted token format.", nameof(value));
            foreach (string part in parts)
            {
                if (!char.IsLetter(part[0])) throw new ArgumentException("Runtime key tokens must start with a letter.", nameof(value));
                if (part.Any(c => !char.IsLetterOrDigit(c))) throw new ArgumentException("Runtime key tokens may only contain letters and digits.", nameof(value));
            }

            return trimmed;
        }
    }
}
