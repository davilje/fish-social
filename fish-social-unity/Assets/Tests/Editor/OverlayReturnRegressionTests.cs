#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Collections;
using System.Reflection;
using FishSocial.Desktop;
using FishSocial.Desktop.Auth;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Tests.Editor
{
    public sealed class OverlayReturnRegressionTests
    {
        static void Set(object obj, string name, object value) => obj.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(obj, value);
        static object Call(object obj, string name) => obj.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(obj, null);

        static IEnumerator ReturnSequence(System.Threading.Tasks.Task closed, System.Action restore, System.Func<bool> ready, System.Action completed, System.Action<System.Exception> failed) =>
            (IEnumerator)typeof(DesktopAppBootstrap).GetMethod("SequenceOverlayReturn", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { closed, restore, ready, completed, failed });

        [Test]
        public void ReturnWaitsForOverlayExitThenDesktopFrameBeforeReceipt()
        {
            var closed = new System.Threading.Tasks.TaskCompletionSource<bool>();
            var restored = 0; var receipt = 0; var visible = false;
            var sequence = ReturnSequence(closed.Task, () => restored++, () => visible, () => receipt++, error => Assert.Fail(error.ToString()));
            Assert.IsTrue(sequence.MoveNext()); Assert.AreEqual(0, restored);
            closed.SetResult(true);
            Assert.IsTrue(sequence.MoveNext()); Assert.AreEqual(1, restored); Assert.AreEqual(0, receipt);
            Assert.IsTrue(sequence.MoveNext()); Assert.AreEqual(0, receipt);
            visible = true;
            Assert.IsFalse(sequence.MoveNext()); Assert.AreEqual(1, receipt);
            Assert.IsFalse(sequence.MoveNext()); Assert.AreEqual(1, restored);
        }

        [TestCase(false)] [TestCase(true)]
        public void FailedOrCanceledCloseCannotOpenReceipt(bool canceled)
        {
            var closed = new System.Threading.Tasks.TaskCompletionSource<bool>();
            if (canceled) closed.SetCanceled(); else closed.SetException(new System.TimeoutException());
            var errors = 0;
            var sequence = ReturnSequence(closed.Task, () => Assert.Fail("restored before exit"), () => true,
                () => Assert.Fail("opened receipt after failed close"), error => errors++);
            Assert.IsFalse(sequence.MoveNext()); Assert.AreEqual(1, errors);
        }

        [Test]
        public void TargetPondSnapshotIsRetainedBeforeNativeProcessStarts()
        {
            var host = new GameObject("OverlayStateTest");
            try
            {
                var controller = host.AddComponent<NativeOverlayProcessController>();
                var state = new NativeOverlayStateDto { pondId = "pond-bamboo" };
                controller.PublishState(state);
                Assert.AreEqual(NativeOverlayLifecycleState.Stopped, controller.LifecycleState);
                Assert.Greater(state.sequence, 0);
                Assert.AreSame(state, typeof(NativeOverlayProcessController).GetField("_latestState", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller));
            }
            finally { Object.DestroyImmediate(host); }
        }

        [TestCase(true)] [TestCase(false)]
        public void SavedAvatarUpdatesOwnJournalButNeverAnotherPlayersCard(bool isSelf)
        {
            var page = Object.Instantiate(Resources.Load<GameObject>("Desktop/Prefabs/PanelProfileHub"));
            try
            {
                var panel = page.GetComponent<DesktopProfileHubPanel>();
                panel.Bind(null, null, null);
                var oldPath = DesktopDefaultAvatars.All[0].Path;
                var newPath = DesktopDefaultAvatars.All[1].Path;
                var hub = new ProfileHubDto { isSelf = isSelf, profile = new ProfileHubProfileDto {
                    playerId = "self", nickname = "Before", avatarUrl = oldPath } };
                Set(panel, "_hub", hub);
                Call(panel, "RenderCurrentTab");
                page.SetActive(false);
                panel.ApplySavedProfile(new PlayerProfileDto { playerId = "self", nickname = "After", avatarUrl = newPath, bio = "Saved" });
                Assert.AreEqual(isSelf ? newPath : oldPath, hub.profile.avatarUrl);
                page.SetActive(true);
                Call(panel, "RenderCurrentTab");
                var card = page.transform.Find("Shell/Content/Tab_资料");
                Assert.AreSame(DesktopDefaultAvatars.Portrait(isSelf ? newPath : oldPath, "self"), card.Find("Avatar").GetComponent<Image>().sprite);
                Assert.AreEqual(isSelf ? "After" : "Before", card.Find("DesignName").GetComponent<Text>().text);
            }
            finally { Object.DestroyImmediate(page); }
        }

        [Test]
        public void ReceiptDoesNotCreateBackdropWhileHiddenOrRoutingAndThenUsesNormalEntrance()
        {
            var host = new GameObject("ReceiptTest", typeof(RectTransform));
            var previousPaused = CozyMotion.Paused;
            var previousReduced = CozyMotion.Reduced;
            try
            {
                var shell = host.AddComponent<DesktopShellUi>();
                var router = host.AddComponent<PanelRouter>();
                Set(shell, "_router", router);
                Set(shell, "_settlementCanvas", host.transform);
                Set(shell, "_pendingSessionSummary", new PondSessionSummaryDto { pondName = "Target pond" });
                CozyMotion.SetReduced(false);
                CozyMotion.SetPaused(true);
                var routine = (IEnumerator)Call(shell, "PresentSummaryAfterReturn");
                Assert.IsTrue(routine.MoveNext());
                Assert.IsTrue(routine.MoveNext());
                Assert.IsNull(host.GetComponentInChildren<DesktopPondSettlementModalView>());
                Set(router, "transitioning", true);
                CozyMotion.SetPaused(false);
                Assert.IsTrue(routine.MoveNext());
                Assert.IsNull(host.GetComponentInChildren<DesktopPondSettlementModalView>());
                Set(router, "transitioning", false);
                shell.SetOverlayReturnPending(true);
                Assert.IsTrue(routine.MoveNext());
                Assert.IsNull(host.GetComponentInChildren<DesktopPondSettlementModalView>());
                shell.SetOverlayReturnPending(false);
                Assert.IsFalse(routine.MoveNext());
                var view = host.GetComponentInChildren<DesktopPondSettlementModalView>();
                Assert.IsNotNull(view);
                var alpha = view.transform.Find("Panel").GetComponent<CanvasGroup>();
                Assert.AreEqual(0, alpha.alpha, .001f);
                CozyMotion.Tick(.1f);
                Assert.That(alpha.alpha, Is.GreaterThan(0).And.LessThan(1));
            }
            finally
            {
                Object.DestroyImmediate(host);
                CozyMotion.SetReduced(previousReduced);
                CozyMotion.SetPaused(previousPaused);
            }
        }

        [Test]
        public void LeaveReceiptIsPrioritySoDisconnectCannotDropIt()
        {
            var interactive = typeof(SocketIoSocialSocketClient).GetMethod(
                "IsInteractiveSocketEvent", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsTrue((bool)interactive.Invoke(null, new object[] { "pond_session_summary" }));
        }

        [Test]
        public void LeaveAckDeliversSessionSummaryBeforeDisconnectCallback()
        {
            var client = new SocketIoSocialSocketClient("http://127.0.0.1:3001");
            var order = new System.Collections.Generic.List<string>();
            client.PondSessionSummaryReceived += summary => order.Add(summary.pondName);
            var acks = (System.Collections.Generic.Dictionary<int, System.Action<bool, string>>)
                typeof(SocketIoSocialSocketClient).GetField("_pendingAcks", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(client);
            acks[7] = (_, __) =>
            {
                order.Add("ack");
                client.Disconnect();
            };
            typeof(SocketIoSocialSocketClient).GetMethod("HandleAckPacket", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(client, new object[]
                {
                    "437[{\"ok\":true,\"sessionSummary\":{\"pondId\":\"pond-1\",\"pondName\":\"千岛湖\",\"biteCount\":2,\"catches\":[]}}]",
                });
            CollectionAssert.AreEqual(new[] { "千岛湖", "ack" }, order);
        }
    }
}
#endif
