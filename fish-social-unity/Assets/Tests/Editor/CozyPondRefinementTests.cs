#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using FishSocial.Desktop;
using FishSocial.Desktop.Auth;
using FishSocial.Desktop.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Tests.Editor
{
    public sealed class CozyPondRefinementTests
    {
        GameObject root;
        [TearDown]public void Cleanup(){if(root!=null)Object.DestroyImmediate(root);}
        Transform Load(string name)
        {
            root=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(DesktopPrefabCatalog.AssetPath(name)));
            CozyRedesignAuthoring.ApplyPondRefinement(root);return root.transform;
        }
        [Test]public void CardShowsCategoryWaterFeesSpeciesAndClosedStateUsingRealBinding()
        {
            var t=Load("PondSelectionCard");var state=new PondSelectionState();state.ApplyProgress(new FishingProgressDto{level=99,onboardingCompleted=true});
            var pond=new WorldMapPondView{pondId="pond-calm",displayName="测试鱼塘",pondCategory="reservoir",isOpen=true,allowsAutoReturn=true,feePer2hSellOnly=120,feePer2hAutoReturn=240,minPlayerLevel=3};
            var definition=new DesktopGameData.PondDef{waterType="reservoir"};
            t.GetComponent<CozyPondCardContent>().Bind(pond,definition,state);
            StringAssert.Contains("水库",t.Find("Description").GetComponent<Text>().text);
            Assert.AreEqual("120 / 240 金币",t.Find("Fee").GetComponent<Text>().text);
            Assert.AreEqual("可进入",t.Find("Access").GetComponent<Text>().text);
            Assert.Greater(t.Find("Access").GetSiblingIndex(),t.Find("AccessBadge").GetSiblingIndex(),"Badge text must draw after its opaque background.");
            Assert.IsNotEmpty(t.Find("Species").GetComponent<Text>().text);
            foreach(var name in new[]{"Name","Description","Fee","Species","Access","CategoryDot"})
            {var label=t.Find(name).GetComponent<Text>();Assert.IsTrue(label.gameObject.activeInHierarchy);Assert.IsTrue(label.enabled);Assert.Greater(label.color.a,0);}
            pond.isOpen=false;t.GetComponent<CozyPondCardContent>().Bind(pond,definition,state);
            Assert.AreEqual("暂未开放",t.Find("Access").GetComponent<Text>().text);
            var description=(RectTransform)t.Find("Description");var fee=(RectTransform)t.Find("Fee");
            Assert.LessOrEqual(description.anchoredPosition.x+description.rect.width,fee.anchoredPosition.x);
        }
        [Test]public void MapNoteAndBothRailButtonsDoNotOverlap()
        {
            var t=Load("PanelWorldMap");var note=(RectTransform)t.Find("DesignMapRail/Note");var list=(RectTransform)t.Find("ListPonds");var reset=(RectTransform)t.Find("Details/ResetView");
            Assert.LessOrEqual(Bottom(t,note),Top(t,list));Assert.LessOrEqual(Bottom(t,list),Top(t,reset));Assert.LessOrEqual(Bottom(t,reset),608);
            Assert.IsFalse(t.Find("DesignMapRail/Body").gameObject.activeSelf);
        }
        [Test]public void ListNoteFitsBetweenClearFiltersAndMapButton()
        {
            var t=Load("PanelPondSelection");var note=t.Find("DesignPondRail/Note").GetComponent<Text>();
            var clear=(RectTransform)t.Find("ClearFilters");var locate=(RectTransform)t.Find("DesignPondAside/Locate");
            Assert.LessOrEqual(Bottom(t,clear),Top(t,note.rectTransform));Assert.LessOrEqual(Bottom(t,note.rectTransform),Top(t,locate));
            Assert.AreEqual("选好一片水域，\n再决定回鱼方式。",note.text);
            Assert.LessOrEqual(note.preferredHeight,note.rectTransform.rect.height+.1f);
        }
        [TestCase("PanelWorldMap","Details/EnterPond")]
        [TestCase("PanelPondSelection","DesignPondAside/Enter")]
        public void EntryIconAndChangingLabelAreCenteredAsOneMeasuredRow(string prefab,string path)
        {
            var t=Load(prefab);var button=(RectTransform)t.Find(path);var inline=button.GetComponent<CozyJournalInlineContent>();Assert.IsNotNull(inline);
            var label=button.Find("Label").GetComponent<Text>();var icon=button.Find("Icon")?.GetComponent<Image>()??button.Find("CozyIcon").GetComponent<Image>();
            var original=button.anchoredPosition;
            foreach(var caption in new[]{"进入鱼塘","正在进入…","暂不可进入"})
            {
                label.text=caption;inline.Reflow();var left=icon.rectTransform.anchoredPosition.x;var right=label.rectTransform.anchoredPosition.x+label.rectTransform.rect.width;
                Assert.AreEqual(button.rect.width*.5f,(left+right)*.5f,.05f);Assert.AreEqual(22,label.fontSize);Assert.AreEqual(original,button.anchoredPosition);
                Assert.AreEqual(10,label.rectTransform.anchoredPosition.x-(left+icon.rectTransform.rect.width),.05f);
            }
        }
        [Test]public void SavedProductionPrefabsRetainCardContentAndEntryInlineBindings()
        {
            var card=AssetDatabase.LoadAssetAtPath<GameObject>(DesktopPrefabCatalog.AssetPath("PondSelectionCard"));
            Assert.IsNotNull(card.GetComponent<CozyPondCardContent>(),"ApplyPondRefinement must run before prefab save.");
            Assert.IsNotNull(card.transform.Find("Species"));Assert.IsNotNull(card.transform.Find("CategoryDot"));
            foreach(var name in new[]{"PanelPondSelection","PanelWorldMap"})
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(DesktopPrefabCatalog.AssetPath(name));
                var path=name=="PanelPondSelection"?"DesignPondAside/Enter":"Details/EnterPond";
                Assert.IsNotNull(prefab.transform.Find(path).GetComponent<CozyJournalInlineContent>(),name);
                if(name=="PanelPondSelection")Assert.AreEqual(248,prefab.transform.Find("PondCardScroll/Viewport/Cards").GetComponent<GridLayoutGroup>().cellSize.y);
            }
        }
        [Test]public void AccessLabelDistinguishesClosedLoadingAndOnboarding()
        {
            var p=new WorldMapPondView{isOpen=true,minPlayerLevel=5};var state=new PondSelectionState();
            Assert.AreEqual("条件待核对",CozyPondCardContent.AccessLabel(p,state));
            state.ApplyProgress(new FishingProgressDto{level=99,onboardingCompleted=false});Assert.AreEqual("先完成引导",CozyPondCardContent.AccessLabel(p,state));
            p.isOpen=false;Assert.AreEqual("暂未开放",CozyPondCardContent.AccessLabel(p,state));
        }
        static float Top(Transform root,RectTransform node){var corners=new Vector3[4];node.GetWorldCorners(corners);return -root.InverseTransformPoint(corners[1]).y;}
        static float Bottom(Transform root,RectTransform node){var corners=new Vector3[4];node.GetWorldCorners(corners);return -root.InverseTransformPoint(corners[0]).y;}
    }
}
#endif
