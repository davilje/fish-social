using System;
using System.Globalization;
using System.Text;
using FishSocial.Desktop.Auth;

namespace FishSocial.Desktop
{
    /// <summary>Reads capture snapshots only. Opening an old catch never ages it.</summary>
    public static class DesktopFishHistory
    {
        public static bool IsRecorded(FishHistorySnapshotDto history)
        {
            // JsonUtility can materialize an empty nested object for a missing JSON field.
            return history != null && !string.IsNullOrEmpty(history.fishId) && history.enteredAt > 0;
        }
        public static string Residence(long milliseconds)
        {
            var minutes = Math.Max(0, milliseconds) / 60000;
            if (minutes < 1) return "不足1分钟";
            var days = minutes / 1440;
            var hours = minutes % 1440 / 60;
            if (days > 0) return days + "天" + hours + "小时";
            return hours > 0 ? hours + "小时" + minutes % 60 + "分钟" : minutes + "分钟";
        }

        public static string Timestamp(long at)
        {
            if (at <= 0) return "时间未记录";
            try { return DateTimeOffset.FromUnixTimeMilliseconds(at).ToOffset(TimeSpan.FromHours(8)).ToString("yyyy/MM/dd HH:mm", CultureInfo.InvariantCulture); }
            catch (ArgumentOutOfRangeException) { return "时间未记录"; }
        }

        public static string Summary(FishHistorySnapshotDto history)
        {
            if (!IsRecorded(history)) return "这条鱼的入塘履历未记录";
            return "在塘" + Residence(history.residenceMs) + " · 入塘" + DesktopGameData.FormatFishSize(history.entrySizeM) +
                " · 长大" + DesktopGameData.FormatFishSize(history.growthSinceEntryM);
        }

        public static string Details(FishHistorySnapshotDto history)
        {
            if (!IsRecorded(history)) return Summary(null);
            var text = new StringBuilder(Summary(history));
            text.Append("\n入塘：").Append(Timestamp(history.enteredAt));
            text.Append("\n钓获：").Append(Timestamp(history.caughtAt));
            text.Append("\n吃钩饵").Append(history.hookFeedCount).Append("次 · 吃窝料").Append(history.groundbaitFeedCount).Append("份");
            text.Append("\n脱钩").Append(history.escapeCount).Append("次");
            foreach (var escape in history.escapes ?? Array.Empty<FishEscapeRecordDto>())
            {
                if (escape == null) continue;
                var pond = DesktopGameData.GetPond(escape.pondId);
                text.Append("\n").Append(Timestamp(escape.at)).Append(" · ").Append(pond?.name ?? escape.pondId)
                    .Append(" · ").Append(escape.spotId).Append(" · ").Append(DesktopGameData.FormatFishSize(escape.sizeM)).Append("时脱钩");
            }
            return text.ToString();
        }

        public static bool LandingReminderDue(string phase, long now, long deadline, long leadMs = 5000)
        {
            return phase == "hooked" && deadline > now && deadline - now <= Math.Max(1, leadMs);
        }
    }
}
