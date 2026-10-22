using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Desktop
{
    /// <summary>Binds both authored pond details to the same data, fees and access rules.</summary>
    public static class PondDetailView
    {
        public static void Refresh(Transform root, PondSelectionState state, string factsName)
        {
            if (root == null || state == null) return;
            var pond = state.Selected;
            var name = DesktopModalUi.FindComponent<Text>(root, "Name");
            var fee = DesktopModalUi.FindComponent<Text>(root, "FeeSummary");
            var facts = DesktopModalUi.FindComponent<Text>(root, "FactsScroll/" + factsName) ??
                        DesktopModalUi.FindComponent<Text>(root, factsName);
            var photo = DesktopModalUi.FindComponent<Image>(root, "Photo");
            if (photo != null)
            {
                var previousPhoto = photo.sprite;
                photo.sprite = pond == null ? null : Resources.Load<Sprite>("Desktop/Theme/Cozy/journal/ponds/" + pond.pondId);
                if(photo.sprite != previousPhoto && photo.sprite != null) RevealPhoto(root);
                photo.enabled = photo.sprite != null;
                photo.raycastTarget = false;
                photo.preserveAspect = false;
            }
            if (name != null) name.text = pond == null ? "鱼塘详情" : pond.displayName;
            var feeText = FeeSummary(state);
            if (fee != null) fee.text = feeText;
            if (facts != null)
            {
                facts.text = pond == null ? "点选鱼塘，查看真实场景和入塘条件。" :
                    (name == null ? pond.displayName + "\n" : "") + Facts(pond, state) +
                    (fee == null ? "\n" + feeText : "");
            }
            var status = DesktopModalUi.FindComponent<Text>(root, "StatusText");
            if (status != null)
                status.text = state.Busy ? "正在连接鱼塘…" : state.EntryFailed ? "进入失败，详情中可查看原因" :
                    state.ProgressLoading ? "正在核对入塘条件…" : state.Progress == null ? "等级信息未就绪，可重试" :
                    state.CanEnter(pond) ? "已准备好入塘" : "请查看入塘条件";
            var retry = DesktopModalUi.FindComponent<Button>(root, "Retry");
            if (retry != null)
            {
                retry.gameObject.SetActive(!state.Busy && !state.ProgressLoading && (state.EntryFailed || state.Progress == null));
                retry.interactable = !state.Busy && !state.ProgressLoading;
                var label = retry.GetComponentInChildren<Text>(true);
                if (label != null) label.text = state.EntryFailed && state.Progress != null ? "重试进入" : "重新加载";
            }
            var track = root.Find("FeeTrack");
            if (track != null)
            {
                track.gameObject.SetActive(PondSelectionState.HasDualFee(pond));
                var sell = root.Find("ModeSell"); var auto = root.Find("ModeAuto");
                if (sell != null && auto != null)
                {
                    var first = Mathf.Min(sell.GetSiblingIndex(), auto.GetSiblingIndex());
                    if (track.GetSiblingIndex() > first) track.SetSiblingIndex(first);
                }
            }
            RefreshMode(root, "ModeSell", "出售档", pond != null ? pond.feePer2hSellOnly : 0,
                state, "sell_only");
            RefreshMode(root, "ModeAuto", "回鱼档", pond != null ? pond.feePer2hAutoReturn : 0,
                state, "auto_return");
        }

        public static void RevealPhoto(Transform root)
        {
            if(root==null || !root.gameObject.activeInHierarchy)return;
            var frame=root.Find("PhotoFrame");if(frame==null)return;
            var pose=frame.GetComponent<CozyJournalStaticPose>();
            if(pose==null)
            {
                // Old authored prefabs already contain the exact frame, tape and native photo crop.
                // Transfer the permanent -2deg CSS pose to the shared reveal owner without moving hits.
                var previous=frame.GetComponent<CozyPondArtworkPose>();if(previous!=null)previous.enabled=false;
                pose=frame.gameObject.AddComponent<CozyJournalStaticPose>();
                pose.Configure(-2,frame.GetComponent<Graphic>(),root.Find("PhotoShadow/JournalShadow")?.GetComponent<Graphic>(),
                    root.Find("Photo")?.GetComponent<Graphic>(),root.Find("Name")?.GetComponent<Graphic>(),root.Find("PhotoTape")?.GetComponent<Graphic>());
            }
            pose.PlayReveal(new Vector2(24,0),.26f,.3f);
        }

        static void RefreshMode(Transform root, string path, string label, int fee, PondSelectionState state, string mode)
        {
            var button = DesktopModalUi.FindComponent<Button>(root, path);
            if (button == null) return;
            button.gameObject.SetActive(PondSelectionState.HasDualFee(state.Selected));
            button.interactable = !state.Busy;
            var text = button.GetComponentInChildren<Text>(true);
            if (text != null)
            {
                text.text = mode == "sell_only" ? "回鱼出售" : "自动回鱼";
                text.color = state.ReturnFeeMode == mode ? Color.white : CatTheme.Ink;
            }
            button.GetComponent<CozyButtonVisual>()?.SetSelected(state.ReturnFeeMode == mode);
        }

        static string FeeSummary(PondSelectionState state)
        {
            if (state.Selected == null) return string.Empty;
            var fee = state.ActiveFee(state.Selected);
            if (fee <= 0) return "免费入塘";
            var count = state.Progress != null ? state.Progress.todayFeeCharges.ToString() : "—";
            var cap = state.Selected.maxFeeChargesPerDay > 0 ? state.Selected.maxFeeChargesPerDay : 4;
            return fee + " 金币 / 2 小时 · 今日 " + count + "/" + cap + " 次";
        }

        static string Facts(WorldMapPondView pond, PondSelectionState state)
        {
            var text = new StringBuilder(DesktopGameData.CategoryLabel(pond.pondCategory));
            foreach (var definition in DesktopGameData.Ponds)
            {
                if (definition == null || definition.pondId != pond.pondId) continue;
                if (!string.IsNullOrEmpty(definition.waterType))
                    text.Append(" · ").Append(DesktopGameData.WaterLabel(definition.waterType));
                if (!string.IsNullOrEmpty(definition.realWorldRef))
                    text.Append("\n").Append(definition.realWorldRef);
                break;
            }
            text.Append("\n鱼种：").Append(DesktopGameData.PondSpeciesSummary(pond.pondId, 4));
            text.Append("\n").Append(state.AccessReason(pond));
            if (pond.minPlayerLevel > 0 && state.CanEnter(pond))
                text.Append(" · 等级 ≥ ").Append(pond.minPlayerLevel);
            if (PondSelectionState.HasDualFee(pond))
                text.Append("\n出售档：").Append(pond.feePer2hSellOnly).Append(" 金币 / 2小时；回鱼档：")
                    .Append(pond.feePer2hAutoReturn).Append(" 金币 / 2小时")
                    .Append(state.ReturnFeeMode == "auto_return" ? "\n当前回鱼档：达标自动回塘" : "\n当前出售档：鱼获仅可出售");
            if (!string.IsNullOrEmpty(state.Status) && state.Status != state.AccessReason(pond))
                text.Append("\n").Append(state.Status);
            return text.ToString();
        }
    }
}
