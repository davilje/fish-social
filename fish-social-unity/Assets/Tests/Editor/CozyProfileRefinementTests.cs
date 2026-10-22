#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Reflection;
using FishSocial.Desktop;
using FishSocial.Desktop.Auth;
using FishSocial.Desktop.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
namespace FishSocial.Tests.Editor
{
    public sealed class CozyProfileRefinementTests
    {
        const string Root="Assets/Resources/Desktop/Prefabs/";
        GameObject Open(string name)
        {var instance=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Root+name+".prefab"));instance.name=name;CozyRedesignAuthoring.ApplyProfileRefinement(instance);return instance;}
        [Test]
        public void ProgressKeepsRoundCapsAndScalesOnlyItsWidth()
        {
            var page=Open("PanelProfileHub");try
            {
                var progress=page.GetComponentInChildren<CozyJournalProgressFill>(true);Assert.NotNull(progress);
                foreach(var ratio in new[]{0f,.64f,1f})
                {
                    progress.SetValue(ratio);var image=progress.GetComponent<Image>();
                    Assert.AreEqual(Image.Type.Sliced,image.type);
                    Assert.That(image.rectTransform.rect.width,Is.EqualTo(((RectTransform)image.transform.parent).rect.width*ratio).Within(.01));
                    Assert.That(image.rectTransform.rect.height,Is.EqualTo(((RectTransform)image.transform.parent).rect.height).Within(.01));
                    Assert.AreEqual(ratio>0,image.enabled);
                }
            }finally{Object.DestroyImmediate(page);}
        }
        [Test]
        public void CodexStampHasSourceCenterAndNewSnapshotReplacesCards()
        {
            var page=Open("PanelProfileHub");try
            {
                var controller=page.GetComponent<DesktopProfileHubPanel>();
                var hub=new ProfileHubDto{isSelf=true,canEdit=true,profile=new ProfileHubProfileDto{nickname="测试"},showcaseFish=new FishInventoryItemDto[8]};
                var entries=new[]{new FishCodexEntryDto{speciesId="koi",totalCaught=2,maxSizeM=.62f}};
                controller.PresentSnapshot(hub,entries,DesktopProfileHubPanel.HubTab.Codex);
                var content=page.transform.Find("Shell/Content/Tab_图鉴/CodexGrid/Viewport/Content");var count=content.childCount;
                controller.PresentSnapshot(hub,entries,DesktopProfileHubPanel.HubTab.Codex);Assert.AreEqual(count,content.childCount);
                var card=content.Find("koi");Assert.NotNull(card);
                Assert.AreEqual(24,content.GetComponent<GridLayoutGroup>().padding.top);
                Assert.Less(card.Find("CozyVisual").GetSiblingIndex(),card.Find("JournalArtWell").GetSiblingIndex());
                Assert.Less(card.Find("JournalArtWell").GetSiblingIndex(),card.Find("DesignItemArt").GetSiblingIndex());
                Assert.AreEqual(new Vector2(.5f,.5f),((RectTransform)card.Find("JournalTape")).pivot);
                Assert.AreEqual("锦鲤",card.Find("Label").GetComponent<Text>().text);
                Assert.AreEqual("累计捕获 2 次",card.Find("JournalMetadata").GetComponent<Text>().text);
                var stamp=(RectTransform)card.Find("JournalStamp");Assert.IsTrue(stamp.gameObject.activeSelf);
                Assert.AreEqual(new Vector2(.5f,.5f),stamp.pivot);
                Assert.AreEqual(new Vector2(140,-15),stamp.anchoredPosition);
                var color=stamp.GetComponent<Image>().color;Assert.Greater(color.r,color.g*2);
                Assert.AreEqual("已收录",stamp.Find("Label").GetComponent<Text>().text);
            }finally{Object.DestroyImmediate(page);}
        }
        [Test]
        public void ShowcaseKeepsEightRealSlotsAndSeparatesMetadata()
        {
            var page=Open("PanelProfileHub");try
            {
                var fish=new FishInventoryItemDto[8];fish[0]=new FishInventoryItemDto{speciesId="koi",id="f1",sizeM=.62f,quality="red"};
                var hub=new ProfileHubDto{isSelf=true,canEdit=true,profile=new ProfileHubProfileDto{nickname="测试"},showcaseFish=fish};
                page.GetComponent<DesktopProfileHubPanel>().PresentSnapshot(hub,null,DesktopProfileHubPanel.HubTab.Showcase);
                var content=page.transform.Find("Shell/Content/Tab_展示柜/ShowcaseGrid/Viewport/Content");Assert.AreEqual(8,content.childCount);
                Assert.AreEqual("锦鲤",content.GetChild(0).Find("Label").GetComponent<Text>().text);
                StringAssert.StartsWith("0.62 m",content.GetChild(0).Find("JournalMetadata").GetComponent<Text>().text);
                Assert.AreEqual("展示 8",content.GetChild(7).Find("JournalRibbon/Label").GetComponent<Text>().text);
                Assert.IsTrue(content.GetChild(7).Find("JournalEmpty").gameObject.activeSelf);
            }finally{Object.DestroyImmediate(page);}
        }
        [Test]
        public void AchievementReadsItsRealIconKey()
        {
            var host=new GameObject("Rows",typeof(RectTransform));try
            {
                typeof(DesktopProfileHubPanel).GetMethod("CreateAchievementRow",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{host.transform,"ach-first-catch","初出茅庐\n首次将鱼收入背包",Color.white,true,"ach_first_catch"});
                var row=host.transform.GetChild(0);Assert.AreEqual("初出茅庐",row.Find("Label").GetComponent<Text>().text);
                Assert.AreEqual("首次将鱼收入背包",row.Find("JournalDescription").GetComponent<Text>().text);
                Assert.AreSame(CatTheme.LoadCozy("icons/hook.png",Vector4.zero),row.Find("Reflow_AchievementSeal").GetComponent<Image>().sprite);
            }finally{Object.DestroyImmediate(host);}
        }
    }
}
#endif
