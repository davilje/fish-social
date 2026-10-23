#if UNITY_INCLUDE_TESTS
using FishSocial.Desktop;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Tests.Editor
{
    public sealed class CozyWorkspaceBodyMotionTests
    {
        GameObject page;
        bool reduced;
        [SetUp] public void Setup(){reduced=CozyMotion.Reduced;CozyMotion.SetPaused(false);CozyMotion.SetReduced(false);}
        [TearDown] public void Cleanup(){CozyMotion.CancelPageTransition();if(page!=null)Object.DestroyImmediate(page);CozyMotion.SetReduced(reduced);}
        Transform Load(string name,string path="")
        {
            page=Object.Instantiate(Resources.Load<GameObject>("Desktop/Prefabs/"+name));page.name=name;page.SetActive(true);
            return path==""?page.transform:page.transform.Find(path);
        }
        static CozyWorkspaceBodyGraphic Effect(Transform node)=>node.GetComponentInChildren<CozyWorkspaceBodyGraphic>(true);

        [Test] public void SavedProfileSectionsStaggerAndKeepHitRectanglesFixed()
        {
            var tab=Load("PanelProfileHub","Shell/Content/Tab_资料");
            var body=tab.GetComponent<CozyWorkspaceBodyMotion>();Assert.NotNull(body);
            var nodes=new[]{tab.Find("Avatar"),tab.Find("DesignStatCards"),tab.Find("JournalProgress")};
            var positions=new Vector3[nodes.Length];for(var i=0;i<nodes.Length;i++)positions[i]=nodes[i].localPosition;
            body.PlayEntrance();foreach(var node in nodes)Assert.AreEqual(0,Effect(node).Opacity);
            body.Sample(.02f);Assert.Greater(Effect(nodes[0]).Opacity,0);Assert.AreEqual(0,Effect(nodes[1]).Opacity);
            body.Sample(.1f);
            Assert.Greater(Effect(nodes[0]).Opacity,Effect(nodes[1]).Opacity);
            Assert.Greater(Effect(nodes[1]).Opacity,Effect(nodes[2]).Opacity);
            Assert.Less(Effect(nodes[0]).Offset.y,0);
            body.Sample(.32f);foreach(var node in nodes)Assert.AreEqual(1,Effect(node).Opacity,.0001f);
            for(var i=0;i<nodes.Length;i++)Assert.AreEqual(positions[i],nodes[i].localPosition);
        }
        [Test] public void EditorCardBackgroundFormAndButtonsShareOneClock()
        {
            var root=Load("PanelProfileEdit");var body=root.GetComponent<CozyWorkspaceBodyMotion>();Assert.NotNull(body);
            body.Sample(.12f);
            var alpha=Effect(root.Find("JournalPreview")).Opacity;
            // Actual Chrome sample for 120ms / 240ms OutCubic.
            Assert.AreEqual(.872202f,alpha,.0001f);
            foreach(var path in new[]{"Form","Actions","Header/Back"})Assert.AreEqual(alpha,Effect(root.Find(path)).Opacity,.0001f,path);
            Assert.Less(Effect(root.Find("WorkspaceEditRight")).Opacity,alpha);
            Assert.AreEqual(Effect(root.Find("WorkspaceEditRight")).Opacity,Effect(root.Find("WorkspaceAvatarTab")).Opacity);
        }
        [Test] public void GrowthBarKeepsItsTargetAndRoundCapsDuringDelayedEntrance()
        {
            var tab=Load("PanelProfileHub","Shell/Content/Tab_资料");var body=tab.GetComponent<CozyWorkspaceBodyMotion>();
            var fill=tab.Find("JournalProgress/Track/Fill").GetComponent<CozyJournalProgressFill>();
            fill.SetValue(.5f);var rect=(RectTransform)fill.transform;var track=(RectTransform)rect.parent;
            body.Sample(.2f);Assert.AreEqual(0,rect.rect.width,.001f);
            body.Sample(.45f);Assert.AreEqual(track.rect.width*.5f*.872202f,rect.rect.width,.01f);
            Assert.AreEqual(.5f,fill.GetComponent<Image>().fillAmount);
            Assert.AreEqual(Image.Type.Sliced,fill.GetComponent<Image>().type);
            body.Finish();Assert.AreEqual(track.rect.width*.5f,rect.rect.width,.001f);
        }
        [Test] public void ReducedMotionRemovesOffsetsAndStaggerButRetainsShortFade()
        {
            var tab=Load("PanelProfileHub","Shell/Content/Tab_资料");var body=tab.GetComponent<CozyWorkspaceBodyMotion>();
            CozyMotion.SetReduced(true);body.PlayEntrance();body.Sample(.06f);
            foreach(var path in new[]{"Avatar","DesignStatCards","JournalProgress"})
            {Assert.AreEqual(.872202f,Effect(tab.Find(path)).Opacity,.0001f);Assert.AreEqual(Vector3.zero,Effect(tab.Find(path)).Offset);}
            body.Sample(.12f);Assert.AreEqual(1,Effect(tab.Find("Avatar")).Opacity);
        }
        [Test] public void LateGraphicsJoinTheCurrentSectionClock()
        {
            var root=Load("PanelProfileEdit");var body=root.GetComponent<CozyWorkspaceBodyMotion>();body.PlayEntrance();
            var late=new GameObject("LateAvatar",typeof(RectTransform),typeof(Image));late.transform.SetParent(root.Find("Avatars"),false);
            body.Sample(.12f);Assert.IsNotNull(late.GetComponent<CozyWorkspaceBodyGraphic>());
            Assert.AreEqual(Effect(root.Find("WorkspaceEditRight")).Opacity,late.GetComponent<CozyWorkspaceBodyGraphic>().Opacity);
        }
    }
}
#endif
