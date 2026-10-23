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
    public sealed class CozyWorkspaceProfileTests
    {
        GameObject Open(string name)
        {
            var page=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Desktop/Prefabs/"+name+".prefab"));
            page.name=name;CozyRedesignAuthoring.ApplyWorkspaceProfile(page);return page;
        }
        static PlayerProfileDto Profile()=>new PlayerProfileDto{nickname="钓友",avatarUrl=DesktopDefaultAvatars.All[0].Path,showcaseFishIds=new[]{"one"}};
        static FishInventoryItemDto[] Fish()=>new[]{new FishInventoryItemDto{id="one",speciesId="koi",sizeM=.62f},new FishInventoryItemDto{id="two",speciesId="carp",sizeM=.45f}};

        [Test]
        public void EditorKeepsLargeWholeCardTargetsAndAllAvailableAvatars()
        {
            var page=Open("PanelProfileEdit");try
            {
                var view=page.GetComponent<DesktopProfileEditPanel>();view.PresentSnapshot(Profile(),Fish());
                var avatars=page.transform.Find("Avatars/Viewport/Content");
                Assert.AreEqual(DesktopDefaultAvatars.All.Length,avatars.childCount);
                Assert.AreEqual(new Vector2(222,104),avatars.GetComponent<GridLayoutGroup>().cellSize);
                var avatarButton=avatars.GetChild(0).GetComponent<Button>();
                var avatarHit=avatars.GetChild(0).GetComponent<Image>();
                Assert.IsNotNull(avatarButton);
                Assert.IsTrue(avatarHit.raycastTarget);
                Assert.AreEqual(avatarHit,avatarButton.targetGraphic);
                page.transform.Find("WorkspaceShowcaseTab").GetComponent<Button>().onClick.Invoke();
                Assert.IsFalse(page.transform.Find("Avatars").gameObject.activeSelf);
                Assert.IsTrue(page.transform.Find("Showcase").gameObject.activeSelf);
                var slots=page.transform.Find("Showcase/Viewport/Content");Assert.AreEqual(8,slots.childCount);
                Assert.IsTrue(slots.GetChild(0).GetComponent<Image>().raycastTarget);
                Assert.AreEqual(slots.GetChild(0).GetComponent<Image>(),slots.GetChild(0).GetComponent<Button>().targetGraphic);
                Assert.AreEqual("锦鲤",slots.GetChild(0).Find("Label").GetComponent<Text>().text);
                Assert.IsTrue(slots.GetChild(7).Find("WorkspaceEmpty").gameObject.activeSelf);
            }finally{Object.DestroyImmediate(page);}
        }

        [Test]
        public void PickerSearchAndCancelDoNotCommitUntilConfirmed()
        {
            var page=Open("PanelProfileEdit");try
            {
                var view=page.GetComponent<DesktopProfileEditPanel>();view.PresentSnapshot(Profile(),Fish(),true,0);
                var picker=page.transform.Find("Picker");var search=picker.Find("Search").GetComponent<InputField>();
                search.text="鲤鱼";
                var rows=picker.Find("Viewport/Content");Assert.AreEqual(1,rows.childCount);
                Assert.IsTrue(rows.GetChild(0).GetComponent<Image>().raycastTarget);
                rows.GetChild(0).GetComponent<Button>().onClick.Invoke();
                picker.Find("Close").GetComponent<Button>().onClick.Invoke();
                var slots=page.transform.Find("Showcase/Viewport/Content");
                Assert.AreEqual("锦鲤",slots.GetChild(0).Find("Label").GetComponent<Text>().text);
                slots.GetChild(0).GetComponent<Button>().onClick.Invoke();
                rows.Find("Fish_two").GetComponent<Button>().onClick.Invoke();
                picker.Find("Confirm").GetComponent<Button>().onClick.Invoke();
                Assert.AreEqual("鲤鱼",slots.GetChild(0).Find("Label").GetComponent<Text>().text);
                Assert.IsFalse(picker.gameObject.activeSelf);
                slots.GetChild(0).GetComponent<Button>().onClick.Invoke();picker.Find("Clear").GetComponent<Button>().onClick.Invoke();
                Assert.IsTrue(slots.GetChild(0).Find("WorkspaceEmpty").gameObject.activeSelf);
            }finally{Object.DestroyImmediate(page);}
        }

        [Test]
        public void EditorActionsStayInsideLeftCardBelowTheBiography()
        {
            var page=Open("PanelProfileEdit");try
            {
                var card=(RectTransform)page.transform.Find("JournalPreview");
                foreach(var path in new[]{"Header/Back","Actions/Save"})
                {
                    var rect=(RectTransform)page.transform.Find(path);var corners=new Vector3[4];rect.GetWorldCorners(corners);
                    foreach(var point in corners)Assert.IsTrue(card.rect.Contains(card.InverseTransformPoint(point)),path);
                }
                var cancel=(RectTransform)page.transform.Find("Header/Back");var save=(RectTransform)page.transform.Find("Actions/Save");
                Assert.That(save.position.x-cancel.position.x,Is.GreaterThan(cancel.rect.width));
                var bio=(RectTransform)page.transform.Find("Form/Bio");
                Assert.That(bio.position.y-bio.rect.height,Is.GreaterThan(cancel.position.y));
            }finally{Object.DestroyImmediate(page);}
        }

        [Test]
        public void EditorRailRoutesEveryTabAndCannotLeaveWhileSaving()
        {
            var page=Open("PanelProfileEdit");try
            {
                var view=page.GetComponent<DesktopProfileEditPanel>();
                var calls=0;var destination=DesktopProfileHubPanel.HubTab.Profile;
                view.Bind(null,null,()=>calls++,tab=>{calls++;destination=tab;});
                var profile=Profile();view.PresentSnapshot(profile,Fish());
                page.transform.Find("Form/Nickname").GetComponent<InputField>().text="未保存的名字";
                var labels=new[]{"资料","展示柜","图鉴","相册","成就"};
                for(var i=0;i<labels.Length;i++)
                {
                    page.transform.Find("JournalEditRail/Tab_"+labels[i]).GetComponent<Button>().onClick.Invoke();
                    Assert.AreEqual((DesktopProfileHubPanel.HubTab)i,destination);
                }
                Assert.AreEqual(5,calls);Assert.AreEqual("钓友",profile.nickname);
                Assert.IsFalse(page.transform.Find("JournalEditRail/Heading").gameObject.activeSelf);
                Assert.IsFalse(page.transform.Find("JournalEditRail/Note").gameObject.activeSelf);
                const System.Reflection.BindingFlags flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
                typeof(DesktopProfileEditPanel).GetField("_busy",flags).SetValue(view,true);
                typeof(DesktopProfileEditPanel).GetMethod("SetStatus",flags).Invoke(view,new object[]{"正在保存资料…"});
                foreach(var label in labels)
                {
                    var button=page.transform.Find("JournalEditRail/Tab_"+label).GetComponent<Button>();
                    Assert.IsFalse(button.interactable);button.onClick.Invoke();
                }
                var cancel=page.transform.Find("Header/Back").GetComponent<Button>();Assert.IsFalse(cancel.interactable);cancel.onClick.Invoke();
                Assert.AreEqual(5,calls);
            }finally{Object.DestroyImmediate(page);}
        }

        [Test]
        public void ProfileUsesActualCodexAndAchievementCountsWithoutDuplicateActions()
        {
            var page=Open("PanelProfileHub");try
            {
                var hub=new ProfileHubDto{isSelf=true,canEdit=true,profile=new ProfileHubProfileDto{nickname="钓友"},progress=new ProfileHubProgressDto{level=7,xp=320},codexSummary=new ProfileHubCodexSummaryDto{unlockedCount=1,totalSpecies=50},showcaseFish=Fish(),achievements=new[]{new AchievementViewDto{unlocked=true},new AchievementViewDto{unlocked=false}}};
                page.GetComponent<DesktopProfileHubPanel>().PresentSnapshot(hub,new[]{new FishCodexEntryDto{speciesId="koi",totalCaught=17,maxSizeM=.62f}},DesktopProfileHubPanel.HubTab.Profile);
                var tab=page.transform.Find("Shell/Content/Tab_资料");
                Assert.AreEqual("17 条",tab.Find("DesignStatCards/Card0/Value").GetComponent<Text>().text);
                Assert.AreEqual("1 项",tab.Find("DesignStatCards/Card2/Value").GetComponent<Text>().text);
                StringAssert.Contains("330",tab.Find("JournalProgress/Caption").GetComponent<Text>().text);
                foreach(var path in new[]{"JournalShowcaseLink","JournalAlbumLink","DesignShowcase/Open"})Assert.IsFalse(tab.Find(path).gameObject.activeSelf,path);
            }finally{Object.DestroyImmediate(page);}
        }
    }
}
#endif
