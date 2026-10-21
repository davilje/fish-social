#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using FishSocial.Desktop;
using FishSocial.Desktop.Auth;
using NUnit.Framework;

namespace FishSocial.Tests.Editor
{
    public sealed class CozyPondSelectionTests
    {
        static WorldMapPondView Paid(string id = "paid") => new WorldMapPondView
        {
            pondId = id, isOpen = true, pondCategory = "advanced", minPlayerLevel = 5,
            allowsAutoReturn = true, feePer2hSellOnly = 20, feePer2hAutoReturn = 40,
        };

        [Test]
        public void SamePondAcrossMapAndListKeepsSelectedFeeMode()
        {
            var state = new PondSelectionState();
            state.Select(Paid());
            state.SelectFeeMode("auto_return");
            state.Select(Paid()); // Catalog reload creates another view of the same pond.
            Assert.AreEqual("auto_return", state.ReturnFeeMode);
            Assert.AreEqual(40, state.ActiveFee(state.Selected));
        }

        [Test]
        public void DifferentPondStartsWithItsDefaultMode()
        {
            var state = new PondSelectionState();
            state.Select(Paid());
            state.SelectFeeMode("auto_return");
            state.Select(Paid("other"));
            Assert.AreEqual("sell_only", state.ReturnFeeMode);
        }

        [Test]
        public void ConnectingLocksSelectionAndModeUntilFailure()
        {
            var state = new PondSelectionState();
            state.Select(Paid());
            state.SelectFeeMode("auto_return");
            state.SetEntryStatus(true, "正在进入");
            state.Select(Paid("other"));
            state.SelectFeeMode("sell_only");
            Assert.AreEqual("paid", state.Selected.pondId);
            Assert.AreEqual("auto_return", state.ReturnFeeMode);
            state.SetEntryStatus(false, "余额不足", true);
            Assert.IsTrue(state.EntryFailed);
            Assert.AreEqual("auto_return", state.ReturnFeeMode);
            Assert.AreEqual("余额不足", state.Status);
            state.SelectFeeMode("sell_only");
            Assert.AreEqual("sell_only", state.ReturnFeeMode);
        }

        [Test]
        public void HiddenOldPageCannotCancelActivePagesProgressRequest()
        {
            var state = new PondSelectionState();
            var activePage = new object();
            var oldPage = new object();
            var checkedOwner = false;
            state.Changed += () =>
            {
                if (!state.ProgressLoading || checkedOwner) return;
                checkedOwner = true;
                state.CancelProgress(oldPage);
                Assert.IsTrue(state.ProgressLoading);
                state.CancelProgress(activePage);
                Assert.IsFalse(state.ProgressLoading);
            };
            var request = state.LoadProgress(activePage);
            while (request.MoveNext()) { }
            Assert.IsTrue(checkedOwner);
            Assert.IsFalse(state.ProgressLoading);
        }

        [Test]
        public void UnknownProgressCannotClaimPondIsAccessible()
        {
            var state = new PondSelectionState();
            var pond = Paid();
            Assert.IsFalse(state.CanEnter(pond));
            StringAssert.Contains("等级信息", state.AccessReason(pond));
        }

        [TestCase(false, 10, false)]
        [TestCase(true, 4, false)]
        [TestCase(true, 5, true)]
        public void EntryUsesRealOnboardingAndLevel(bool completed, int level, bool expected)
        {
            var state = new PondSelectionState();
            state.ApplyProgress(new FishingProgressDto { onboardingCompleted = completed, level = level });
            Assert.AreEqual(expected, state.CanEnter(Paid()));
        }

        [Test]
        public void FailedRefreshDoesNotRetainStaleAccess()
        {
            var state = new PondSelectionState();
            state.ApplyProgress(new FishingProgressDto { onboardingCompleted = true, level = 10 });
            Assert.IsTrue(state.CanEnter(Paid()));
            state.ApplyProgress(null, "网络暂不可用");
            Assert.IsFalse(state.CanEnter(Paid()));
            Assert.AreEqual("网络暂不可用", state.AccessReason(Paid()));
        }

        [Test]
        public void ClosedAndGiantPondsStayUnavailableAtHighLevel()
        {
            var state = new PondSelectionState();
            state.ApplyProgress(new FishingProgressDto { onboardingCompleted = true, level = 100 });
            var pond = Paid();
            pond.isOpen = false;
            Assert.IsFalse(state.CanEnter(pond));
            pond.isOpen = true;
            pond.pondCategory = "giant";
            Assert.IsFalse(state.CanEnter(pond));
        }

        [TestCase("sell_only", "auto_return", false)]
        [TestCase("auto_return", "sell_only", false)]
        [TestCase("auto_return", "auto_return", true)]
        [TestCase(null, "sell_only", true)]
        public void SamePondMaySkipRejoinOnlyWithMatchingFeeMode(string current, string requested, bool same)
        {
            Assert.AreEqual(same, SocialPondSessionController.SameReturnFeeMode(current, requested));
        }

        [Test]
        public void FreeFilterDoesNotIncludePaidOptionalMode()
        {
            var pond = new WorldMapPondView { pondId = "free", allowsAutoReturn = true, feePer2hAutoReturn = 40 };
            Assert.IsFalse(PondSelectionState.IsFree(pond));
            pond.feePer2hAutoReturn = 0;
            Assert.IsTrue(PondSelectionState.IsFree(pond));
        }
    }
}
#endif
