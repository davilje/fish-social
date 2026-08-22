using System;
using System.Collections.Generic;
using FishSocial.Desktop.Auth;
using UnityEngine;

namespace FishSocial.Desktop
{
    /// <summary>
    /// FEAT-SPOT-01: on seat, weighted-random observation from spot_clue_texts.
    /// Keeps one bubble per seated spot; redraws on leave/change.
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

            var playerLevel = progress != null && progress.level > 0 ? progress.level : 1;
            var pondLevel = ResolvePondLevel(progress, pondId);
            var pond = DesktopGameData.GetPond(pondId);
            var pondCategory = pond != null ? pond.pondCategory ?? string.Empty : string.Empty;
            var spotTags = ResolveSpotTags(pondId, spotId);

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
                if (!TagMatches(row.spotTag, spotTags))
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

        static HashSet<string> ResolveSpotTags(string pondId, string spotId)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var rows = DesktopGameData.SpotTags;
            if (rows == null)
                return set;
            for (var i = 0; i < rows.Length; i++)
            {
                var row = rows[i];
                if (row == null)
                    continue;
                if (!string.Equals(row.pondId, pondId, StringComparison.Ordinal))
                    continue;
                if (!SpotIdMatches(spotId, row.spotId))
                    continue;
                if (string.IsNullOrEmpty(row.tags))
                    continue;
                var parts = row.tags.Split(',');
                for (var p = 0; p < parts.Length; p++)
                {
                    var tag = parts[p] != null ? parts[p].Trim() : string.Empty;
                    if (!string.IsNullOrEmpty(tag))
                        set.Add(tag);
                }
            }

            return set;
        }

        static bool TagMatches(string clueTag, HashSet<string> spotTags)
        {
            if (string.IsNullOrEmpty(clueTag))
                return true;
            if (spotTags == null || spotTags.Count == 0)
                return true;
            return spotTags.Contains(clueTag.Trim());
        }

        static bool SpotIdMatches(string liveId, string tableId)
        {
            if (string.IsNullOrEmpty(liveId) || string.IsNullOrEmpty(tableId))
                return false;
            if (string.Equals(liveId, tableId, StringComparison.Ordinal))
                return true;
            return liveId.EndsWith("-" + tableId, StringComparison.Ordinal) ||
                   tableId.EndsWith("-" + liveId, StringComparison.Ordinal);
        }
    }
}
