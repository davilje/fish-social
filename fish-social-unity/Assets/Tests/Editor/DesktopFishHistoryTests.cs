#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using FishSocial.Desktop;
using FishSocial.Desktop.Auth;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace FishSocial.Tests.Editor
{
    public sealed class DesktopFishHistoryTests
    {
        const string HistoryJson = "{\"fishId\":\"fish-1\",\"enteredAt\":1793491200000,\"entrySizeM\":0.042," +
            "\"caughtAt\":1793685600000,\"residenceMs\":194400000,\"growthSinceEntryM\":0.143," +
            "\"individualCapM\":0.2,\"growthRuleVersion\":\"pond-entry-v1\",\"source\":\"pre_opening_seed\"," +
            "\"hookFeedCount\":2,\"groundbaitFeedCount\":6,\"escapeCount\":1," +
            "\"escapes\":[{\"at\":1793577600000,\"pondId\":\"pond-calm\",\"spotId\":\"spot-7\",\"sizeM\":0.16}]}";

        [Test]
        public void ExpandedCatalogExposesAllFortyPondsWithoutDuplicatePositions()
        {
            var ponds = WorldMapPondCatalog.LoadVisible();
            Assert.AreEqual(40, ponds.Length);
            Assert.AreEqual(40, ponds.Select(pond => pond.pondId).Distinct().Count());
            Assert.AreEqual(40, ponds.Select(pond => new Vector2(pond.x, pond.y)).Distinct().Count());
            var authored = WorldMapPondCatalog.Load();
            Assert.AreEqual(40, authored.Length, "The map must have authored water locations for all formal ponds.");
            foreach (var pond in ponds)
            {
                var position = authored.Single(item => item.pondId == pond.pondId);
                Assert.AreEqual(position.x, pond.x);
                Assert.AreEqual(position.y, pond.y);
            }
            Assert.IsTrue(ponds.All(pond => pond.capacity == 20 && pond.x >= 0 && pond.x <= 1 && pond.y >= 0 && pond.y <= 1));
        }

        [Test]
        public void NetworkDtosKeepTheSameCaptureHistory()
        {
            var json = "{\"history\":" + HistoryJson + "}";
            Assert.AreEqual("fish-1", JsonUtility.FromJson<PendingFishCatchDto>(json).history.fishId);
            Assert.AreEqual("fish-1", JsonUtility.FromJson<FishInventoryItemDto>(json).history.fishId);
            Assert.AreEqual("fish-1", JsonUtility.FromJson<FishCatchSettledDto>(json).history.fishId);
            Assert.AreEqual("fish-1", JsonUtility.FromJson<AlbumCardDto>(json).history.fishId);
        }

        [Test]
        public void SummaryUsesCapturedDurationAndActualEntryLength()
        {
            var history = JsonUtility.FromJson<FishHistorySnapshotDto>(HistoryJson);
            Assert.AreEqual("在塘2天6小时 · 入塘4.2cm · 长大14.3cm", DesktopFishHistory.Summary(history));
            StringAssert.Contains("spot-7", DesktopFishHistory.Details(history));
            StringAssert.Contains("吃钩饵2次 · 吃窝料6份", DesktopFishHistory.Details(history));
            Assert.AreEqual("这条鱼的入塘履历未记录", DesktopFishHistory.Details(null));
            var legacy = JsonUtility.FromJson<FishInventoryItemDto>("{\"id\":\"legacy\"}");
            Assert.IsFalse(DesktopFishHistory.IsRecorded(legacy.history));
            Assert.AreEqual("这条鱼的入塘履历未记录", DesktopFishHistory.Summary(legacy.history));
        }

        [Test]
        public void LandingReminderRejectsExpiredOrUnhookedFishAndHonorsLeadTime()
        {
            Assert.IsTrue(DesktopFishHistory.LandingReminderDue("hooked", 5000, 10000));
            Assert.IsFalse(DesktopFishHistory.LandingReminderDue("hooked", 4999, 10000));
            Assert.IsFalse(DesktopFishHistory.LandingReminderDue("hooked", 10000, 10000));
            Assert.IsFalse(DesktopFishHistory.LandingReminderDue("waiting", 9000, 10000));
            Assert.IsFalse(DesktopFishHistory.LandingReminderDue("hooked", 8000, 10000, 1000));
        }

        [Test]
        public void ReminderSoundIsDeduplicatedAndMuteConsumesIt()
        {
            var policy = new DesktopAudioPolicy(new DesktopAudioConfiguration());
            policy.Reset(new AudioPresentationDto { source = "authoritative", eventKind = "bite", eventId = "catch-1", eventDeadlineMs = 10000 });
            Assert.IsFalse(policy.ConsumeLandingReminder("catch-1", 4000, true));
            Assert.IsFalse(policy.ConsumeLandingReminder("catch-1", 5000, false));
            Assert.IsFalse(policy.ConsumeLandingReminder("catch-1", 6000, true));
            policy.Reset(new AudioPresentationDto { source = "authoritative", eventKind = "bite", eventId = "catch-2", eventDeadlineMs = 10000 });
            Assert.IsTrue(policy.ConsumeLandingReminder("catch-2", 6000, true));
            Assert.IsFalse(policy.ConsumeLandingReminder("catch-2", 7000, true));
            Assert.IsFalse(policy.ConsumeLandingReminder("another-fish", 7000, true));
        }
    }
}
#endif
