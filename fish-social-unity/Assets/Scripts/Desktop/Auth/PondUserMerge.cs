using System;

namespace FishSocial.Desktop.Auth
{
    /// <summary>
    /// BUG-13 guards for pond_user_updated / session_timer_tick merges.
    /// Overlay tooltip reads sessionFishingMs from this merged roster.
    /// </summary>
    static class PondUserMerge
    {
        public static PondUserDto Merge(PondUserDto prev, PondUserDto incoming)
        {
            if (incoming == null)
                return prev;
            if (prev == null)
                return incoming;

            var merged = Copy(incoming);
            merged.sessionCatchCount = Math.Max(prev.sessionCatchCount, incoming.sessionCatchCount);
            var nextPhase = string.IsNullOrEmpty(incoming.fishingPhase)
                ? prev.fishingPhase
                : incoming.fishingPhase;
            var stillFishing = IsFishingActive(nextPhase) && nextPhase != "stopping";

            if (!stillFishing)
                return merged;

            PreserveSessionTimer(prev, merged, nextPhase);
            PreserveHookDeadline(prev, merged, nextPhase);
            PreserveFishingAnchors(prev, merged, incoming, nextPhase);
            return merged;
        }

        static void PreserveSessionTimer(PondUserDto prev, PondUserDto merged, string nextPhase)
        {
            if (!IsFishingActive(nextPhase))
                return;

            if (merged.sessionFishingMs > 0)
                return;

            if (prev.sessionFishingMs <= 0)
                return;

            var anchor = SessionAnchor(merged);
            if (anchor > 0)
            {
                merged.sessionFishingMs = Math.Max(
                    prev.sessionFishingMs,
                    DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - anchor);
                return;
            }

            merged.sessionFishingMs = prev.sessionFishingMs;
        }

        static void PreserveHookDeadline(PondUserDto prev, PondUserDto merged, string nextPhase)
        {
            if (nextPhase != "hooked")
                return;
            if (merged.phaseEndsAt > 0)
                return;
            if (prev.phaseEndsAt > 0)
                merged.phaseEndsAt = prev.phaseEndsAt;
        }

        static void PreserveFishingAnchors(
            PondUserDto prev,
            PondUserDto merged,
            PondUserDto incoming,
            string nextPhase)
        {
            var prevAnchor = SessionAnchor(prev);
            var incomingAnchor = SessionAnchor(incoming);

            if (nextPhase == "waiting" && incomingAnchor == 0 && prevAnchor > 0)
            {
                merged.fishingStartedAt = prev.fishingStartedAt > 0
                    ? prev.fishingStartedAt
                    : prev.sessionStartedAt;
                merged.sessionStartedAt = merged.fishingStartedAt;
                return;
            }

            if (incomingAnchor == 0 && prevAnchor > 0 && IsFishingActive(nextPhase))
            {
                merged.fishingStartedAt = prev.fishingStartedAt;
                merged.sessionStartedAt = prev.sessionStartedAt;
            }
        }

        static PondUserDto Copy(PondUserDto source)
        {
            return new PondUserDto
            {
                id = source.id,
                playerId = source.playerId,
                nickname = source.nickname,
                spotId = source.spotId,
                status = source.status,
                fishingPhase = source.fishingPhase,
                fishingStartedAt = source.fishingStartedAt,
                sessionStartedAt = source.sessionStartedAt,
                todayFishingMs = source.todayFishingMs,
                todayFishingBaseMs = source.todayFishingBaseMs,
                todayRemainingMs = source.todayRemainingMs,
                sessionFishingMs = source.sessionFishingMs,
                sessionCatchCount = source.sessionCatchCount,
                phaseEndsAt = source.phaseEndsAt,
                isBot = source.isBot,
                groundbait = source.groundbait,
                returnFeeMode = source.returnFeeMode,
            };
        }

        static bool IsFishingActive(string phase)
        {
            return phase == "waiting" ||
                   phase == "baiting" ||
                   phase == "casting" ||
                   phase == "hooked" ||
                   phase == "resolving";
        }

        static long SessionAnchor(PondUserDto user)
        {
            if (user == null)
                return 0;
            if (user.fishingStartedAt > 0)
                return user.fishingStartedAt;
            return user.sessionStartedAt > 0 ? user.sessionStartedAt : 0;
        }
    }
}
