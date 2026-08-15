using FishSocial.Desktop.Auth;

namespace FishSocial.Desktop
{
    /// <summary>
    /// Copies authoritative pond_snapshot fields into the Overlay DTO.
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

            var source = snapshot?.pond?.spots;
            if (source == null || source.Length == 0)
            {
                dto.spots = new NativeOverlaySpotDto[0];
                return;
            }

            dto.spots = new NativeOverlaySpotDto[source.Length];
            for (var i = 0; i < source.Length; i++)
            {
                var spot = source[i];
                var copy = new NativeOverlaySpotDto
                {
                    id = spot != null ? spot.id ?? string.Empty : string.Empty,
                    x = spot != null ? spot.x : 0f,
                    y = spot != null ? spot.y : 0f,
                };
                dto.spots[i] = copy;
                if (!dto.hasOwnPosition &&
                    !string.IsNullOrEmpty(dto.ownSpotId) &&
                    copy.id == dto.ownSpotId)
                {
                    dto.ownX = copy.x;
                    dto.ownY = copy.y;
                    dto.hasOwnPosition = true;
                }
            }
        }
    }
}
