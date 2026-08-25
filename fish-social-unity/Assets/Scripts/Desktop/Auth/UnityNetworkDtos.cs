using System;

namespace FishSocial.Desktop.Auth
{
    public static class FishSocialProtocol
    {
        public const string Version = "1.0.0-draft";
    }

    public enum SocialSocketState
    {
        Disconnected,
        Connecting,
        Connected,
        Reconnecting,
        Failed,
    }

    [Serializable]
    public sealed class JoinPondPayload
    {
        public string pondId;
        public string nickname;
        public string playerId;
        // FEAT-RETURN-02
        public string returnFeeMode;
    }

    [Serializable]
    public sealed class StartFishingPayload
    {
        public string pondId;
        public string spotId;
    }

    [Serializable]
    public sealed class GroundbaitStartPayload
    {
        public string pondId;
        public string groundbaitId;
    }

    [Serializable]
    public sealed class TakeSpotPayload
    {
        public string pondId;
        public string spotId;
    }

    [Serializable]
    public sealed class LeaveSpotPayload
    {
        public string pondId;
    }

    [Serializable]
    public sealed class LeavePondPayload
    {
        public string pondId;
        public string reason;
    }

    [Serializable]
    public sealed class PondSnapshotDto
    {
        public PondConfigDto pond;
        public PondUserDto[] users;
        public ChatMessageDto[] messages;
        public FishInventoryItemDto[] inventory;
    }

    [Serializable]
    public sealed class PondConfigDto
    {
        public string id;
        public string name;
        public string regionId;
        public FishingSpotDto[] spots;
    }

    [Serializable]
    public sealed class FishingSpotDto
    {
        public string id;
        public float x;
        public float y;
    }

    [Serializable]
    public sealed class PondUserDto
    {
        public string id;
        public string playerId;
        public string nickname;
        public string spotId;
        public string status;
        public string fishingPhase;
        public long fishingStartedAt;
        public long sessionStartedAt;
        public long todayFishingMs;
        public long todayFishingBaseMs;
        public long todayRemainingMs;
        public long sessionFishingMs;
        public long phaseEndsAt;
        public bool isBot;
        public PondUserGroundbaitDto groundbait;
        // FEAT-RETURN-02
        public string returnFeeMode;
    }

    [Serializable]
    public sealed class PondUserGroundbaitDto
    {
        public string groundbaitId;
        public int stackCount;
        public long expiresAt;
        public int bitesLeft;
        public float biteBonus;
        public float sizeBonus;
    }

    [Serializable]
    public sealed class SessionTimerTickDto
    {
        public string userId;
        public long sessionFishingMs;
    }

    [Serializable]
    public sealed class ChatMessageDto
    {
        public string id;
        public string pondId;
        public string userId;
        public string nickname;
        public string text;
        public long createdAt;
        public string type;
    }

    [Serializable]
    public sealed class PoliceRaidDto
    {
        public const string WarningText = "巡警来了！快跑！";
        public string status;
        public string raidId;
        public string pondId;
        public string text;
        public long deadlineMs;
        public int coinsAfter;
        public int charged;
        public string message;
    }

    [Serializable]
    public sealed class SendChatPayload
    {
        public string pondId;
        public string text;
    }

    [Serializable]
    public sealed class CodexUnlockDto
    {
        public string speciesId;
        public string speciesName;
        public bool isFirstCatch;
    }

    [Serializable]
    public sealed class FriendInfoDto
    {
        public string playerId;
        public string nickname;
        public string avatarUrl;
        public long since;
    }

    [Serializable]
    public sealed class FriendRequestDto
    {
        public string id;
        public string fromPlayerId;
        public string fromNickname;
        public string toPlayerId;
        public string toNickname;
        public string status;
        public long createdAt;
    }

    [Serializable]
    public sealed class DirectMessageDto
    {
        public string id;
        public string fromPlayerId;
        public string fromNickname;
        public string toPlayerId;
        public string text;
        public long createdAt;
    }

    [Serializable]
    public sealed class DmConversationDto
    {
        public string friendPlayerId;
        public string friendNickname;
        public string lastMessage;
        public long lastAt;
        public int unread;
    }

    [Serializable]
    public sealed class FishCodexEntryDto
    {
        public string speciesId;
        public int totalCaught;
        public float maxSizeM;
        public long firstCaughtAt;
        public long lastCaughtAt;
    }

    [Serializable]
    public sealed class PendingFishCatchDto
    {
        public string catchId;
        public string pondFishId;
        public string speciesId;
        public string quality;
        public float sizeM;
        public int hookDurationMs;
        public bool isCodexNew;
    }

    [Serializable]
    public sealed class FishInventoryItemDto
    {
        public string id;
        public string speciesId;
        public string quality;
        public float sizeM;
        public long caughtAt;
        public string pondId;
    }

    [Serializable]
    public sealed class ShopBaitDto
    {
        public string id;
        public string name;
        public string icon;
        public int price;
        public float globalBonus;
        public bool consumed;
        public string diet;
        public int unlockPlayerLevel;
        public int costGoldPerUse;
        public bool isDefaultInfinite;
        public float biteBonusHerbivore;
        public float biteBonusOmnivore;
        public float biteBonusCarnivore;
    }

    [Serializable]
    public sealed class ShopTackleDto
    {
        public string id;
        public string name;
        public string icon;
        public int price;
        public float escapeReduction;
        public float biteBonus;
        public string subType;
        public float breakSizeM;
        public int breakMaxLandings;
        public float fitGray;
        public float fitGreen;
        public float fitBlue;
        public float fitPurple;
        public float fitRed;
        public float fitOrange;
        public float fitGold;
        public float fitStillBait;
        public float fitStreamLight;
        public float fitLurePredator;
        public float fitCastHeavy;
        public float fitGiantGame;
    }

    [Serializable]
    public sealed class ShopVesselDto
    {
        public string vesselId;
        public string name;
        public int unlockPlayerLevel;
        public int priceGold;
        public int placeholderCatchCount;
        public bool enabledUse;
    }

    [Serializable]
    public sealed class ShopGearDto
    {
        public string equippedBait = "bait-basic";
        public string equippedTackle = "basic";
        public string equippedRod = "";
        public string[] ownedTackles = new string[0];
        public string[] ownedRods = new string[0];
        public string[] unlockedBaits = new string[0];
        public string[] ownedVessels = new string[0];
        public int playerLevel = 1;
        public int basic;
        public int corn;
        public int pellet;
        public int live;

        public int BaitCount(string baitId)
        {
            switch (baitId)
            {
                case "corn": return corn;
                case "pellet": return pellet;
                case "live": return live;
                default: return basic;
            }
        }
    }

    [Serializable]
    public sealed class JoinPondAckDto
    {
        public bool ok;
        public string userId;
        public string error;
        public long todayFishingBaseMs;
        public long todayRemainingMs;
        public string quotaDateKey;
        public string returnFeeMode;
        public int feePer2hSellOnly;
        public int feePer2hAutoReturn;
        public bool allowsAutoReturn;
    }

    [Serializable]
    public sealed class SocketActionAckDto
    {
        public bool ok;
        public string error;
        public bool autoReturned;
        public int gold;
        public int playerXp;
        public int pondXp;
        public float newSizeM;
        public float sizeGainM;
        public int totalCoins;
    }

    [Serializable]
    public sealed class FishCatchSettledDto
    {
        public string speciesId;
        public string quality;
        public float sizeM;
        public bool autoReturned;
        public int gold;
        public int playerXp;
        public int pondXp;
        public float newSizeM;
        public float sizeGainM;
        public int totalCoins;
        public string message;
    }

    [Serializable]
    public sealed class PondSessionCatchEntryDto
    {
        public string speciesId;
        public string quality;
        public float sizeM;
        public string outcome;
        public int returnGold;
        public int catchPlayerXp;
        public int catchPondXp;
        public int returnPlayerXp;
        public int returnPondXp;
        public long caughtAt;
    }

    [Serializable]
    public sealed class PondSessionSummaryDto
    {
        public string pondId;
        public string pondName;
        public string returnFeeMode;
        public PondSessionCatchEntryDto[] catches;
        public int feesPaid;
        public int totalReturnGold;
        public int totalCatchPlayerXp;
        public int totalCatchPondXp;
        public int totalReturnPlayerXp;
        public int totalReturnPondXp;
        public int netProfit;
        public long joinedAt;
        public long leftAt;
    }

    [Serializable]
    public sealed class PlayerProfileDto
    {
        public const int ShowcaseSlotCount = 8;
        public string playerId;
        public string nickname;
        public int coins;
        public string shareVisibility;
        public long createdAt;
        public string avatarUrl;
        public string bio;
        public string[] showcaseFishIds;
    }

    [Serializable]
    public sealed class AlbumCardDto
    {
        public string id;
        public string speciesId;
        public string quality;
        public float sizeM;
        public string pondId;
        public string pondName;
        public string source;
        public long eventAt;
        public string inventoryItemId;
        public string photoUrl;
    }

    [Serializable]
    public sealed class AchievementViewDto
    {
        public string achievementId;
        public string name;
        public string desc;
        public string iconKey;
        public string category;
        public string conditionType;
        public float conditionValue;
        public int sortOrder;
        public bool isHidden;
        public bool unlocked;
        public long unlockedAt;
    }

    [Serializable]
    public sealed class AchievementUnlockDto
    {
        public string achievementId;
        public string name;
        public string desc;
    }

    [Serializable]
    public sealed class ProfileHubProgressDto
    {
        public int level;
        public int xp;
    }

    [Serializable]
    public sealed class ProfileHubCodexSummaryDto
    {
        public int unlockedCount;
        public int totalSpecies;
    }

    [Serializable]
    public sealed class ProfileHubProfileDto
    {
        public string playerId;
        public string nickname;
        public string avatarUrl;
        public string bio;
        public string shareVisibility;
        public int coins;
        public string[] showcaseFishIds;
    }

    [Serializable]
    public sealed class ProfileHubDto
    {
        public bool isSelf;
        public bool canEdit;
        public ProfileHubProfileDto profile;
        public ProfileHubProgressDto progress;
        public FishInventoryItemDto[] showcaseFish;
        public ProfileHubCodexSummaryDto codexSummary;
        public AlbumCardDto[] albumPins;
        public AlbumCardDto[] albumCandidates;
        public AchievementViewDto[] achievements;
        public int albumPinCap;
    }

    [Serializable]
    public sealed class PublicPlayerProfileDto
    {
        public string playerId;
        public string nickname;
        public string avatarUrl;
        public string bio;
        public string[] showcaseFishIds;
    }

    [Serializable]
    public sealed class PublicPlayerViewDto
    {
        public PublicPlayerProfileDto profile;
        public FishInventoryItemDto[] showcaseFish;
        public SocialPostDto[] posts;
    }

    [Serializable]
    public sealed class SocialPostDto
    {
        public string id;
        public string playerId;
        public string nickname;
        public string authorAvatarUrl;
        public FishInventoryItemDto fish;
        public string text;
        public string photoUrl;
        public string visibility;
        public long createdAt;
        public int likeCount;
        public int commentCount;
        public bool likedByMe;
    }

    [Serializable]
    public sealed class PostCommentDto
    {
        public string id;
        public string postId;
        public string playerId;
        public string nickname;
        public string avatarUrl;
        public string text;
        public long createdAt;
    }

    [Serializable]
    public sealed class PostLikedDto
    {
        public string postId;
        public string playerId;
        public bool liked;
        public int likeCount;
    }

    [Serializable]
    public sealed class PostCommentedDto
    {
        public string postId;
        public PostCommentDto comment;
    }

    [Serializable]
    public sealed class PostCommentDeletedDto
    {
        public string postId;
        public string commentId;
        public int commentCount;
    }

    [Serializable]
    public sealed class LeaderboardExtraDto
    {
        public string speciesId;
        public float sizeM;
        public string pondId;
        public int catchCount;
        public long caughtAt;
    }

    [Serializable]
    public sealed class LeaderboardEntryDto
    {
        public int rank;
        public string playerId;
        public string nickname;
        public string avatarUrl;
        public float value;
        public LeaderboardExtraDto extra;
    }

    [Serializable]
    public sealed class LeaderboardMyRankDto
    {
        public int rank;
        public bool hasRank;
        public float value;
        public LeaderboardEntryDto entry;
    }

    [Serializable]
    public sealed class PondProficiencyDto
    {
        public string pondId;
        public int level;
        public int xp;
    }

    /// <summary>
    /// Public fishing progress from GET /api/progress/me (FEAT-PROG-01).
    /// </summary>
    [Serializable]
    public sealed class FishingProgressDto
    {
        public int level;
        public int xp;
        public bool onboardingCompleted;
        public long onboardingCompletedAt;
        public int todayFeeCharges;
        public long feeProgressMs;
        public bool needsFeeToContinue;
        public PondProficiencyDto[] pondProficiencies;
    }
}
