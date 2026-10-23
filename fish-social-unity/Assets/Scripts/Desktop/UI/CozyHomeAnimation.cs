using System;
using System.Collections.Generic;
using UnityEngine;

namespace FishSocial.Desktop
{
    public static class CozyHomeAnimation
    {
        public const string ResourceRoot = CozyHomeRoom.ResourceRoot + "animations/";
        public const float CharacterScale = .82f;

        [Serializable] public sealed class Frame
        {
            public string file;
            public float durationMs;
        }

        [Serializable] public sealed class Clip
        {
            public string id, skinId, action, direction;
            public int[] canvas, registration, loop;
            public Frame[] frames;
            [NonSerialized] public Texture2D[] textures;
            public float Duration
            {
                get { var total = 0f; foreach (var frame in frames) total += frame.durationMs / 1000f; return total; }
            }
            public float PlaybackDuration(int repetitions = 1)
            {
                var duration = Duration;
                if (loop != null && loop.Length == 2)
                    for (var index = loop[0]; index < loop[1]; index++) duration += frames[index].durationMs / 1000f * (Mathf.Max(1, repetitions) - 1);
                return duration;
            }
            public int FrameAt(float elapsed, int repetitions = 1, bool repeat = false)
            {
                elapsed = Mathf.Max(0, elapsed);
                if (repeat) elapsed %= Duration;
                if (loop != null && loop.Length == 2 && repetitions > 1)
                {
                    var start = 0f; var length = 0f;
                    for (var index = 0; index < loop[0]; index++) start += frames[index].durationMs / 1000f;
                    for (var index = loop[0]; index < loop[1]; index++) length += frames[index].durationMs / 1000f;
                    if (elapsed >= start && elapsed < start + length * repetitions) elapsed = start + (elapsed - start) % length;
                    else if (elapsed >= start + length * repetitions) elapsed -= length * (repetitions - 1);
                }
                for (var index = 0; index < frames.Length - 1; index++)
                {
                    var duration = frames[index].durationMs / 1000f;
                    if (elapsed < duration) return index;
                    elapsed -= duration;
                }
                return frames.Length - 1;
            }
        }

        [Serializable] sealed class Manifest { public Clip[] clips; }
        static Dictionary<string, Clip> catalog;
        static string Key(string skin, string action, string direction) => skin + "/" + action + "/" + direction;

        public static Clip Find(string skin, string action, string direction = "neutral")
        {
            if (catalog == null)
            {
                catalog = new Dictionary<string, Clip>();
                var json = Resources.Load<TextAsset>(ResourceRoot + "manifest");
                if (json != null)
                {
                    var manifest = JsonUtility.FromJson<Manifest>(json.text);
                    if (manifest?.clips != null)
                        foreach (var clip in manifest.clips)
                            if (Valid(clip)) catalog[Key(clip.skinId, clip.action, clip.direction)] = clip;
                }
            }
            if (!catalog.TryGetValue(Key(skin, action, direction), out var result)) return null;
            if (result.textures == null)
            {
                var textures = new Texture2D[result.frames.Length];
                for (var index = 0; index < textures.Length; index++)
                {
                    var file = result.frames[index].file;
                    if (!file.EndsWith(".png", StringComparison.Ordinal)) return null;
                    textures[index] = Resources.Load<Texture2D>(ResourceRoot + file.Substring(0, file.Length - 4));
                    if (textures[index] == null || textures[index].width != result.canvas[0] || textures[index].height != result.canvas[1]) return null;
                }
                result.textures = textures;
            }
            return result;
        }

        static bool Valid(Clip clip)
        {
            if (clip == null || clip.canvas == null || clip.canvas.Length != 2 || clip.registration == null || clip.registration.Length != 2 || clip.frames == null || clip.frames.Length == 0) return false;
            foreach (var frame in clip.frames)
                if (frame == null || string.IsNullOrEmpty(frame.file) || frame.durationMs <= 0 || float.IsNaN(frame.durationMs) || float.IsInfinity(frame.durationMs)) return false;
            return clip.loop == null || clip.loop.Length == 0 || clip.loop.Length == 2 && clip.loop[0] >= 0 && clip.loop[0] < clip.loop[1] && clip.loop[1] <= clip.frames.Length;
        }

        public static bool CanWalk(string skin)
        {
            foreach (var direction in new[] { "left", "right" })
                foreach (var action in new[] { "walk-start", "walk", "walk-stop" })
                    if (Find(skin, action, direction) == null) return false;
            return true;
        }

        public static bool CanFeed(string skin)
        {
            if (!CanWalk(skin) || Find(skin, "eat", "left") == null || Find(skin, "eat", "right") == null) return false;
            foreach (var action in new[] { "food-pour", "food-fill", "bowl-food" })
                if (Find("shared", action) == null) return false;
            return true;
        }

        public static Vector2 MouthContact(string direction) => direction == "left" ? new Vector2(149, 341) : new Vector2(363, 339);
    }
}
