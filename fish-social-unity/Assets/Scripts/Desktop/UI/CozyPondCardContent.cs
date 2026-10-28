using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Desktop
{
    /// <summary>One content binding for live pond cards and native Editor preview fixtures.</summary>
    [DisallowMultipleComponent]
    public sealed class CozyPondCardContent : MonoBehaviour
    {
        public void Bind(WorldMapPondView pond,DesktopGameData.PondDef definition,PondSelectionState state)
        {
            if(pond==null)return;
            Write("Name",pond.displayName);
            Write("Description",DesktopGameData.CategoryLabel(pond.pondCategory)+
                (definition!=null&&!string.IsNullOrEmpty(definition.waterType)?" · "+DesktopGameData.WaterLabel(definition.waterType):""));
            var dot=Get<Text>("CategoryDot");if(dot!=null){dot.text="●";dot.color=CozyPondMarkerView.CategoryColor(pond.pondCategory);dot.gameObject.SetActive(true);}
            Write("Fee",FeeLabel(pond));
            Write("Level",pond.minPlayerLevel>0?DesktopLocalization.Format("ui.fishing.pond_card.label.required_level", (object)(pond.minPlayerLevel)):DesktopLocalization.Text("ui.fishing.pond_card.label.no_level_requirement", "等级要求：无"));
            var access=Get<Text>("Access");
            var allowed=state!=null&&state.CanEnter(pond);
            if(access!=null){access.text=AccessLabel(pond,state);access.color=allowed?(Color)new Color32(113,137,80,255):(Color)new Color32(146,131,114,255);access.gameObject.SetActive(true);
                var badge=transform.Find("AccessBadge");
                if(badge!=null && access.transform.GetSiblingIndex()<badge.GetSiblingIndex())access.transform.SetSiblingIndex(badge.GetSiblingIndex()+1);
            }
            var species=DesktopGameData.PondSpeciesSummary(pond.pondId,3);
            Write("Species",string.IsNullOrEmpty(species)?DesktopLocalization.Text("ui.fishing.pond_card.hint.choose_pond", "选择鱼塘，查看水域详情"):DesktopLocalization.Format("ui.fishing.pond_card.label.species_count", (object)(species)));
            var photo=Get<Image>("Photo");
            if(photo!=null){photo.sprite=Resources.Load<Sprite>("Desktop/Theme/Cozy/journal/ponds/"+pond.pondId);photo.enabled=photo.sprite!=null;photo.preserveAspect=false;photo.color=new Color(1,1,1,allowed?1:.65f);photo.raycastTarget=false;}
        }
        T Get<T>(string path) where T:Component => transform.Find(path)?.GetComponent<T>();
        void Write(string path,string value)
        {var text=Get<Text>(path);if(text!=null){text.text=value;text.enabled=true;if(path!="Level")text.gameObject.SetActive(true);}}
        public static string FeeLabel(WorldMapPondView pond)
        {
            if(pond==null)return "";
            if(PondSelectionState.HasDualFee(pond))return DesktopLocalization.Format("ui.fishing.pond_card.format.fee_rate", (object)(pond.feePer2hSellOnly), (object)(pond.feePer2hAutoReturn));
            var fee=pond.feePer2hSellOnly>0?pond.feePer2hSellOnly:pond.feePer2h;
            return fee>0?DesktopLocalization.Format("ui.fishing.pond_card.format.fee", (object)(fee)):DesktopLocalization.Text("ui.fishing.pond_card.label.free", "免费");
        }
        public static string AccessLabel(WorldMapPondView pond,PondSelectionState state)
        {
            if(pond==null)return DesktopLocalization.Text("ui.fishing.pond_card.access.choose_pond", "请选择鱼塘");
            if(!pond.isOpen||pond.pondCategory=="giant")return DesktopLocalization.Text("ui.fishing.pond_card.access.unreleased", "暂未开放");
            if(state==null||state.ProgressLoading)return DesktopLocalization.Text("ui.fishing.pond_card.access.checking", "核对条件中");
            if(state.Progress==null)return DesktopLocalization.Text("ui.fishing.pond_card.access.unverified", "条件待核对");
            if(!state.Progress.onboardingCompleted)return DesktopLocalization.Text("ui.fishing.pond_card.access.tutorial_required", "先完成引导");
            return state.CanEnter(pond)?DesktopLocalization.Text("ui.fishing.pond_card.access.available", "可进入"):DesktopLocalization.Format("ui.fishing.pond_card.access.level_required", (object)(pond.minPlayerLevel));
        }
    }
}
