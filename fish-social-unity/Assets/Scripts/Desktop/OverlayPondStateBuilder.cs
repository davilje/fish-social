using System;
using FishSocial.Desktop.Auth;
using FishSocial.Desktop.Onboarding;
using FishSocial.Desktop.Pet;

namespace FishSocial.Desktop
{
    /// <summary>
    /// Copies pond_snapshot into the Overlay DTO, or the local onboarding tutorial
    /// when DesktopOnboardingController is active.
    /// Overlay only renders; it does not invent spots or other players except the
    /// scripted STEAM-DESKTOP-11 tutorial spots.
    /// </summary>
    public static class OverlayPondStateBuilder
    {
        public static void Fill(NativeOverlayStateDto dto, SocialPondSessionController pond)
        {
            if (dto == null)
                return;

            var onboarding = DesktopOnboardingController.Instance;
            if (onboarding != null && onboarding.IsOnboardingActive)
            {
                onboarding.FillOverlayState(dto);
                return;
            }

            var snapshot = pond != null ? pond.LatestSnapshot : null;
            dto.pondId = pond != null ? pond.CurrentPondId ?? string.Empty : string.Empty;
            dto.pondName = snapshot?.pond?.name ?? string.Empty;
            dto.ownSpotId = pond?.CurrentUser?.spotId ?? string.Empty;
            dto.ownNickname = pond?.CurrentUser?.nickname ?? pond?.Nickname ?? string.Empty;
            dto.ownPlayerId = pond?.CurrentUser?.playerId ?? string.Empty;
            dto.ownUserId = pond?.CurrentUser?.id ?? string.Empty;
            dto.sessionFishingMs = ResolveSessionFishingMs(pond != null ? pond.CurrentUser : null);
            dto.hookDeadlineMs = pond?.CurrentUser?.phaseEndsAt ?? 0L;
            dto.ownFishingStartedAt = SessionAnchor(pond != null ? pond.CurrentUser : null);
            dto.hasOwnPosition = false;
            dto.ownX = 0f;
            dto.ownY = 0f;
            dto.spots = MapSpots(snapshot?.pond?.spots);
            FillOwnPosition(dto);
            dto.users = MapOthers(pond, dto.spots);
            dto.recentChats = MapRecentChats(pond);
            dto.hasPendingCatch = pond != null && pond.HasPendingCatch;
            dto.availableActions = MapAvailableActions(pond);
            dto.guideTip = ResolvePoliceGuideTip(pond);
            dto.lockFeatureNav = false;
            dto.overlayPromptKind = string.Empty;
            dto.overlayPromptTitle = string.Empty;
            dto.overlayPromptBody = string.Empty;
            dto.overlayPromptButton = string.Empty;
            dto.overlayPromptDeadlineMs = 0;
        }

        static string[] MapAvailableActions(SocialPondSessionController pond)
        {
            if (pond == null || pond.State != SocialSocketState.Connected)
                return new string[0];

            var actions = new System.Collections.Generic.List<string>();
            var hasSpot = !string.IsNullOrEmpty(pond.CurrentUser?.spotId);
            if (!hasSpot)
                actions.Add("take_spot");
            if (hasSpot && pond.CanStartFishing)
                actions.Add("start_fishing");
            if (pond.CanStopFishing)
                actions.Add("stop_fishing");
            if (pond.HasPendingCatch)
                actions.Add("accept_catch");
            if (hasSpot && !pond.CanStopFishing)
                actions.Add("leave_spot");
            if (!pond.IsTransitioning)
                actions.Add("exit_pond");
            if (ShowPoliceDebug(pond.CurrentPondId))
                actions.Add("debug_police_raid");
            return actions.ToArray();
        }

        static bool ShowPoliceDebug(string pondId)
        {
            var pond = DesktopGameData.GetPond(pondId);
            return pond != null && pond.pondCategory == "forbidden";
        }

        static string ResolvePoliceGuideTip(SocialPondSessionController pond)
        {
            var raid = pond != null ? pond.ActivePoliceRaid : null;
            if (raid == null || raid.status != "warning")
                return string.Empty;
            if (raid.deadlineMs > 0 &&
                raid.deadlineMs < DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
                return string.Empty;
            return string.IsNullOrEmpty(raid.text) ? PoliceRaidDto.WarningText : raid.text;
        }

        static NativeOverlayChatDto[] MapRecentChats(SocialPondSessionController pond)
        {
            var source = pond != null ? pond.OverlayRecentChats() : null;
            if (source == null || source.Length == 0)
                return new NativeOverlayChatDto[0];

            var chats = new NativeOverlayChatDto[source.Length];
            for (var i = 0; i < source.Length; i++)
            {
                var message = source[i];
                var userId = message != null ? message.userId ?? string.Empty : string.Empty;
                chats[i] = new NativeOverlayChatDto
                {
                    messageId = message != null ? message.id ?? string.Empty : string.Empty,
                    userId = userId,
                    playerId = ResolvePlayerId(pond, userId),
                    nickname = message != null ? message.nickname ?? string.Empty : string.Empty,
                    text = message != null ? message.text ?? string.Empty : string.Empty,
                    sentAtMs = message != null ? message.createdAt : 0L,
                };
            }

            return chats;
        }

        static string ResolvePlayerId(SocialPondSessionController pond, string userId)
        {
            if (string.IsNullOrEmpty(userId) || pond == null)
                return string.Empty;

            var self = pond.CurrentUser;
            if (self != null && string.Equals(self.id, userId, StringComparison.Ordinal))
                return self.playerId ?? string.Empty;

            var others = pond.VisibleOthers;
            if (others == null)
                return string.Empty;

            for (var i = 0; i < others.Length; i++)
            {
                var user = others[i];
                if (user != null && string.Equals(user.id, userId, StringComparison.Ordinal))
                    return user.playerId ?? string.Empty;
            }

            return string.Empty;
        }

        static NativeOverlaySpotDto[] MapSpots(FishingSpotDto[] source)
        {
            if (source == null || source.Length == 0)
                return new NativeOverlaySpotDto[0];

            var spots = new NativeOverlaySpotDto[source.Length];
            for (var i = 0; i < source.Length; i++)
            {
                var spot = source[i];
                spots[i] = new NativeOverlaySpotDto
                {
                    id = spot != null ? spot.id ?? string.Empty : string.Empty,
                    x = spot != null ? spot.x : 0f,
                    y = spot != null ? spot.y : 0f,
                };
            }

            return spots;
        }

        static void FillOwnPosition(NativeOverlayStateDto dto)
        {
            if (string.IsNullOrEmpty(dto.ownSpotId) || dto.spots == null)
                return;
            for (var i = 0; i < dto.spots.Length; i++)
            {
                var spot = dto.spots[i];
                if (spot == null || spot.id != dto.ownSpotId)
                    continue;
                dto.ownX = spot.x;
                dto.ownY = spot.y;
                dto.hasOwnPosition = true;
                return;
            }
        }

        static NativeOverlayActorDto[] MapOthers(
            SocialPondSessionController pond, NativeOverlaySpotDto[] spots)
        {
            var others = pond != null ? pond.VisibleOthers : null;
            if (others == null || others.Length == 0)
                return new NativeOverlayActorDto[0];

            var users = new NativeOverlayActorDto[others.Length];
            for (var i = 0; i < others.Length; i++)
            {
                var user = others[i];
                var actor = new NativeOverlayActorDto
                {
                    playerId = user != null ? user.playerId ?? string.Empty : string.Empty,
                    userId = user != null ? user.id ?? string.Empty : string.Empty,
                    nickname = user != null ? user.nickname ?? string.Empty : string.Empty,
                    spotId = user != null ? user.spotId ?? string.Empty : string.Empty,
                    petVisualState = PetStateController.ToWire(
                        PetStateController.FromFishingPhase(user != null ? user.fishingPhase : null)),
                    fishingPhase = user != null ? user.fishingPhase ?? string.Empty : string.Empty,
                    sessionFishingMs = ResolveSessionFishingMs(user),
                    hookDeadlineMs = user != null ? user.phaseEndsAt : 0L,
                    fishingStartedAt = SessionAnchor(user),
                    isBot = user != null && user.isBot,
                };
                if (TryFindSpot(spots, actor.spotId, out var x, out var y))
                {
                    actor.x = x;
                    actor.y = y;
                    actor.hasPosition = true;
                }

                users[i] = actor;
            }

            return users;
        }

        static long ResolveSessionFishingMs(PondUserDto user)
        {
            if (user == null)
                return 0;
            if (user.sessionFishingMs > 0)
                return user.sessionFishingMs;
            var anchor = SessionAnchor(user);
            if (anchor <= 0 || !IsFishingPhase(user.fishingPhase))
                return 0;
            var elapsed = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - anchor;
            return elapsed > 0 ? elapsed : 0;
        }

        static long SessionAnchor(PondUserDto user)
        {
            if (user == null)
                return 0;
            if (user.fishingStartedAt > 0)
                return user.fishingStartedAt;
            return user.sessionStartedAt > 0 ? user.sessionStartedAt : 0;
        }

        static bool IsFishingPhase(string phase)
        {
            return phase == "waiting" ||
                   phase == "baiting" ||
                   phase == "casting" ||
                   phase == "hooked" ||
                   phase == "resolving";
        }

        static bool TryFindSpot(
            NativeOverlaySpotDto[] spots, string spotId, out float x, out float y)
        {
            x = 0f;
            y = 0f;
            if (spots == null || string.IsNullOrEmpty(spotId))
                return false;
            for (var i = 0; i < spots.Length; i++)
            {
                if (spots[i] != null && spots[i].id == spotId)
                {
                    x = spots[i].x;
                    y = spots[i].y;
                    return true;
                }
            }

            return false;
        }
    }
}
