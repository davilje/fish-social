using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;

namespace FishSocial.Localization
{
    /// <summary>Build display copy while retaining each appended translation plan.</summary>
    public sealed class LocalizedStringBuilder
    {
        readonly List<object> parts = new List<object>();

        public LocalizedStringBuilder() { }
        public LocalizedStringBuilder(string value) { Append(value); }

        public LocalizedStringBuilder Append(string value)
        {
            if (value != null) parts.Add(value);
            return this;
        }

        public LocalizedStringBuilder Append(char value)
        {
            parts.Add(value);
            return this;
        }

        public LocalizedStringBuilder Append(object value)
        {
            // StringBuilder snapshots object.ToString() at append time. Strings
            // themselves retain the weak translation metadata associated with them.
            return Append(value == null ? null : value.ToString());
        }

        public LocalizedStringBuilder AppendLine() { return Append(Environment.NewLine); }
        public LocalizedStringBuilder AppendLine(string value) { return Append(value).AppendLine(); }
        public LocalizedStringBuilder Clear() { parts.Clear(); return this; }
        public override string ToString() { return LocalizationCatalog.Concat(parts.ToArray()); }
    }

    [Serializable]
    public sealed class LocalizationEntry
    {
        public string key;
        public string sourceText;
        public string zhCN;
        public string enUS;
        public bool dataText;
        // Only explicitly identified game-data arguments may be translated in a
        // server message. Nicknames and other user content must remain verbatim.
        public int[] dataArguments;
    }

    /// <summary>
    /// Shared Unity/WPF translation catalog. A returned string carries a weakly
    /// referenced render plan; ordinary strings never become translation tokens.
    /// </summary>
    public static class LocalizationCatalog
    {
        public const string Chinese = "zh-CN";
        public const string English = "en-US";
        static readonly object Gate = new object();
        static readonly ConditionalWeakTable<string, RenderPlan> Plans = new ConditionalWeakTable<string, RenderPlan>();
        static readonly Regex Placeholder = new Regex(@"\{(?<index>\d+)(?:,-?\d+)?(?::(?<format>[^{}]*))?\}", RegexOptions.CultureInvariant);
        static Catalog catalog = new Catalog();
        static string language = Chinese;

        public static event Action LanguageChanged;
        public static string Language { get { lock (Gate) return language; } }

        public static void Load(LocalizationEntry[] entries)
        {
            if (entries == null) throw new ArgumentNullException("entries");
            var next = new Catalog();
            foreach (var item in entries)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.key))
                    throw new ArgumentException("Localization entries require a key.", "entries");
                if (next.Entries.ContainsKey(item.key))
                    throw new ArgumentException("Duplicate localization key: " + item.key, "entries");
                var entry = new LocalizationEntry {
                    key = item.key, sourceText = item.sourceText ?? "", zhCN = item.zhCN ?? item.sourceText ?? "",
                    enUS = item.enUS ?? "", dataText = item.dataText,
                    dataArguments = item.dataArguments == null ? new int[0] : (int[])item.dataArguments.Clone()
                };
                next.Entries.Add(entry.key, entry);
            }
            foreach (var entry in next.Entries.Values)
            {
                foreach (var text in Variants(entry))
                {
                    next.AddMessage(text, entry);
                    if ((entry.dataText || entry.key.StartsWith("game.", StringComparison.Ordinal)) && !HasPlaceholder(text))
                        next.AddData(text, entry);
                }
            }
            next.Templates.Sort((a, b) => b.LiteralLength.CompareTo(a.LiteralLength));
            lock (Gate) catalog = next; // Validation/index building is atomic.
        }

        public static void SetLanguage(string value)
        {
            var normalized = string.Equals(value, English, StringComparison.OrdinalIgnoreCase) ? English : Chinese;
            Action changed;
            lock (Gate)
            {
                if (language == normalized) return;
                language = normalized;
                changed = LanguageChanged;
            }
            if (changed != null) changed();
        }

        public static string Text(string key, string fallback = null)
        {
            return Remember(new RenderPlan { Key = key, Fallback = fallback, Arguments = null });
        }

        public static string Format(string key, params object[] arguments)
        {
            return Remember(new RenderPlan { Key = key, Arguments = arguments == null ? new object[0] : (object[])arguments.Clone() });
        }

        /// <summary>Compose display copy without losing nested translation plans.</summary>
        public static string Concat(params object[] values)
        {
            if (values == null) return string.Empty;
            var localized = false;
            foreach (var value in values)
                if (IsLocalized(value as string)) { localized = true; break; }
            if (!localized) return string.Concat(values);
            // Flatten chained '+' migrations so long display lines do not consume
            // one recursion level per appended fragment.
            var parts = new List<object>();
            foreach (var value in values)
            {
                RenderPlan nested;
                var text = value as string;
                if (text != null && Plans.TryGetValue(text, out nested) && nested.Kind == PlanKind.Concat)
                    parts.AddRange(nested.Arguments);
                else parts.Add(value);
            }
            return Remember(new RenderPlan { Kind = PlanKind.Concat, Arguments = parts.ToArray() });
        }

        public static string Join(string separator, IEnumerable<string> values)
        {
            if (values == null) throw new ArgumentNullException("values");
            var snapshot = new List<object>();
            var localized = IsLocalized(separator);
            foreach (var value in values)
            {
                snapshot.Add(value);
                localized |= IsLocalized(value);
            }
            if (!localized) return string.Join(separator, snapshot);
            return Remember(new RenderPlan { Kind = PlanKind.Join,
                Separator = separator, Arguments = snapshot.ToArray() });
        }

        public static string Trim(string value)
        {
            if (value == null) return null;
            return Transform(value, text => text.Trim());
        }

        /// <summary>Apply a pure display transformation again after each language render.</summary>
        public static string Transform(string value, Func<string, string> transformation)
        {
            if (transformation == null) throw new ArgumentNullException("transformation");
            if (!IsLocalized(value)) return transformation(value);
            return Remember(new RenderPlan { Kind = PlanKind.Transform,
                Arguments = new object[] { value }, Transformation = transformation });
        }

        /// <summary>The catalog entry contains a DateTime custom or standard format.</summary>
        public static string FormatDate(string key, DateTime value)
        {
            return Remember(new RenderPlan { Kind = PlanKind.Date, Key = key, Date = value });
        }

        public static string FormatDate(string key, DateTimeOffset value)
        {
            return Remember(new RenderPlan { Kind = PlanKind.Date, Key = key, Date = value });
        }

        /// <summary>Re-renders only strings created by this catalog.</summary>
        public static string Render(string value)
        {
            if (value == null) return null;
            RenderPlan plan;
            return Plans.TryGetValue(value, out plan) ? Remember(plan) : value;
        }

        public static bool IsLocalized(string value)
        {
            RenderPlan ignored;
            return value != null && Plans.TryGetValue(value, out ignored);
        }

        /// <summary>Exact game-data lookup; ambiguous translations remain unchanged.</summary>
        public static string BySource(string sourceText)
        {
            if (sourceText == null) return null;
            RenderPlan plan;
            if (Plans.TryGetValue(sourceText, out plan)) return Remember(plan);
            Catalog snapshot;
            lock (Gate) snapshot = catalog;
            LocalizationEntry entry;
            return snapshot.Data.TryGetValue(sourceText, out entry) && entry != null ? Text(entry.key) : sourceText;
        }

        /// <summary>
        /// Translate trusted service messages, never player chat/bio/nicknames.
        /// Matching covers the complete message; there are no substring replacements.
        /// </summary>
        public static string TranslateMessage(string raw)
        {
            if (raw == null || raw.Length == 0 || raw.Length > 8192) return raw;
            RenderPlan existing;
            if (Plans.TryGetValue(raw, out existing)) return Remember(existing);
            Catalog snapshot;
            lock (Gate) snapshot = catalog;
            LocalizationEntry exact;
            if (snapshot.Messages.TryGetValue(raw, out exact))
                return exact != null ? Text(exact.key) : raw;
            foreach (var template in snapshot.Templates)
            {
                if (template.Prefix.Length > 0 && !raw.StartsWith(template.Prefix, StringComparison.Ordinal)) continue;
                Match match;
                try { match = template.Pattern.Match(raw); }
                catch (RegexMatchTimeoutException) { continue; }
                if (!match.Success) continue;
                var captures = new Dictionary<int, CapturedArgument>();
                var valid = true;
                foreach (var part in template.Parts)
                {
                    CapturedArgument argument;
                    if (!captures.TryGetValue(part.Index, out argument))
                    {
                        argument = new CapturedArgument { IsData = Array.IndexOf(template.Entry.dataArguments, part.Index) >= 0 };
                        captures.Add(part.Index, argument);
                    }
                    var text = match.Groups[part.Group].Value;
                    string prior;
                    if (argument.Formats.TryGetValue(part.Format, out prior) && prior != text) { valid = false; break; }
                    argument.Formats[part.Format] = text;
                }
                if (!valid) continue;
                var arguments = new object[template.MaxIndex + 1];
                foreach (var pair in captures) arguments[pair.Key] = pair.Value;
                return Format(template.Entry.key, arguments);
            }
            return raw;
        }

        static string Remember(RenderPlan plan)
        {
            Catalog snapshot;
            string locale;
            lock (Gate) { snapshot = catalog; locale = language; }
            var output = RenderPlanText(plan, snapshot, locale, 0);
            // Do not attach metadata to interned strings or to the shared empty string.
            var result = new string(output.ToCharArray());
            if (result.Length > 0) Plans.Add(result, plan);
            return result;
        }

        static string RenderPlanText(RenderPlan plan, Catalog snapshot, string locale, int depth)
        {
            if (depth > 24) return plan.Fallback ?? plan.Key ?? "";
            if (plan.Kind == PlanKind.Transform)
                return plan.Transformation((string)RenderArgument(plan.Arguments[0], snapshot, locale, depth)) ?? string.Empty;
            if (plan.Kind == PlanKind.Concat || plan.Kind == PlanKind.Join)
            {
                var output = new StringBuilder();
                var separator = plan.Kind == PlanKind.Join
                    ? RenderArgument(plan.Separator, snapshot, locale, depth) as string : null;
                for (var i = 0; i < plan.Arguments.Length; i++)
                {
                    if (i > 0 && separator != null) output.Append(separator);
                    output.Append(RenderArgument(plan.Arguments[i], snapshot, locale, depth));
                }
                return output.ToString();
            }
            LocalizationEntry entry = null;
            if (plan.Key != null) snapshot.Entries.TryGetValue(plan.Key, out entry);
            var source = entry == null ? plan.Fallback ?? plan.Key ?? "" : Select(entry, locale);
            if (plan.Kind == PlanKind.Date)
            {
                var culture = CultureInfo.GetCultureInfo(locale);
                var date = (IFormattable)plan.Date;
                try { return date.ToString(source, culture); }
                catch (FormatException)
                {
                    try { return date.ToString(entry == null ? null : entry.sourceText, culture); }
                    catch (FormatException) { return date.ToString(null, culture); }
                }
            }
            if (plan.Arguments == null) return source;
            var arguments = new object[plan.Arguments.Length];
            for (var i = 0; i < arguments.Length; i++)
                arguments[i] = RenderArgument(plan.Arguments[i], snapshot, locale, depth);
            try { return string.Format(CultureInfo.InvariantCulture, source, arguments); }
            catch (FormatException)
            {
                var fallback = plan.Fallback ?? (entry == null ? source : entry.sourceText);
                try { return string.Format(CultureInfo.InvariantCulture, fallback, arguments); }
                catch (FormatException) { return fallback; }
            }
        }

        static object RenderArgument(object value, Catalog snapshot, string locale, int depth)
        {
            var text = value as string;
            RenderPlan nested;
            if (text != null && Plans.TryGetValue(text, out nested))
                return RenderPlanText(nested, snapshot, locale, depth + 1);
            var captured = value as CapturedArgument;
            return captured == null ? value : captured.ForLanguage(snapshot, locale);
        }

        static string Select(LocalizationEntry entry, string locale)
        {
            return locale == English && !string.IsNullOrEmpty(entry.enUS) ? entry.enUS : entry.zhCN ?? entry.sourceText ?? entry.key;
        }

        static bool Equivalent(LocalizationEntry a, LocalizationEntry b)
        {
            return a.zhCN == b.zhCN && a.enUS == b.enUS;
        }

        static IEnumerable<string> Variants(LocalizationEntry entry)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var value in new[] { entry.sourceText, entry.zhCN, entry.enUS })
                if (!string.IsNullOrEmpty(value) && seen.Add(value)) yield return value;
        }

        static bool HasPlaceholder(string text) { return Placeholder.IsMatch(text); }

        sealed class Catalog
        {
            public readonly Dictionary<string, LocalizationEntry> Entries = new Dictionary<string, LocalizationEntry>(StringComparer.Ordinal);
            public readonly Dictionary<string, LocalizationEntry> Messages = new Dictionary<string, LocalizationEntry>(StringComparer.Ordinal);
            public readonly Dictionary<string, LocalizationEntry> Data = new Dictionary<string, LocalizationEntry>(StringComparer.Ordinal);
            public readonly List<MessageTemplate> Templates = new List<MessageTemplate>();
            public void AddData(string text, LocalizationEntry entry) { AddExact(Data, text, entry); }
            public void AddMessage(string text, LocalizationEntry entry)
            {
                if (!HasPlaceholder(text)) { AddExact(Messages, text, entry); return; }
                var template = MessageTemplate.Create(text, entry);
                if (template != null) Templates.Add(template);
            }
            static void AddExact(Dictionary<string, LocalizationEntry> index, string text, LocalizationEntry entry)
            {
                LocalizationEntry prior;
                if (!index.TryGetValue(text, out prior)) index.Add(text, entry);
                else if (prior != null && !Equivalent(prior, entry)) index[text] = null;
            }
        }

        enum PlanKind { Text, Concat, Join, Date, Transform }

        sealed class RenderPlan
        {
            public PlanKind Kind;
            public string Key;
            public string Fallback;
            public object[] Arguments;
            public string Separator;
            public object Date;
            public Func<string, string> Transformation;
        }

        sealed class CapturedArgument : IFormattable
        {
            public bool IsData;
            public readonly Dictionary<string, string> Formats = new Dictionary<string, string>(StringComparer.Ordinal);
            public CapturedArgument ForLanguage(Catalog snapshot, string locale)
            {
                if (!IsData) return this;
                var result = new CapturedArgument();
                foreach (var pair in Formats)
                {
                    LocalizationEntry entry;
                    result.Formats.Add(pair.Key, snapshot.Data.TryGetValue(pair.Value, out entry) && entry != null ? Select(entry, locale) : pair.Value);
                }
                return result;
            }
            public string ToString(string format, IFormatProvider provider)
            {
                string result;
                if (Formats.TryGetValue(format ?? "", out result)) return result;
                foreach (var value in Formats.Values) return value;
                return "";
            }
            public override string ToString() { return ToString("", CultureInfo.InvariantCulture); }
        }

        sealed class TemplatePart
        {
            public int Index;
            public string Format;
            public string Group;
        }

        sealed class MessageTemplate
        {
            public LocalizationEntry Entry;
            public Regex Pattern;
            public string Prefix;
            public int MaxIndex;
            public int LiteralLength;
            public readonly List<TemplatePart> Parts = new List<TemplatePart>();
            public static MessageTemplate Create(string text, LocalizationEntry entry)
            {
                var result = new MessageTemplate { Entry = entry, Prefix = "" };
                var pattern = new StringBuilder("\\A");
                var literal = new StringBuilder();
                for (var index = 0; index < text.Length;)
                {
                    if (index + 1 < text.Length && ((text[index] == '{' && text[index + 1] == '{') || (text[index] == '}' && text[index + 1] == '}')))
                    { literal.Append(text[index]); index += 2; continue; }
                    var match = text[index] == '{' ? Placeholder.Match(text, index) : Match.Empty;
                    if (!match.Success || match.Index != index) { literal.Append(text[index++]); continue; }
                    if (result.Parts.Count == 0) result.Prefix = literal.ToString();
                    result.LiteralLength += literal.Length;
                    pattern.Append(Regex.Escape(literal.ToString())); literal.Length = 0;
                    var part = new TemplatePart { Index = int.Parse(match.Groups["index"].Value, CultureInfo.InvariantCulture), Format = match.Groups["format"].Value, Group = "p" + result.Parts.Count };
                    // Bound malformed catalogs as well as accidental catch-all templates.
                    if (part.Index > 128 || result.Parts.Count >= 64) return null;
                    result.Parts.Add(part); result.MaxIndex = Math.Max(result.MaxIndex, part.Index);
                    pattern.Append("(?<").Append(part.Group).Append(">.*?)");
                    index += match.Length;
                }
                result.LiteralLength += literal.Length;
                if (result.Parts.Count == 0 || result.LiteralLength < 2) return null;
                pattern.Append(Regex.Escape(literal.ToString())).Append("\\z");
                result.Pattern = new Regex(pattern.ToString(), RegexOptions.Singleline | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(25));
                return result;
            }
        }
    }
}
