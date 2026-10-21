#if UNITY_EDITOR
using System;
using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Desktop.Editor
{
    public static partial class CozyRedesignAuthoring
    {
        static void ApplyInlineParity(GameObject root)
        {
            var t = root.transform;
            switch (root.name)
            {
                case "PanelShop": case "PanelEquipment":
                    InlineAction(t,"Details/Buy",true,false);
                    InlineAction(t,"Details/Equip",false,true);
                    InlineAction(t,"Details/BrowseShop",false,false);
                    break;
                case "PanelCatch":
                    InlineAction(t,"Sell",false,true);
                    InlineAction(t,"Return",false,false);
                    InlineAction(t,"Share",false,false);
                    InlineAction(t,"EquipItem",false,true);
                    break;
                case "ShopItemCard":
                    var meta = t.Find("Meta")?.GetComponent<Text>();
                    if (meta == null) break;
                    var bounds = Place(t,"MetaInlineBounds",10,161,190,20);
                    bounds.anchorMin = Vector2.up; bounds.anchorMax = Vector2.one;
                    bounds.sizeDelta = new Vector2(-20,20);
                    var coin = Picture(t,"MetaCoin",CatTheme.LoadCozy("icons/coin.png",Vector4.zero),10,162,18,18);
                    var price = bounds.GetComponent<CozyJournalInlineContent>() ?? bounds.gameObject.AddComponent<CozyJournalInlineContent>();
                    price.ConfigureCentered(bounds,meta,coin,new Vector2(18,18),6,0,numericPriceOnly:true);
                    break;
                case "DesktopShell":
                    var card = t.Find("MainRoot/Header/ProfileCard") as RectTransform;
                    if (card == null) throw new InvalidOperationException("DesktopShell requires MainRoot/Header/ProfileCard for inline nickname and level layout.");
                    var nickname = card.Find("Name")?.GetComponent<Text>();
                    var badge = card.Find("LevelBadge")?.GetComponent<Image>();
                    var level = card.Find("Level")?.GetComponent<Text>();
                    if (nickname == null || badge == null || level == null)
                        throw new InvalidOperationException("DesktopShell profile inline layout requires Name Text, LevelBadge Image and Level Text.");
                    var profile = card.GetComponent<CozyJournalInlineContent>() ?? card.gameObject.AddComponent<CozyJournalInlineContent>();
                    profile.ConfigureProfile(card,nickname,badge,level);
                    break;
            }
        }

        static void InlineAction(Transform root, string path, bool purchase, bool fish)
        {
            var button = root.Find(path) as RectTransform;
            var label = button?.Find("Label")?.GetComponent<Text>();
            if (button == null || label == null) return;
            // These children are explicitly measured; no LayoutGroup may compete for their position.
            foreach (var group in button.GetComponents<LayoutGroup>()) UnityEngine.Object.DestroyImmediate(group);
            foreach (var fitter in button.GetComponents<ContentSizeFitter>()) UnityEngine.Object.DestroyImmediate(fitter);
            var icon = button.Find("Icon")?.GetComponent<Image>() ?? button.Find("CozyIcon")?.GetComponent<Image>();
            Image prefix = null;
            var miniFish = CozyJournalTheme.Sprite("motion/inline-mini-fish");
            if (purchase)
                prefix = Picture(button,"InlineFish",miniFish,0,0,30,22);
            if (fish && icon == null)
                icon = Picture(button,"Icon",miniFish,0,0,30,22);
            var size = fish ? new Vector2(30,22) : new Vector2(28,28);
            var component = button.GetComponent<CozyJournalInlineContent>() ?? button.gameObject.AddComponent<CozyJournalInlineContent>();
            component.ConfigureCentered(button,label,icon,size,10,12,prefix,fish ? miniFish : null);
        }
    }
}
#endif
