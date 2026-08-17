using FishSocial.Desktop.Auth;
using FishSocial.Desktop.Pet;

namespace FishSocial.Desktop
{
    /// <summary>
    /// Copies authoritative pond_snapshot / roster fields into the Overlay DTO.
    /// Overlay only renders; it does not invent spots or other players.
    /// </summary>
    public static class OverlayPondStateBuilder
    {
        public static void Fill(NativeOverlayStateDto dto, SocialPondSessionController pond)
        {
            if (dto == null)
                return;

            var snapshot = pond != null ? pond.LatestSnapshot : null;
            dto.pondId = pond != null ? pond.CurrentPondId ?? string.Empty : string.Empty;
            dto.pondName = snapshot?.pond?.name ?? string.Empty;
            dto.ownSpotId = pond?.CurrentUser?.spotId ?? string.Empty;
            dto.hasOwnPosition = false;
            dto.ownX = 0f;
            dto.ownY = 0f;
            dto.spots = MapSpots(snapshot?.pond?.spots);
            FillOwnPosition(dto);
            dto.users = MapOthers(pond, dto.spots);
            dto.hasPendingCatch = pond != null && pond.HasPendingCatch;
            dto.availableActions = MapAvailableActions(pond);
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
            return actions.ToArray();
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
