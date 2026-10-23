#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Reflection;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Remoting.Proxies;
using Object=UnityEngine.Object;
using FishSocial.Desktop;
using FishSocial.Desktop.Auth;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Tests.Editor
{
    public sealed class CozyWorkspaceCompletionMotionTests
    {
        GameObject host;
        bool reduced;
        PlayerProfileDto cached;
        [SetUp] public void Setup()
        {
            cached=DesktopProfileCache.Latest; reduced=CozyMotion.Reduced; CozyMotion.SetPaused(false); CozyMotion.SetReduced(false);
            host=new GameObject("Canvas",typeof(RectTransform));
        }
        [TearDown] public void Cleanup()
        {
            CozyMotion.CancelPageTransition();
            if(host!=null)Object.DestroyImmediate(host);
            DesktopProfileCache.Latest=cached;
            CozyMotion.SetReduced(reduced); CozyMotion.SetPaused(false);
        }
        Button EditorButton()
        {
            var page=Object.Instantiate(Resources.Load<GameObject>("Desktop/Prefabs/PanelProfileEdit"),host.transform);
            page.SetActive(true);
            return page.transform.Find("Actions/Save").GetComponent<Button>();
        }
        [TestCase(false)] [TestCase(true)]
        public void SaveSuccessIsVisibleBeforeReturningAndCannotReplay(bool reducedMode)
        {
            CozyMotion.SetReduced(reducedMode);
            var button=EditorButton();var label=button.GetComponentInChildren<Text>();var count=0;
            CozyProfileSaveFeedback.Play(button,()=>count++);
            CozyProfileSaveFeedback.Play(button,()=>count++);
            Assert.AreEqual("✓ 已保存",label.text);Assert.AreEqual(0,count);
            CozyMotion.Tick(reducedMode?.06f:.12f);
            Assert.Greater(label.GetComponent<CanvasGroup>().alpha,.35f);Assert.AreEqual(0,count);
            CozyMotion.Tick(reducedMode?.06f:.12f);Assert.AreEqual(1,count);
        }
        [Test] public void LeavingEditorCancelsPendingSuccessReturn()
        {
            var button=EditorButton();var label=button.GetComponentInChildren<Text>();var original=label.text;var count=0;
            CozyProfileSaveFeedback.Play(button,()=>count++);
            CozyMotion.Tick(.1f);
            button.GetComponentInParent<DesktopProfileEditPanel>().PresentSnapshot(new PlayerProfileDto{nickname="小橘"},new FishInventoryItemDto[0]);
            button.GetComponentInParent<DesktopProfileEditPanel>().OnClosed();
            CozyMotion.Tick(1);
            Assert.AreEqual(0,count);Assert.AreEqual(original,label.text);
        }
        sealed class SaveApi : RealProxy
        {
            public bool ProfileOk, ShowcaseOk;
            public SaveApi() : base(typeof(IAuthenticatedApiClient)) {}
            public IAuthenticatedApiClient Client => (IAuthenticatedApiClient)GetTransparentProxy();
            public override IMessage Invoke(IMessage message)
            {
                var call=(IMethodCallMessage)message; object result=null;
                if(call.MethodName=="get_CanUse") result=true;
                else if(call.MethodName=="UpdatePlayerProfile" || call.MethodName=="SetShowcase")
                {
                    var ok=call.MethodName=="UpdatePlayerProfile"?ProfileOk:ShowcaseOk;
                    var callback=(Action<bool,PlayerProfileDto,string>)call.Args[call.ArgCount-1];
                    result=Done(()=>callback(ok,ok?new PlayerProfileDto{nickname="湖畔小橘"}:null,ok?null:"网络失败"));
                }
                return new ReturnMessage(result,null,0,call.LogicalCallContext,call);
            }
            static IEnumerator Done(Action action){action();yield break;}
        }
        static void Drain(IEnumerator routine){while(routine.MoveNext())if(routine.Current is IEnumerator nested)Drain(nested);}
        [TestCase(true,true)] [TestCase(false,true)] [TestCase(true,false)]
        public void ActualSaveOnlyAnimatesAfterBothApiWritesSucceed(bool profileOk,bool showcaseOk)
        {
            var button=EditorButton();var view=button.GetComponentInParent<DesktopProfileEditPanel>();var returned=0;
            var api=new SaveApi{ProfileOk=profileOk,ShowcaseOk=showcaseOk};
            view.Bind(api.Client,null,()=>returned++);
            view.PresentSnapshot(new PlayerProfileDto{nickname="小橘"},new FishInventoryItemDto[0]);
            Drain((IEnumerator)typeof(DesktopProfileEditPanel).GetMethod("SaveRoutine",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(view,new object[]{"湖畔小橘"}));
            Assert.AreEqual(0,returned);
            var success=profileOk&&showcaseOk;
            Assert.AreEqual(success,button.GetComponentInChildren<Text>().text=="✓ 已保存");
            if(success)Assert.IsFalse(button.interactable);
            CozyMotion.Tick(.24f);Assert.AreEqual(success?1:0,returned);
            if(!success)Assert.IsTrue(view.transform.Find("Actions/Retry").gameObject.activeSelf);
        }
        DesktopPondSettlementModalView Settlement() => DesktopPondSettlementModalView.Show(host.transform,
            new PondSessionSummaryDto{pondName="千岛湖",biteCount=3,escapeCount=1,catches=new[]{new PondSessionCatchEntryDto{speciesId="koi",outcome="kept",inventoryItemId="fish-1"}}});
        static float Opacity(Transform node)=>node.GetComponentInChildren<CozyWorkspaceBodyGraphic>(true).Opacity;
        [Test] public void ActualSummaryEntryRevealsPaperThenStatsRecordsAndAccount()
        {
            var view=Settlement();var paper=view.transform.Find("Panel");
            Assert.AreEqual(0,paper.GetComponent<CanvasGroup>().alpha);
            CozyMotion.Tick(.2f);
            Assert.AreEqual(1,paper.GetComponent<CanvasGroup>().alpha);
            Assert.AreEqual(0,Opacity(paper.Find("Stats/Bites")),.0001f);
            CozyMotion.Tick(.1f);
            Assert.Greater(Opacity(paper.Find("Stats/Bites")),Opacity(paper.Find("Stats/Escapes")));
            Assert.AreEqual(0,Opacity(paper.Find("Account")));
            CozyMotion.Tick(.38f);
            Assert.AreEqual(1,Opacity(paper.Find("ListScroll")),.0001f);
            Assert.AreEqual(1,Opacity(paper.Find("Account")),.0001f);
            Assert.AreEqual(1,Opacity(paper.Find("Home")),.0001f);
        }
        [Test] public void SummaryReceivedWhileMainWindowHiddenWaitsForRestore()
        {
            CozyMotion.SetPaused(true);
            var view=Settlement();var paper=view.transform.Find("Panel");
            Assert.AreEqual(0,paper.GetComponent<CanvasGroup>().alpha);
            CozyMotion.SetPaused(false);
            Assert.AreEqual(0,paper.GetComponent<CanvasGroup>().alpha);
            CozyMotion.Tick(.1f);
            Assert.Greater(paper.GetComponent<CanvasGroup>().alpha,0);
            Assert.Less(paper.GetComponent<CanvasGroup>().alpha,1);
            Assert.AreEqual(0,Opacity(paper.Find("Stats/Bites")));
            CozyMotion.Tick(.58f);Assert.AreEqual(1,Opacity(paper.Find("Home")));
        }
        [Test] public void EarlyCloseKeepsCurrentOpacityAndBackdropUntilExitFinishes()
        {
            var view=Settlement();var paper=view.transform.Find("Panel");
            CozyMotion.Tick(.1f);var alpha=paper.GetComponent<CanvasGroup>().alpha;var position=((RectTransform)paper).anchoredPosition;
            view.Close();Assert.AreEqual(alpha,paper.GetComponent<CanvasGroup>().alpha);Assert.AreEqual(position,((RectTransform)paper).anchoredPosition);
            CozyMotion.Tick(.07f);Assert.IsTrue(view!=null);Assert.Less(paper.GetComponent<CanvasGroup>().alpha,alpha);
            Assert.Greater(view.transform.Find("Dim").GetComponent<Image>().color.a,0);
            CozyMotion.Tick(.07f);Assert.IsTrue(view==null);
        }
        [TestCase(false)] [TestCase(true)]
        public void HomeNavigationWaitsForClosingAndFiresOnce(bool reducedMode)
        {
            CozyMotion.SetReduced(reducedMode);
            var view=Settlement();CozyMotion.Tick(1);var count=0;view.ProfileRequested+=()=>count++;
            var button=view.transform.Find("Panel/Home").GetComponent<Button>();
            button.onClick.Invoke();button.onClick.Invoke();Assert.AreEqual(0,count);
            CozyMotion.Tick(reducedMode?.06f:.07f);Assert.AreEqual(0,count);
            if(reducedMode)Assert.AreEqual(Vector2.zero,((RectTransform)view.transform.Find("Panel")).anchoredPosition);
            CozyMotion.Tick(reducedMode?.06f:.07f);Assert.AreEqual(1,count);
        }
        [Test] public void ReducedSummaryHasNoSectionOffsetsOrStagger()
        {
            CozyMotion.SetReduced(true);var view=Settlement();CozyMotion.Tick(.06f);var paper=view.transform.Find("Panel");
            Assert.AreEqual(Opacity(paper.Find("Stats/Bites")),Opacity(paper.Find("Home")),.0001f);
            foreach(var effect in paper.GetComponentsInChildren<CozyWorkspaceBodyGraphic>())Assert.AreEqual(Vector3.zero,effect.Offset);
            CozyMotion.Tick(.06f);Assert.AreEqual(1,Opacity(paper.Find("Home")));
        }
    }
}
#endif
