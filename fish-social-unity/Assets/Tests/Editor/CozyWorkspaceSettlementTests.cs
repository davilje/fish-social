#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using FishSocial.Desktop;
using FishSocial.Desktop.Auth;
using FishSocial.Desktop.Editor;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
namespace FishSocial.Tests.Editor
{
    public sealed class CozyWorkspaceSettlementTests
    {
        [Test]
        public void ServerStatisticsFilteringAndSafeShareTargets()
        {
            var root=new GameObject("PanelPondSettlement",typeof(RectTransform));
            try
            {
                CozyWorkspaceSettlementAuthoring.Apply(root);
                var view=root.GetComponent<DesktopPondSettlementModalView>();
                var kept=new PondSessionCatchEntryDto{speciesId="koi",quality="blue",outcome="kept",inventoryItemId="kept-id",caughtAt=1000,sizeM=.6f,weightKg=2.59f};
                var returned=new PondSessionCatchEntryDto{speciesId="koi",quality="purple",outcome="returned",inventoryItemId="stale-id",caughtAt=2000,sizeM=1.2f,weightKg=20.74f};
                view.PresentSummary(new PondSessionSummaryDto{pondName="试验水域",biteCount=9,escapeCount=7,fishingDurationMs=65000,catches=new[]{kept,returned}});
                Assert.AreEqual("9 次",TextAt(root,"Panel/Stats/Bites/Value"));
                Assert.AreEqual("7 次",TextAt(root,"Panel/Stats/Escapes/Value"));
                Assert.AreEqual("1 分 5 秒",TextAt(root,"Panel/Stats/Duration/Value"));
                var list=root.transform.Find("Panel/ListScroll/Viewport/List");
                Assert.IsFalse(list.GetChild(0).Find("Share").GetComponent<Button>().interactable);
                string id=null;view.ShareRequested+=value=>id=value;
                list.GetChild(1).Find("Share").GetComponent<Button>().onClick.Invoke();Assert.AreEqual("kept-id",id);
                root.transform.Find("Panel/Filter").GetComponent<Button>().onClick.Invoke();
                Assert.IsTrue(view.ChoicesOpen);
                Assert.IsTrue(root.transform.Find("Panel/ChoiceLayer/FilterMenu").gameObject.activeSelf);
                root.transform.Find("Panel/ChoiceLayer/FilterMenu/Option1").GetComponent<Button>().onClick.Invoke();
                Assert.IsFalse(view.ChoicesOpen);Assert.AreEqual(1,view.VisibleRecordCount);
                Assert.AreEqual("2.59 kg",list.GetChild(0).Find("Weight").GetComponent<Text>().text);
                view.SetFilter(0);
                root.transform.Find("Panel/Sort").GetComponent<Button>().onClick.Invoke();
                Assert.IsTrue(view.ChoicesOpen);
                root.transform.Find("Panel/ChoiceLayer/SortMenu/Option1").GetComponent<Button>().onClick.Invoke();
                Assert.IsFalse(view.ChoicesOpen);
                Assert.AreEqual("1.20 m",list.GetChild(0).Find("Size").GetComponent<Text>().text);
                view.PresentSummary(new PondSessionSummaryDto{catches=new PondSessionCatchEntryDto[0]});
                Assert.IsTrue(root.transform.Find("Panel/Empty").gameObject.activeSelf);
                Assert.AreEqual(0,view.VisibleRecordCount);
            }
            finally{Object.DestroyImmediate(root);}
        }
        [Test]
        public void RecordListStaysAboveFooterAndUnsafeSharesAreDisabled()
        {
            var root=new GameObject("PanelPondSettlement",typeof(RectTransform));
            try
            {
                CozyWorkspaceSettlementAuthoring.Apply(root);
                var panel=root.transform.Find("Panel") as RectTransform;
                var scroll=panel.Find("ListScroll") as RectTransform;var home=panel.Find("Home") as RectTransform;
                Assert.AreEqual(new Vector2(1120,720),panel.sizeDelta);
                Assert.AreEqual(Vector2.zero,((RectTransform)root.transform).anchorMin);
                Assert.AreEqual(Vector2.one,((RectTransform)root.transform).anchorMax);
                var paper=panel.Find("JournalPaper") as RectTransform;
                Assert.Less(paper.offsetMin.x,0);Assert.Greater(paper.offsetMax.x,0);
                CatTheme.SkinTree(root.transform);
                Assert.AreEqual(28,panel.Find("Title").GetComponent<Text>().fontSize);
                Assert.AreEqual(14,panel.Find("Filter/Label").GetComponent<Text>().fontSize);
                Assert.AreEqual(0,panel.GetComponent<Image>().color.a);
                Assert.AreEqual(.4f,root.transform.Find("Dim").GetComponent<Image>().color.a,.001f);
                Assert.Less(-scroll.anchoredPosition.y+scroll.sizeDelta.y,-home.anchoredPosition.y);
                Assert.IsNotNull(scroll.GetComponent<Image>());
                Assert.IsFalse(DesktopPondSettlementModalView.CanShare(new PondSessionCatchEntryDto{outcome="kept"}));
                Assert.IsFalse(DesktopPondSettlementModalView.CanShare(new PondSessionCatchEntryDto{outcome="returned",inventoryItemId="removed"}));
                var view=root.GetComponent<DesktopPondSettlementModalView>();
                view.PlayOpenMotion();
                typeof(DesktopPondSettlementModalView).GetMethod("OnDisable",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(view,null);
                Assert.AreEqual(Vector2.zero,panel.anchoredPosition);
                Assert.AreEqual(1,panel.GetComponent<CanvasGroup>().alpha);
                Assert.IsFalse(view.ChoicesOpen);
            }
            finally{Object.DestroyImmediate(root);}
        }
        static string TextAt(GameObject root,string path){return root.transform.Find(path).GetComponent<Text>().text;}
    }
}
#endif
