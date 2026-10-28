using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using FishSocial.Localization;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FishSocial.Desktop
{
    /// <summary>Loads the player text catalog and updates explicitly bound display text.</summary>
    public static class DesktopLocalization
    {
        public const string Preference = "FishSocial.Language";
        static bool initialized;
        static bool quitting;
        static DesktopLocalizationRuntime runtime;
        static readonly Dictionary<string, List<PrefabBinding>> prefabBindings = new Dictionary<string, List<PrefabBinding>>(StringComparer.Ordinal);
        static readonly Dictionary<Text, DesktopLocalizedText> textBindings = new Dictionary<Text, DesktopLocalizedText>();
        static readonly Dictionary<string, LocalizationEntry> catalogEntries = new Dictionary<string, LocalizationEntry>(StringComparer.Ordinal);
        static readonly List<DataFieldBinding> dataBindings = new List<DataFieldBinding>();
        static ConditionalWeakTable<object, HashSet<string>> boundDataFields = new ConditionalWeakTable<object, HashSet<string>>();
        static readonly HashSet<string> DataFields = new HashSet<string>(StringComparer.Ordinal)
        { "name", "speciesName", "clueText", "title", "description", "desc", "realWorldRef", "subType" };
        public static event Action Changed;

        [Serializable] sealed class CatalogDocument { public LocalizationEntry[] entries; }
        [Serializable] sealed class BindingDocument { public PrefabBinding[] prefabs; }
        [Serializable] sealed class PrefabBinding { public string resource, path, key; }
        sealed class DataFieldBinding
        {
            public WeakReference target;
            public FieldInfo field;
            public string source, rendered;
        }
        sealed class ReferenceComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceComparer Instance = new ReferenceComparer();
            public new bool Equals(object x, object y) { return ReferenceEquals(x, y); }
            public int GetHashCode(object value) { return RuntimeHelpers.GetHashCode(value); }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            LocalizationCatalog.LanguageChanged -= OnLanguageChanged;
            initialized = false;
            quitting = false;
            runtime = null;
            prefabBindings.Clear();
            textBindings.Clear();
            catalogEntries.Clear();
            dataBindings.Clear();
            boundDataFields = new ConditionalWeakTable<object, HashSet<string>>();
            Changed = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void InitializeBeforeScene() { EnsureInitialized(); }

        public static void EnsureInitialized()
        {
            if (initialized) return;
            initialized = true;
            var asset = Resources.Load<TextAsset>("Localization/text-catalog");
            var document = asset != null ? JsonUtility.FromJson<CatalogDocument>(asset.text) : null;
            LocalizationCatalog.Load(document != null && document.entries != null ? document.entries : Array.Empty<LocalizationEntry>());
            foreach (var entry in document != null && document.entries != null ? document.entries : Array.Empty<LocalizationEntry>())
                catalogEntries[entry.key] = entry;
            var bindings = Resources.Load<TextAsset>("Localization/prefab-bindings");
            var bindingDocument = bindings != null ? JsonUtility.FromJson<BindingDocument>(bindings.text) : null;
            foreach (var binding in bindingDocument != null && bindingDocument.prefabs != null ? bindingDocument.prefabs : Array.Empty<PrefabBinding>())
            {
                if (binding == null || string.IsNullOrEmpty(binding.resource) || string.IsNullOrEmpty(binding.key)) continue;
                if (!prefabBindings.TryGetValue(binding.resource, out var list))
                    prefabBindings.Add(binding.resource, list = new List<PrefabBinding>());
                list.Add(binding);
            }
            LocalizationCatalog.SetLanguage(PlayerPrefs.GetString(Preference, "zh-CN"));
            LocalizationCatalog.LanguageChanged += OnLanguageChanged;
            if (Application.isPlaying && !quitting)
            {
                var root = new GameObject("DesktopLocalization");
                UnityEngine.Object.DontDestroyOnLoad(root);
                runtime = root.AddComponent<DesktopLocalizationRuntime>();
            }
        }

        public static string Language { get { EnsureInitialized(); return LocalizationCatalog.Language; } }
        public static void SetLanguage(string language)
        {
            EnsureInitialized();
            var normalized = language == "en-US" ? "en-US" : "zh-CN";
            PlayerPrefs.SetString(Preference, normalized);
            PlayerPrefs.Save();
            LocalizationCatalog.SetLanguage(normalized);
        }

        public static string Text(string key, string fallback = null) { EnsureInitialized(); return LocalizationCatalog.Text(key, fallback); }
        public static string Format(string key, params object[] args) { EnsureInitialized(); return LocalizationCatalog.Format(key, args); }
        public static string Message(string raw) { EnsureInitialized(); return LocalizationCatalog.TranslateMessage(raw); }
        public static string Data(string raw) { EnsureInitialized(); return LocalizationCatalog.BySource(raw); }

        /// <summary>Call only for a game-data deserialization root, never a player/network profile.</summary>
        public static void BindData(object root)
        {
            EnsureInitialized();
            BindData(root, new HashSet<object>(ReferenceComparer.Instance));
        }

        static void BindData(object value, HashSet<object> visited)
        {
            if (value == null || value is string || value.GetType().IsPrimitive || !visited.Add(value)) return;
            var array = value as Array;
            if (array != null)
            {
                foreach (var item in array) BindData(item, visited);
                return;
            }
            var type = value.GetType();
            if (type.IsValueType) return;
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (field.IsInitOnly || field.IsLiteral) continue;
                var original = field.GetValue(value);
                if (field.FieldType != typeof(string))
                {
                    BindData(original, visited);
                    continue;
                }
                if (!DataFields.Contains(field.Name)) continue;
                var fields = boundDataFields.GetOrCreateValue(value);
                if (!fields.Add(field.Name)) continue;
                var localized = Data(original as string);
                if (!LocalizationCatalog.IsLocalized(localized)) continue;
                var rendered = LocalizationCatalog.Render(localized);
                field.SetValue(value, rendered);
                dataBindings.Add(new DataFieldBinding
                { target = new WeakReference(value), field = field, source = localized, rendered = rendered });
            }
        }

        public static string SetText(Text target, string value)
        {
            EnsureInitialized();
            if (target == null) return value;
            if (!textBindings.TryGetValue(target, out var binding) || binding == null)
            {
                binding = target.GetComponent<DesktopLocalizedText>();
                if (binding == null) binding = target.gameObject.AddComponent<DesktopLocalizedText>();
                textBindings[target] = binding;
            }
            binding.Bind(target, value);
            return value;
        }

        // Input content is identity/user data. Only its separate placeholder Text may be bound.
        public static string SetText(InputField target, string value)
        {
            if (target != null) target.text = value ?? string.Empty;
            return value;
        }

        public static void BindPrefab(GameObject instance, string resource)
        {
            EnsureInitialized();
            if (instance == null || string.IsNullOrEmpty(resource) || !prefabBindings.TryGetValue(resource, out var bindings)) return;
            foreach (var binding in bindings)
            {
                var node = string.IsNullOrEmpty(binding.path) ? instance.transform : instance.transform.Find(binding.path);
                if (node == null) continue;
                var text = node.GetComponent<Text>();
                if (text == null || textBindings.ContainsKey(text)) continue;
                if (catalogEntries.TryGetValue(binding.key, out var entry) &&
                    !string.Equals(text.text, entry.sourceText, StringComparison.Ordinal) &&
                    !string.Equals(text.text, entry.zhCN, StringComparison.Ordinal) &&
                    !string.Equals(text.text, entry.enUS, StringComparison.Ordinal)) continue;
                // Never replace InputField content (a placeholder is a different Text component).
                var input = text.GetComponentInParent<InputField>();
                if (input != null && input.textComponent == text) continue;
                SetText(text, Text(binding.key, text.text));
            }
        }

        internal static void DiscoverScenePrefabs()
        {
            EnsureInitialized();
            if (prefabBindings.Count == 0) return;
            var roots = Resources.FindObjectsOfTypeAll<Transform>();
            foreach (var pair in prefabBindings)
            {
                var prefabName = Path.GetFileName(pair.Key);
                foreach (var root in roots)
                {
                    if (root == null || !root.gameObject.scene.IsValid()) continue;
                    if (root.name != prefabName && root.name != prefabName + "(Clone)" &&
                        !(prefabName == "DesktopShell" && root.name == "ShellCanvas")) continue;
                    BindPrefab(root.gameObject, pair.Key);
                }
            }
        }

        static void OnLanguageChanged()
        {
            PruneDestroyedBindings();
            // Explicit factories bind before UI consumers populate user content; this scan handles
            // already loaded legacy roots without an every-frame search of the entire scene.
            DiscoverScenePrefabs();
            for (var index = dataBindings.Count - 1; index >= 0; index--)
            {
                var data = dataBindings[index];
                var target = data.target.Target;
                if (target == null || !string.Equals(data.field.GetValue(target) as string, data.rendered, StringComparison.Ordinal))
                {
                    dataBindings.RemoveAt(index);
                    continue;
                }
                data.rendered = LocalizationCatalog.Render(data.source);
                data.field.SetValue(target, data.rendered);
            }
            var snapshot = new List<DesktopLocalizedText>(textBindings.Values);
            foreach (var binding in snapshot) if (binding != null) binding.Refresh();
            Changed?.Invoke();
        }

        internal static void PruneDestroyedBindings()
        {
            // Unity does not send OnDestroy to objects that have never been active.
            // Inactive prefab labels can therefore outlive their native object in this map.
            List<Text> destroyed = null;
            foreach (var pair in textBindings)
            {
                if (pair.Key != null && pair.Value != null) continue;
                if (destroyed == null) destroyed = new List<Text>();
                destroyed.Add(pair.Key);
            }
            if (destroyed != null)
                foreach (var text in destroyed) textBindings.Remove(text);
        }

        internal static void Forget(Text text, DesktopLocalizedText binding)
        {
            if (ReferenceEquals(text, null)) return;
            if (textBindings.TryGetValue(text, out var current) && current == binding) textBindings.Remove(text);
        }

        internal static void OnQuit() { quitting = true; }
    }

    internal sealed class DesktopLocalizedText : MonoBehaviour
    {
        Text target;
        string source;
        string rendered;
        bool configured;

        internal void Bind(Text text, string value)
        {
            target = text;
            source = value ?? string.Empty;
            configured = true;
            Apply();
        }

        void Apply()
        {
            if (!configured || target == null) return;
            rendered = LocalizationCatalog.Render(source) ?? string.Empty;
            target.text = rendered;
        }

        internal void Refresh()
        {
            if (!configured || target == null) return;
            // A legacy direct assignment supersedes an old prefab binding. Do not restore it.
            if (!string.Equals(target.text, rendered, StringComparison.Ordinal))
            {
                configured = false;
                DesktopLocalization.Forget(target, this);
                return;
            }
            Apply();
        }

        void OnEnable() { if (configured) Refresh(); }
        void OnDestroy() { DesktopLocalization.Forget(target, this); }
    }

    internal sealed class DesktopLocalizationRuntime : MonoBehaviour
    {
        float nextCleanup;
        void OnEnable() { SceneManager.sceneLoaded += OnSceneLoaded; }
        void OnDisable() { SceneManager.sceneLoaded -= OnSceneLoaded; }
        void OnSceneLoaded(Scene _, LoadSceneMode mode) { DesktopLocalization.DiscoverScenePrefabs(); }
        void Update()
        {
            if (Time.unscaledTime < nextCleanup) return;
            nextCleanup = Time.unscaledTime + 15f;
            DesktopLocalization.PruneDestroyedBindings();
        }
        void OnApplicationQuit() { DesktopLocalization.OnQuit(); }
    }
}
