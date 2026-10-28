using System;
using System.Collections;
using FishSocial.Desktop.Auth;

namespace FishSocial.Desktop
{
    /// <summary>One selection and entry state shared by the map and pond list.</summary>
    public sealed class PondSelectionState
    {
        public event Action Changed;
        IAuthenticatedApiClient _api;
        int _progressVersion;
        object _progressOwner;
        public WorldMapPondView Selected { get; private set; }
        public FishingProgressDto Progress { get; private set; }
        public string ReturnFeeMode { get; private set; } = "sell_only";
        public bool ProgressLoading { get; private set; }
        public bool Busy { get; private set; }
        public bool EntryFailed { get; private set; }
        public string Status { get; private set; } = DesktopLocalization.Text("ui.map.selection.hint.pond_required", "请选择鱼塘。");
        public string ProgressError { get; private set; }

        public void Bind(IAuthenticatedApiClient api)
        {
            _api = api;
            ++_progressVersion;
            Progress = null;
            ProgressLoading = false;
            ProgressError = null;
            Busy = false;
            EntryFailed = false;
            Selected = null;
            ReturnFeeMode = "sell_only";
        }

        public void Select(WorldMapPondView pond)
        {
            if (Busy) return;
            var changed = Selected == null ? pond != null : pond == null || Selected.pondId != pond.pondId;
            Selected = pond;
            if (changed) { ReturnFeeMode = "sell_only"; EntryFailed = false; }
            if (!HasDualFee(pond)) ReturnFeeMode = "sell_only";
            if (changed) Status = pond == null ? DesktopLocalization.Text("ui.map.selection.hint.pond_required", "请选择鱼塘。") : AccessReason(pond);
            Changed?.Invoke();
        }

        public void SelectFeeMode(string mode)
        {
            if (Busy || !HasDualFee(Selected)) return;
            if (mode != "sell_only" && mode != "auto_return") return;
            ReturnFeeMode = mode;
            Status = DesktopLocalization.Format("ui.map.selection.status.mode_confirm", (object)((mode == "auto_return" ? DesktopLocalization.Text("ui.map.selection.status.release_mode_selected", "已选回鱼档") : DesktopLocalization.Text("ui.map.selection.status.sell_mode_selected", "已选出售档"))));
            Changed?.Invoke();
        }

        public void SetEntryStatus(bool busy, string message, bool failed = false)
        {
            Busy = busy;
            EntryFailed = failed;
            Status = message ?? string.Empty;
            Changed?.Invoke();
        }

        public IEnumerator LoadProgress(object owner = null)
        {
            var version = ++_progressVersion;
            _progressOwner = owner;
            ProgressLoading = true;
            ProgressError = null;
            Changed?.Invoke();
            if (version != _progressVersion) yield break;
            if (_api == null || !_api.CanUse)
            {
                ProgressLoading = false;
                Progress = null;
                ProgressError = DesktopLocalization.Text("ui.map.selection.error.sign_in_required", "请先完成登录。");
                Changed?.Invoke();
                yield break;
            }
            yield return _api.GetFishingProgress((ok, dto, error) =>
            {
                if (version != _progressVersion) return;
                ApplyProgress(ok ? dto : null, error);
            });
            if (version != _progressVersion) yield break;
            ProgressLoading = false;
            Changed?.Invoke();
        }

        public void CancelProgress(object owner)
        {
            if (!ProgressLoading || !ReferenceEquals(_progressOwner, owner)) return;
            ++_progressVersion;
            _progressOwner = null;
            ProgressLoading = false;
            Changed?.Invoke();
        }

        public void ApplyProgress(FishingProgressDto progress, string error = null)
        {
            Progress = progress;
            ProgressError = progress != null ? null :
                (string.IsNullOrEmpty(error) ? DesktopLocalization.Text("ui.map.selection.error.level_load_failed", "等级信息加载失败，重新打开页面可重试。") : error);
            Changed?.Invoke();
        }

        public bool CanEnter(WorldMapPondView pond)
        {
            return pond != null && pond.isOpen && pond.pondCategory != "giant" &&
                   Progress != null && !ProgressLoading && Progress.onboardingCompleted &&
                   Progress.level >= pond.minPlayerLevel;
        }

        public string AccessReason(WorldMapPondView pond)
        {
            if (pond == null) return DesktopLocalization.Text("ui.map.selection.hint.pond_required", "请选择鱼塘。");
            if (!pond.isOpen || pond.pondCategory == "giant") return DesktopLocalization.Text("ui.map.selection.access.closed", "暂未开放");
            if (ProgressLoading) return DesktopLocalization.Text("ui.map.selection.access.checking", "正在核对等级与入塘条件…");
            if (Progress == null) return string.IsNullOrEmpty(ProgressError) ? DesktopLocalization.Text("ui.map.selection.access.level_pending", "等待加载等级信息") : ProgressError;
            if (!Progress.onboardingCompleted) return DesktopLocalization.Text("ui.map.selection.access.tutorial_required", "请先完成新手引导");
            if (Progress.level < pond.minPlayerLevel)
                return DesktopLocalization.Format("ui.map.selection.access.level_required", (object)(pond.minPlayerLevel), (object)(Progress.level));
            return DesktopLocalization.Text("ui.map.selection.access.allowed", "可进入");
        }

        public int ActiveFee(WorldMapPondView pond)
        {
            if (pond == null) return 0;
            if (ReturnFeeMode == "auto_return" && HasDualFee(pond)) return pond.feePer2hAutoReturn;
            return pond.feePer2hSellOnly > 0 ? pond.feePer2hSellOnly : pond.feePer2h;
        }

        public static bool HasDualFee(WorldMapPondView pond)
        {
            return pond != null && pond.allowsAutoReturn && pond.feePer2hSellOnly > 0;
        }

        public static bool IsFree(WorldMapPondView pond)
        {
            return pond != null && pond.feePer2h <= 0 && pond.feePer2hSellOnly <= 0 &&
                   (!pond.allowsAutoReturn || pond.feePer2hAutoReturn <= 0);
        }
    }
}
