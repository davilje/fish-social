using System;
using System.Collections.Generic;
using FishSocial.Desktop.Auth;
using UnityEngine;

namespace FishSocial.Desktop
{
    /// <summary>
    /// FEAT-SPOT-02: on seat, tag-matched observation from spot_clue_texts + pond_spot_tags.
    /// </summary>
    public static class SpotObservationController
    {
        const string Nickname = "观察";

        static string _pondId = string.Empty;
        static string _spotId = string.Empty;
        static string _lastClueId = string.Empty;
        static string _messageId = string.Empty;
        static string _text = string.Empty;

        public static void Clear()
        {
            _pondId = string.Empty;
            _spotId = string.Empty;
            _messageId = string.Empty;
            _text = string.Empty;
        }

        public static void ResetPond()
        {
            Clear();
            _lastClueId = string.Empty;
        }

        public static NativeOverlayChatDto Resolve(
            string pondId,
            string spotId,
            FishingProgressDto progress)
        {
            if (string.IsNullOrEmpty(spotId))
            {
                Clear();
                return null;
            }

            if (string.Equals(_pondId, pondId, StringComparison.Ordinal) &&
                string.Equals(_spotId, spotId, StringComparison.Ordinal) &&
                !string.IsNullOrEmpty(_messageId))
            {
                return Current();
            }

            var picked = Pick(pondId, spotId, progress);
            if (picked == null)
            {
                Clear();
                return null;
            }

            _pondId = pondId ?? string.Empty;
            _spotId = spotId;
            _lastClueId = picked.clueId ?? string.Empty;
            _messageId = "obs-" + _lastClueId + "-" +
                         DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            _text = picked.clueText != null ? picked.clueText.Trim() : string.Empty;
            if (string.IsNullOrEmpty(_text))
            {
                Clear();
                return null;
            }

            return Current();
        }

        static NativeOverlayChatDto Current()
        {
            if (string.IsNullOrEmpty(_messageId) || string.IsNullOrEmpty(_text))
                return null;
            return new NativeOverlayChatDto
            {
                messageId = _messageId,
                userId = string.Empty,
                playerId = string.Empty,
                nickname = Nickname,
                text = _text,
                sentAtMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            };
        }

        static DesktopGameData.SpotClueTextDef Pick(
            string pondId, string spotId, FishingProgressDto progress)
        {
            var table = DesktopGameData.SpotClueTexts;
            if (table == null || table.Length == 0)
                return null;

            var spotTags = DesktopGameData.GetSpotTags(pondId, spotId);
            if (spotTags == null || spotTags.Length == 0)
                return null;

            var tagSet = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < spotTags.Length; i++)
            {
                if (!string.IsNullOrEmpty(spotTags[i]))
                    tagSet.Add(spotTags[i]);
            }

            var playerLevel = progress != null && progress.level > 0 ? progress.level : 1;
            var pondLevel = ResolvePondLevel(progress, pondId);
            var pond = DesktopGameData.GetPond(pondId);
            var pondCategory = pond != null ? pond.pondCategory ?? string.Empty : string.Empty;

            var pool = new List<DesktopGameData.SpotClueTextDef>();
            for (var i = 0; i < table.Length; i++)
            {
                var row = table[i];
                if (row == null || !row.enabled)
                    continue;
                if (string.IsNullOrEmpty(row.clueText))
                    continue;
                if (playerLevel < Math.Max(0, row.minPlayerLevel) ||
                    pondLevel < Math.Max(0, row.minPondLevel))
                    continue;
                if (!string.IsNullOrEmpty(row.pondCategory) &&
                    !string.Equals(row.pondCategory, pondCategory, StringComparison.Ordinal))
                    continue;
                var tag = row.spotTag != null ? row.spotTag.Trim() : string.Empty;
                if (string.IsNullOrEmpty(tag) || !tagSet.Contains(tag))
                    continue;
                pool.Add(row);
            }

            if (pool.Count == 0)
                return null;

            if (pool.Count > 1 && !string.IsNullOrEmpty(_lastClueId))
            {
                var filtered = new List<DesktopGameData.SpotClueTextDef>(pool.Count);
                for (var i = 0; i < pool.Count; i++)
                {
                    if (!string.Equals(pool[i].clueId, _lastClueId, StringComparison.Ordinal))
                        filtered.Add(pool[i]);
                }
                if (filtered.Count > 0)
                    pool = filtered;
            }

            // 50/50 habitat vs activity when both exist
            var habitat = new List<DesktopGameData.SpotClueTextDef>();
            var activity = new List<DesktopGameData.SpotClueTextDef>();
            for (var i = 0; i < pool.Count; i++)
            {
                if (string.Equals(pool[i].clueType, "habitat", StringComparison.Ordinal))
                    habitat.Add(pool[i]);
                else if (string.Equals(pool[i].clueType, "activity", StringComparison.Ordinal))
                    activity.Add(pool[i]);
            }
            var useHabitat = UnityEngine.Random.value < 0.5f;
            if (useHabitat && habitat.Count > 0)
                return WeightedPick(habitat);
            if (!useHabitat && activity.Count > 0)
                return WeightedPick(activity);
            return WeightedPick(pool);
        }

        static DesktopGameData.SpotClueTextDef WeightedPick(
            List<DesktopGameData.SpotClueTextDef> pool)
        {
            var total = 0;
            for (var i = 0; i < pool.Count; i++)
                total += Math.Max(1, pool[i].weight);
            var roll = UnityEngine.Random.Range(0, total);
            for (var i = 0; i < pool.Count; i++)
            {
                roll -= Math.Max(1, pool[i].weight);
                if (roll < 0)
                    return pool[i];
            }

            return pool[pool.Count - 1];
        }

        static int ResolvePondLevel(FishingProgressDto progress, string pondId)
        {
            var rows = progress != null ? progress.pondProficiencies : null;
            if (rows == null)
                return 1;
            for (var i = 0; i < rows.Length; i++)
            {
                var row = rows[i];
                if (row != null &&
                    string.Equals(row.pondId, pondId, StringComparison.Ordinal))
                    return row.level > 0 ? row.level : 1;
            }

            return 1;
        }
    }
}
