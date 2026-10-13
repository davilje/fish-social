using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace FishSocial.Desktop.Pet
{
    /// <summary>
    /// Nine-layer source recolor clips from the version-two fishing pack.
    /// Reads fishing/&lt;petId&gt;/manifest.json. Missing data falls back to flat pet frames.
    /// </summary>
    public static class FishingLayerArt
    {
        public const int CanvasWidth = 640;
        public const int CanvasHeight = 512;

        public static readonly string[] LayerNames =
        {
            "body", "head", "far_hand", "rod", "line", "fish", "hands", "bobber", "bait",
        };

        public static readonly string[] SkinIds = { "orange", "silver", "brown", "cream", "blue" };
        public static readonly string[] ClipNames =
        {
            "idle", "sit", "ready", "bait", "cast", "fishing", "bite", "effort", "hooked",
            "reel", "catch", "reset_catch", "reel_empty", "reset_empty",
        };

        static readonly string[] BodyLayerNames = { "body", "head", "far_hand", "hands" };

        static readonly Dictionary<string, Pack> Packs = new Dictionary<string, Pack>();
        static readonly Dictionary<string, Session> Cache = new Dictionary<string, Session>();

        public static Session Open(string petId, string clip, int drawW, int drawH)
        {
            if (drawW < 1 || drawH < 1 || string.IsNullOrEmpty(clip))
                return null;
            var id = string.IsNullOrEmpty(petId) ? PetArtLoader.DefaultPetId : petId;
            if (IndexOf(SkinIds, id) < 0)
                return null;
            var pack = LoadPack(id);
            if (pack == null || !pack.Clips.TryGetValue(clip, out var parsed))
                return null;
            var key = id + "|" + clip + "|" + drawW + "x" + drawH;
            if (Cache.TryGetValue(key, out var existing))
            {
                existing.Users++;
                return existing;
            }

            var session = Build(pack, id, clip, parsed, drawW, drawH, key);
            if (session == null)
                return null;
            session.Users = 1;
            Cache[key] = session;
            return session;
        }

        public static void Release(Session session)
        {
            if (session == null || session.Users <= 0)
                return;
            session.Users--;
            if (session.Users > 0)
                return;
            Cache.Remove(session.Key);
            session.DestroyAssets();
        }

        public static void Place(
            string petId,
            float slotW,
            float slotH,
            out int drawW,
            out int drawH,
            out float originX,
            out float originY)
        {
            var place = DefaultPlace;
            var id = Sanitize(petId);
            var pack = LoadPack(id);
            if (pack != null)
                place = pack.Place;
            Place(place, slotW, slotH, out drawW, out drawH, out originX, out originY);
        }

        static void Place(
            LayerPlace place,
            float slotW,
            float slotH,
            out int drawW,
            out int drawH,
            out float originX,
            out float originY)
        {
            if (slotW < 1f || slotH < 1f || place.BodyHeight < 1f || place.Reference < 1f)
            {
                drawW = 1;
                drawH = 1;
                originX = 0f;
                originY = 0f;
                return;
            }

            var uniform = Mathf.Min(slotW, slotH) / place.Reference;
            var scale = uniform * place.ReferenceBody / place.BodyHeight;
            drawW = Mathf.Max(1, Mathf.RoundToInt(CanvasWidth * scale));
            drawH = Mathf.Max(1, Mathf.RoundToInt(CanvasHeight * scale));
            var scaleX = drawW / (float)CanvasWidth;
            var scaleY = drawH / (float)CanvasHeight;
            var padX = (slotW - uniform * place.Reference) * 0.5f;
            var padY = (slotH - uniform * place.Reference) * 0.5f;
            var footX = padX + uniform * place.ReferenceFootX;
            var footY = padY + uniform * place.ReferenceFootY;
            originX = footX - place.FootX * scaleX;
            originY = footY - place.FootY * scaleY;
        }

        static Session Build(
            Pack pack, string petId, string clip, ParsedClip parsed, int drawW, int drawH, string key)
        {
            var frames = new Sprite[parsed.Orders.Count, LayerNames.Length];
            var session = new Session(key, parsed.Playback, parsed.LoopStart, parsed.Orders,
                frames, parsed.DurationsMs, parsed.NextClip);
            try
            {
                for (var index = 0; index < parsed.Orders.Count; index++)
                {
                    for (var layer = 0; layer < LayerNames.Length; layer++)
                    {
                        var path = LayerPath(pack, petId, clip, index, LayerNames[layer]);
                        frames[index, layer] = LoadSprite(path, drawW, drawH);
                        if (frames[index, layer] == null)
                            throw new InvalidDataException("Missing or invalid fishing layer: " + path);
                    }
                }

                return session;
            }
            catch (Exception)
            {
                session.DestroyAssets();
                return null;
            }
        }

        static Sprite LoadSprite(string path, int drawW, int drawH)
        {
            if (!File.Exists(path))
                return null;
            var bytes = File.ReadAllBytes(path);
            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!source.LoadImage(bytes))
            {
                DestroyAsset(source);
                return null;
            }

            if (source.width != CanvasWidth || source.height != CanvasHeight)
            {
                DestroyAsset(source);
                return null;
            }

            var texture = Scale(source, drawW, drawH);
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            return Sprite.Create(
                texture,
                new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f);
        }

        static Texture2D Scale(Texture2D source, int width, int height)
        {
            if (source.width == width && source.height == height)
                return source;

            var pixels = new Color32[width * height];
            for (var y = 0; y < height; y++)
            {
                var v = (y + 0.5f) / height;
                for (var x = 0; x < width; x++)
                {
                    var u = (x + 0.5f) / width;
                    pixels[y * width + x] = source.GetPixelBilinear(u, v);
                }
            }

            DestroyAsset(source);
            var scaled = new Texture2D(width, height, TextureFormat.RGBA32, false);
            scaled.SetPixels32(pixels);
            scaled.Apply(false, false);
            return scaled;
        }

        static string LayerPath(Pack pack, string petId, string clip, int index, string layer)
        {
            var body = false;
            for (var i = 0; i < pack.BodyLayers.Length; i++)
            {
                if (pack.BodyLayers[i] == layer)
                {
                    body = true;
                    break;
                }
            }

            var folder = body
                ? Path.Combine(pack.Root, petId, "body", clip, index.ToString())
                : Path.Combine(pack.Root, "shared", clip, index.ToString());
            return Path.Combine(folder, layer + ".png");
        }

        static Pack LoadPack(string petId)
        {
            if (Packs.TryGetValue(petId, out var cached))
                return cached;

            Pack pack = null;
            foreach (var root in Roots())
            {
                var manifest = Path.Combine(root, petId, "manifest.json");
                if (!File.Exists(manifest))
                    continue;
                try
                {
                    pack = Parse(root, File.ReadAllText(manifest, Encoding.UTF8));
                    if (pack != null && !HasCompleteFiles(pack, petId))
                        pack = null;
                }
                catch (Exception)
                {
                    pack = null;
                }

                if (pack != null)
                    break;
            }

            if (pack != null)
                Packs[petId] = pack;
            return pack;
        }

        static bool HasCompleteFiles(Pack pack, string petId)
        {
            foreach (var pair in pack.Clips)
            {
                for (var index = 0; index < pair.Value.Orders.Count; index++)
                {
                    foreach (var layer in LayerNames)
                    {
                        if (!File.Exists(LayerPath(pack, petId, pair.Key, index, layer)))
                            return false;
                    }
                }
            }
            return true;
        }

        public static bool ValidateManifest(string json, out string error)
        {
            try
            {
                if (Parse(string.Empty, json) != null)
                {
                    error = null;
                    return true;
                }
            }
            catch (Exception)
            {
            }

            error = "Invalid v2 fishing manifest: expected sourceVersion 18, canvas 640x512, " +
                "nine unique layers, four body layers, complete clips and positive integer durations.";
            return false;
        }

        static Pack Parse(string root, string json)
        {
            var value = new JsonReader(json).Parse();
            var obj = value as Dictionary<string, object>;
            if (obj == null || !obj.TryGetValue("version", out var version) || !IsInteger(version, 2) ||
                !obj.TryGetValue("sourceVersion", out var sourceVersion) || !IsInteger(sourceVersion, 18))
                return null;
            if (!obj.TryGetValue("canvas", out var canvasValue) || !(canvasValue is List<object> canvas) ||
                canvas.Count != 2 || !IsInteger(canvas[0], CanvasWidth) || !IsInteger(canvas[1], CanvasHeight))
                return null;
            if (!SameNames(AsStrings(obj, "layers"), LayerNames))
                return null;
            var body = AsStrings(obj, "bodyLayers");
            if (!SameNames(body, BodyLayerNames))
                return null;

            if (!obj.TryGetValue("clips", out var clipsValue))
                return null;
            var clipsObj = clipsValue as Dictionary<string, object>;
            if (clipsObj == null)
                return null;

            var clips = new Dictionary<string, ParsedClip>();
            foreach (var pair in clipsObj)
            {
                if (IndexOf(ClipNames, pair.Key) < 0)
                    return null;
                var parsed = ParseClip(pair.Value as Dictionary<string, object>);
                if (parsed == null)
                    return null;
                clips[pair.Key] = parsed;
            }

            if (clips.Count != ClipNames.Length)
                return null;
            foreach (var parsed in clips.Values)
            {
                if (parsed.NextClip != null && !clips.ContainsKey(parsed.NextClip))
                    return null;
            }
            if (!Matches(clips, "idle", 1, "hold") || !Matches(clips, "sit", 4, "once", "ready") ||
                !Matches(clips, "ready", 1, "hold") || !Matches(clips, "bait", 4, "once") ||
                !Matches(clips, "cast", 4, "once") || !Matches(clips, "fishing", 1, "hold") ||
                !Matches(clips, "bite", 4, "once", "effort") ||
                !Matches(clips, "effort", 6, "intro-loop", null, 0) ||
                !Matches(clips, "hooked", 10, "intro-loop", null, 4) ||
                !Matches(clips, "reel", 6, "once", "catch") ||
                !Matches(clips, "catch", 7, "once", "reset_catch") ||
                !Matches(clips, "reset_catch", 4, "once", "ready") ||
                !Matches(clips, "reel_empty", 6, "once", "reset_empty") ||
                !Matches(clips, "reset_empty", 4, "once", "ready"))
                return null;

            return new Pack(root, body, clips, ReadPlace(obj));
        }

        static bool Matches(Dictionary<string, ParsedClip> clips, string name, int count,
            string playback, string nextClip = null, int loopStart = 0)
        {
            return clips.TryGetValue(name, out var clip) && clip.Orders.Count == count &&
                clip.Playback == playback && clip.NextClip == nextClip && clip.LoopStart == loopStart;
        }

        static bool IsInteger(object value, int expected)
        {
            return value is long integer && integer == expected;
        }

        static LayerPlace ReadPlace(Dictionary<string, object> obj)
        {
            var place = DefaultPlace;
            if (!obj.TryGetValue("place", out var value))
                return place;
            var map = value as Dictionary<string, object>;
            if (map == null)
                return place;
            if (TryPair(map, "foot", out var footX, out var footY))
            {
                place.FootX = footX;
                place.FootY = footY;
            }

            var body = Number(map, "bodyHeight");
            if (body > 1f)
                place.BodyHeight = body;
            var reference = Number(map, "reference");
            if (reference > 1f)
                place.Reference = reference;
            if (TryPair(map, "referenceFoot", out var refX, out var refY))
            {
                place.ReferenceFootX = refX;
                place.ReferenceFootY = refY;
            }

            var refBody = Number(map, "referenceBody");
            if (refBody > 1f)
                place.ReferenceBody = refBody;
            return place;
        }

        static float Number(Dictionary<string, object> map, string key)
        {
            if (!map.TryGetValue(key, out var value))
                return 0f;
            return ToFloat(value);
        }

        static bool TryPair(Dictionary<string, object> map, string key, out float x, out float y)
        {
            x = 0f;
            y = 0f;
            if (!map.TryGetValue(key, out var value))
                return false;
            var list = value as List<object>;
            if (list == null || list.Count < 2)
                return false;
            x = ToFloat(list[0]);
            y = ToFloat(list[1]);
            return true;
        }

        static float ToFloat(object value)
        {
            if (value is long)
                return (long)value;
            if (value is double)
                return (float)(double)value;
            return 0f;
        }

        static ParsedClip ParseClip(Dictionary<string, object> clip)
        {
            if (clip == null || !clip.TryGetValue("playback", out var playbackValue))
                return null;
            var playback = playbackValue as string;
            if (playback != "once" && playback != "hold" && playback != "intro-loop")
                return null;
            if (!clip.TryGetValue("frames", out var framesValue))
                return null;
            var frames = framesValue as List<object>;
            if (frames == null || frames.Count == 0)
                return null;

            var orders = new List<string[]>();
            for (var i = 0; i < frames.Count; i++)
            {
                var row = frames[i] as List<object>;
                if (row == null || row.Count != LayerNames.Length)
                    return null;
                var order = new string[LayerNames.Length];
                for (var n = 0; n < row.Count; n++)
                {
                    var name = row[n] as string;
                    if (IndexOf(LayerNames, name) < 0 || IndexOf(order, name) >= 0)
                        return null;
                    order[n] = name;
                }

                orders.Add(order);
            }

            if (!clip.TryGetValue("durationsMs", out var durationsValue) ||
                !(durationsValue is List<object> durations) || durations.Count != orders.Count)
                return null;
            var durationsMs = new int[durations.Count];
            for (var index = 0; index < durations.Count; index++)
            {
                if (!(durations[index] is long duration) || duration < 1 || duration > int.MaxValue)
                    return null;
                durationsMs[index] = (int)duration;
            }

            if (!clip.TryGetValue("loopStart", out var loopValue))
            {
                if (playback == "intro-loop")
                    return null;
                loopValue = 0L;
            }
            if (!(loopValue is long loopInteger) || loopInteger < 0 || loopInteger >= orders.Count)
                return null;
            var nextClip = clip.TryGetValue("nextClip", out var nextValue) ? nextValue as string : null;
            if (clip.ContainsKey("nextClip") && string.IsNullOrEmpty(nextClip))
                return null;
            if (nextClip != null && playback != "once")
                return null;
            return new ParsedClip(playback, (int)loopInteger, orders, durationsMs, nextClip);
        }

        static string[] AsStrings(Dictionary<string, object> obj, string key)
        {
            if (!obj.TryGetValue(key, out var value))
                return null;
            var list = value as List<object>;
            if (list == null)
                return null;
            var names = new string[list.Count];
            for (var i = 0; i < list.Count; i++)
            {
                names[i] = list[i] as string;
                if (string.IsNullOrEmpty(names[i]))
                    return null;
            }

            return names;
        }

        static bool SameNames(string[] left, string[] right)
        {
            if (left == null || right == null || left.Length != right.Length)
                return false;
            for (var i = 0; i < left.Length; i++)
            {
                if (left[i] != right[i])
                    return false;
            }

            return true;
        }

        static int IndexOf(IList<string> names, string name)
        {
            if (names == null || name == null)
                return -1;
            for (var i = 0; i < names.Count; i++)
            {
                if (names[i] == name)
                    return i;
            }

            return -1;
        }

        static void DestroyAsset(UnityEngine.Object asset)
        {
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(asset);
            else
                UnityEngine.Object.DestroyImmediate(asset);
        }

        static string Sanitize(string petId)
        {
            if (string.IsNullOrEmpty(petId))
                return PetArtLoader.DefaultPetId;
            for (var i = 0; i < petId.Length; i++)
            {
                var c = petId[i];
                if (!((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '-' || c == '_'))
                    return PetArtLoader.DefaultPetId;
            }

            return petId;
        }

        static IEnumerable<string> Roots()
        {
            yield return Path.Combine(Application.streamingAssetsPath, "Fishing");
#if UNITY_EDITOR
            yield return Path.GetFullPath(Path.Combine(
                Application.dataPath, "..", "..", "desktop-overlay", "OverlayResources", "fishing"));
#endif
        }

        static readonly LayerPlace DefaultPlace = new LayerPlace
        {
            FootX = 264f,
            FootY = 450f,
            BodyHeight = 257f,
            Reference = 512f,
            ReferenceFootX = 138.5f,
            ReferenceFootY = 404f,
            ReferenceBody = 279f,
        };

        sealed class Pack
        {
            public Pack(
                string root,
                string[] bodyLayers,
                Dictionary<string, ParsedClip> clips,
                LayerPlace place)
            {
                Root = root;
                BodyLayers = bodyLayers;
                Clips = clips;
                Place = place;
            }

            public string Root { get; }
            public string[] BodyLayers { get; }
            public Dictionary<string, ParsedClip> Clips { get; }
            public LayerPlace Place { get; }
        }

        struct LayerPlace
        {
            public float FootX;
            public float FootY;
            public float BodyHeight;
            public float Reference;
            public float ReferenceFootX;
            public float ReferenceFootY;
            public float ReferenceBody;
        }

        sealed class ParsedClip
        {
            public ParsedClip(string playback, int loopStart, List<string[]> orders,
                int[] durationsMs, string nextClip)
            {
                Playback = playback;
                LoopStart = loopStart;
                Orders = orders;
                DurationsMs = durationsMs;
                NextClip = nextClip;
            }

            public string Playback { get; }
            public int LoopStart { get; }
            public List<string[]> Orders { get; }
            public int[] DurationsMs { get; }
            public string NextClip { get; }
        }

        public sealed class Session
        {
            public Session(
                string key, string playback, int loopStart, List<string[]> orders, Sprite[,] frames,
                int[] durationsMs, string nextClip = null)
            {
                Key = key;
                Playback = playback;
                LoopStart = loopStart;
                Orders = orders;
                Frames = frames;
                _durationsMs = durationsMs;
                NextClip = nextClip;
            }

            readonly int[] _durationsMs;

            public string Key { get; }
            public string Playback { get; }
            public int LoopStart { get; }
            public string NextClip { get; }
            public List<string[]> Orders { get; }
            public Sprite[,] Frames { get; }
            public int FrameCount => Orders.Count;
            public int Users;

            public int DurationMs(int index) => _durationsMs[index];
            public string[] Order(int index) => Orders[index];
            public Sprite Frame(int index, int layer) => Frames[index, layer];
            public Sprite Frame(int index, string layer) => Frames[index, IndexOf(LayerNames, layer)];

            public void DestroyAssets()
            {
                var width = Frames.GetLength(0);
                var height = Frames.GetLength(1);
                for (var y = 0; y < width; y++)
                {
                    for (var x = 0; x < height; x++)
                    {
                        var sprite = Frames[y, x];
                        if (sprite == null)
                            continue;
                        if (sprite.texture != null)
                            DestroyAsset(sprite.texture);
                        DestroyAsset(sprite);
                        Frames[y, x] = null;
                    }
                }
            }
        }

        sealed class JsonReader
        {
            readonly string _text;
            int _index;

            public JsonReader(string text)
            {
                _text = text ?? string.Empty;
            }

            public object Parse()
            {
                Skip();
                var value = ReadValue();
                Skip();
                if (_index != _text.Length)
                    throw new InvalidOperationException("Trailing json content.");
                return value;
            }

            object ReadValue()
            {
                Skip();
                if (_index >= _text.Length)
                    throw new InvalidOperationException("empty json");
                var token = _text[_index];
                if (token == '{')
                    return ReadObject();
                if (token == '[')
                    return ReadArray();
                if (token == '"')
                    return ReadString();
                if (token == 't' || token == 'f')
                    return ReadBool();
                if (token == 'n')
                    return ReadNull();
                return ReadNumber();
            }

            Dictionary<string, object> ReadObject()
            {
                Expect('{');
                var map = new Dictionary<string, object>();
                Skip();
                if (Peek('}'))
                {
                    _index++;
                    return map;
                }

                while (true)
                {
                    Skip();
                    var key = ReadString();
                    Skip();
                    Expect(':');
                    map.Add(key, ReadValue());
                    Skip();
                    if (Peek('}'))
                    {
                        _index++;
                        break;
                    }

                    Expect(',');
                }

                return map;
            }

            List<object> ReadArray()
            {
                Expect('[');
                var list = new List<object>();
                Skip();
                if (Peek(']'))
                {
                    _index++;
                    return list;
                }

                while (true)
                {
                    list.Add(ReadValue());
                    Skip();
                    if (Peek(']'))
                    {
                        _index++;
                        break;
                    }

                    Expect(',');
                }

                return list;
            }

            string ReadString()
            {
                Expect('"');
                var builder = new StringBuilder();
                while (_index < _text.Length)
                {
                    var character = _text[_index++];
                    if (character == '"')
                        return builder.ToString();
                    if (character < ' ')
                        throw new InvalidOperationException("Unescaped control character.");
                    if (character != '\\')
                    {
                        builder.Append(character);
                        continue;
                    }

                    if (_index >= _text.Length)
                        break;
                    var escaped = _text[_index++];
                    switch (escaped)
                    {
                        case '"': case '\\': case '/': builder.Append(escaped); break;
                        case 'b': builder.Append('\b'); break;
                        case 'f': builder.Append('\f'); break;
                        case 'n': builder.Append('\n'); break;
                        case 'r': builder.Append('\r'); break;
                        case 't': builder.Append('\t'); break;
                        case 'u':
                            if (_index + 4 > _text.Length)
                                throw new InvalidOperationException("Incomplete unicode escape.");
                            builder.Append((char)int.Parse(_text.Substring(_index, 4),
                                NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture));
                            _index += 4;
                            break;
                        default: throw new InvalidOperationException("Invalid string escape.");
                    }
                }

                throw new InvalidOperationException("unterminated string");
            }

            object ReadNumber()
            {
                var start = _index;
                if (Peek('-'))
                    _index++;
                if (Peek('0'))
                    _index++;
                else
                    ReadDigits();
                var floating = false;
                if (Peek('.'))
                {
                    floating = true;
                    _index++;
                    ReadDigits();
                }
                if (Peek('e') || Peek('E'))
                {
                    floating = true;
                    _index++;
                    if (Peek('+') || Peek('-'))
                        _index++;
                    ReadDigits();
                }
                var token = _text.Substring(start, _index - start);
                if (floating)
                    return double.Parse(token, CultureInfo.InvariantCulture);
                return long.Parse(token, CultureInfo.InvariantCulture);
            }

            void ReadDigits()
            {
                var start = _index;
                while (_index < _text.Length && _text[_index] >= '0' && _text[_index] <= '9')
                    _index++;
                if (_index == start)
                    throw new InvalidOperationException("Expected numeric digits.");
            }

            bool ReadBool()
            {
                if (_text.IndexOf("true", _index, StringComparison.Ordinal) == _index)
                {
                    _index += 4;
                    return true;
                }

                if (_text.IndexOf("false", _index, StringComparison.Ordinal) == _index)
                {
                    _index += 5;
                    return false;
                }

                throw new InvalidOperationException("bool");
            }

            object ReadNull()
            {
                if (_text.IndexOf("null", _index, StringComparison.Ordinal) != _index)
                    throw new InvalidOperationException("null");
                _index += 4;
                return null;
            }

            void Skip()
            {
                while (_index < _text.Length && char.IsWhiteSpace(_text[_index]))
                    _index++;
            }

            bool Peek(char expected)
            {
                return _index < _text.Length && _text[_index] == expected;
            }

            void Expect(char expected)
            {
                Skip();
                if (_index >= _text.Length || _text[_index] != expected)
                    throw new InvalidOperationException("expected " + expected);
                _index++;
            }
        }
    }
}
