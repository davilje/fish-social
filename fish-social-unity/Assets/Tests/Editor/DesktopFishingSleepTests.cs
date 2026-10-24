#if UNITY_INCLUDE_TESTS
using System;
using System.Collections.Concurrent;
using System.Reflection;
using FishSocial.Desktop;
using FishSocial.Desktop.Auth;
using FishSocial.Desktop.Pet;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;

namespace FishSocial.Tests.Editor
{
    public sealed class DesktopFishingSleepTests
    {
        const long SleepAt = 1792800000123L;
        GameObject _host;
        SocialPondSessionController _pond;
        SteamAuthController _auth;
        SocketIoSocialSocketClient _socket;

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("FishingSleepTest");
            _auth = _host.AddComponent<SteamAuthController>();
            SetProperty(_auth, "State", SteamLoginState.Authenticated);
            SetProperty(_auth, "PlayerId", "self");
            SetProperty(_auth, "AccessToken", "test-token");
            _pond = _host.AddComponent<SocialPondSessionController>();
            _pond.Configure(_auth);
            _socket = (SocketIoSocialSocketClient)GetField(_pond, "_socket");
            SetProperty(_socket, "State", SocialSocketState.Connected);
            SetField(_socket, "_outgoingDraining", 1);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_host);
        }

        [Test]
        public void SocketSnapshotPreservesNullableMillisecondsAndLegacyNulls()
        {
            Dispatch("pond_snapshot", "{\"users\":[{\"id\":\"self-user\",\"playerId\":\"self\"," +
                "\"fishingAsleep\":true,\"fishingSleepAt\":1792800000123," +
                "\"fishingStartedAt\":null,\"sessionStartedAt\":null,\"phaseEndsAt\":null}]}");
            Assert.IsTrue(_pond.IsFishingAsleep);
            Assert.AreEqual(SleepAt, _pond.CurrentUser.fishingSleepAt);
            Assert.AreEqual(0, _pond.CurrentUser.phaseEndsAt);

            Dispatch("pond_user_updated", "{\"id\":\"self-user\",\"playerId\":\"self\"," +
                "\"fishingAsleep\":false,\"fishingSleepAt\":null}");
            Assert.IsFalse(_pond.IsFishingAsleep);
            Assert.IsNull(_pond.CurrentUser.fishingSleepAt);

            Dispatch("pond_user_joined", "{\"id\":\"legacy-user\",\"playerId\":\"legacy\"}");
            Assert.IsFalse(_pond.VisibleOthers[0].fishingAsleep);
            Assert.IsNull(_pond.VisibleOthers[0].fishingSleepAt);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void AttentionAckCannotWakeBeforeAuthoritativeState(bool accepted)
        {
            Snapshot(User("self", true));
            var completed = false;
            _pond.FishingAttention((ok, _) =>
            {
                Assert.AreEqual(accepted, ok);
                completed = true;
            });
            var packets = (ConcurrentQueue<string>)GetField(_socket, "_outgoing");
            Assert.IsTrue(packets.TryDequeue(out var packet));
            Assert.AreEqual("421[\"fishing_attention\"]", packet);
            Assert.IsTrue(_pond.IsFishingAsleep);
            Call(_socket, "HandleAckPacket", accepted ? "431[{\"ok\":true}]" :
                "431[{\"ok\":false,\"error\":\"retry\"}]");
            Assert.IsTrue(completed);
            Assert.IsTrue(_pond.IsFishingAsleep);
            Assert.AreEqual(SleepAt, _pond.CurrentUser.fishingSleepAt);
            Snapshot(User("self", false));
            Assert.IsFalse(_pond.IsFishingAsleep);
            Assert.IsNull(_pond.CurrentUser.fishingSleepAt);
        }

        [Test]
        public void DisconnectedAttentionFailsWithoutChangingSleep()
        {
            Snapshot(User("self", true));
            SetProperty(_socket, "State", SocialSocketState.Disconnected);
            var completed = false;
            _pond.FishingAttention((ok, _) => { Assert.IsFalse(ok); completed = true; });
            Assert.IsTrue(completed);
            Assert.IsTrue(_pond.IsFishingAsleep);
            Assert.IsTrue(((ConcurrentQueue<string>)GetField(_socket, "_outgoing")).IsEmpty);
        }

        [Test]
        public void OverlayIncludesSelfAndOtherSleepAndExplicitNullOnWake()
        {
            Snapshot(User("self", true), User("other", true));
            var dto = new NativeOverlayStateDto { petVisualState = "catch" };
            OverlayPondStateBuilder.Fill(dto, _pond);
            Assert.IsTrue(dto.fishingAsleep);
            Assert.AreEqual(SleepAt, dto.fishingSleepAt);
            Assert.AreEqual("sleep", dto.petVisualState);
            Assert.AreEqual(0, dto.hookDeadlineMs);
            Assert.IsFalse(dto.hasPendingCatch);
            CollectionAssert.Contains(dto.availableActions, "fishing_attention");
            Assert.AreEqual(1, dto.users.Length);
            Assert.IsTrue(dto.users[0].fishingAsleep);
            Assert.AreEqual(SleepAt, dto.users[0].fishingSleepAt);
            Assert.AreEqual("sleep", dto.users[0].petVisualState);
            Assert.AreEqual(0, dto.users[0].hookDeadlineMs);
            var json = JObject.Parse(JsonConvert.SerializeObject(dto));
            Assert.AreEqual(SleepAt, (long)json["fishingSleepAt"]);
            Assert.AreEqual(SleepAt, (long)json["users"][0]["fishingSleepAt"]);

            Snapshot(User("self", false), User("other", false));
            OverlayPondStateBuilder.Fill(dto, _pond);
            json = JObject.Parse(JsonConvert.SerializeObject(dto));
            Assert.IsFalse(dto.fishingAsleep);
            Assert.AreEqual(JTokenType.Null, json["fishingSleepAt"].Type);
            Assert.AreEqual(JTokenType.Null, json["users"][0]["fishingSleepAt"].Type);
            CollectionAssert.Contains(dto.availableActions, "fishing_attention");
        }

        [Test]
        public void SleepClearsPendingCatchAndIgnoresLateBiteAndMiss()
        {
            Snapshot(User("self", false));
            Dispatch("fish_bite", "{\"catchId\":\"old-catch\",\"hookDurationMs\":5000}");
            Assert.IsTrue(_pond.HasPendingCatch);
            Snapshot(User("self", true));
            Assert.IsFalse(_pond.HasPendingCatch);
            var biteCount = 0;
            var missCount = 0;
            _pond.FishBiteReceived += _ => biteCount++;
            _pond.FishMissReceived += _ => missCount++;
            Dispatch("fish_bite", "{\"catchId\":\"late-catch\"}");
            Dispatch("fish_miss", "{}");
            Assert.IsFalse(_pond.HasPendingCatch);
            Assert.AreEqual("waiting", _pond.CurrentPhase);
            Assert.AreEqual(0, biteCount);
            Assert.AreEqual(0, missCount);
            Assert.AreEqual(PetVisualState.Sleep, PetStateController.Resolve(_auth, _pond, false));
            Snapshot(User("self", false));
            Assert.IsFalse(_pond.HasPendingCatch);
            Assert.AreEqual(PetVisualState.Fishing, PetStateController.Resolve(_auth, _pond, false));
        }

        [Test]
        public void SleepDiscardsPredictedHookAndDoesNotRestoreItOnWake()
        {
            Snapshot(User("self", false));
            var presenter = new OwnFishingPresenter(_pond, () => _pond, null);
            presenter.BeginHooked(5000);
            Assert.IsTrue(presenter.IsActive);
            var predicted = new NativeOverlayStateDto();
            presenter.ApplyToDto(predicted, _pond);
            CollectionAssert.Contains(predicted.availableActions, "fishing_attention");
            Snapshot(User("self", true));
            presenter.ReconcileFromServer();
            Assert.IsFalse(presenter.IsActive);
            Assert.IsNull(presenter.Phase);
            presenter.BeginHooked(5000);
            presenter.BeginStartFishing(1);
            var dto = new NativeOverlayStateDto { petVisualState = "sleep", fishingPhase = "waiting" };
            presenter.ApplyToDto(dto, _pond);
            Assert.AreEqual("sleep", dto.petVisualState);
            Assert.AreEqual("waiting", dto.fishingPhase);
            Snapshot(User("self", false));
            presenter.ReconcileFromServer();
            Assert.IsFalse(presenter.IsActive);
        }

        [TestCase("waiting")]
        [TestCase("resolving")]
        [TestCase("stopping")]
        [TestCase("baiting")]
        [TestCase("casting")]
        [TestCase("hooked")]
        [TestCase("seated")]
        public void SleepingAcrossAutomaticPhasesRetainsFishingRestPose(string phase)
        {
            var user = User("self", true);
            user.fishingPhase = phase;
            user.phaseContext = new PondUserPhaseContextDto { outcome = "escape" };
            Snapshot(user);
            var visual = PetStateController.Resolve(_auth, _pond, false);
            Assert.AreEqual(PetVisualState.Sleep, visual);
            var presenter = new OwnFishingPresenter(_pond, () => _pond, null);
            Assert.AreEqual(PetVisualState.Sleep, presenter.ResolveVisual(_pond));
            var dto = new NativeOverlayStateDto();
            OverlayPondStateBuilder.Fill(dto, _pond);
            Assert.IsTrue(dto.fishingAsleep);
            Assert.AreEqual("sleep", dto.petVisualState);
            var renderer = _host.AddComponent<SpriteFramePetRenderer>();
            Assert.AreEqual("fishing", Call(renderer, "ClipFor", visual, PetVisualState.Sleep));
        }

        [Test]
        public void NativePipeRoutesAttentionAndPublishesSleepOnlyChanges()
        {
            Snapshot(User("self", true));
            var bootstrapHost = new GameObject("SleepingOverlayBridge");
            bootstrapHost.SetActive(false);
            try
            {
                var bootstrap = bootstrapHost.AddComponent<DesktopAppBootstrap>();
                var native = _host.AddComponent<NativeOverlayProcessController>();
                SetField(bootstrap, "_pondSession", _pond);
                SetField(bootstrap, "_nativeOverlay", native);
                native.CommandReceived += command => Call(bootstrap, "OnNativeOverlayCommand", command);
                var incoming = (ConcurrentQueue<string>)GetField(native, "_incoming");
                incoming.Enqueue("{\"type\":\"command\",\"version\":1," +
                    "\"command\":\"fishing_attention\",\"commandId\":27," +
                    "\"playerId\":\"other\",\"text\":\"ignored\"}");
                Call(native, "Update");
                var packets = (ConcurrentQueue<string>)GetField(_socket, "_outgoing");
                Assert.IsTrue(packets.TryDequeue(out var packet));
                Assert.AreEqual("421[\"fishing_attention\"]", packet);
                Call(_socket, "HandleAckPacket", "431[{\"ok\":true}]");
                var sleeping = (NativeOverlayStateDto)GetField(native, "_latestState");
                Assert.IsTrue(sleeping.fishingAsleep);
                Assert.AreEqual(SleepAt, sleeping.fishingSleepAt);
                Assert.AreEqual(27, sleeping.commandAcks[0].commandId);
                Assert.IsTrue(sleeping.commandAcks[0].ok);
                Snapshot(User("self", false));
                Call(bootstrap, "FlushNativeOverlayState");
                var awake = (NativeOverlayStateDto)GetField(native, "_latestState");
                Assert.Greater(awake.sequence, sleeping.sequence);
                Assert.IsFalse(awake.fishingAsleep);
                Assert.IsNull(awake.fishingSleepAt);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(bootstrapHost);
            }
        }

        [Test]
        public void FallbackOwnClickRequestsAttentionWithoutWakingLocally()
        {
            Snapshot(User("self", true));
            var previousBootstrap = DesktopAppBootstrap.Instance;
            var bootstrapHost = new GameObject("SleepingOwnClick");
            bootstrapHost.SetActive(false);
            try
            {
                var bootstrap = bootstrapHost.AddComponent<DesktopAppBootstrap>();
                SetField(bootstrap, "_pondSession", _pond);
                typeof(DesktopAppBootstrap).GetProperty("Instance").SetValue(null, bootstrap);
                var renderer = _host.AddComponent<SpriteFramePetRenderer>();
                var pointer = new PointerEventData(null) { button = PointerEventData.InputButton.Right };
                var packets = (ConcurrentQueue<string>)GetField(_socket, "_outgoing");
                renderer.OnPointerClick(pointer);
                Assert.IsTrue(packets.IsEmpty);
                pointer.button = PointerEventData.InputButton.Left;
                pointer.dragging = true;
                renderer.OnPointerClick(pointer);
                Assert.IsTrue(packets.IsEmpty);
                pointer.dragging = false;
                renderer.OnPointerClick(pointer);
                Assert.IsTrue(packets.TryDequeue(out var packet));
                Assert.AreEqual("421[\"fishing_attention\"]", packet);
                Call(_socket, "HandleAckPacket", "431[{\"ok\":true}]");
                Assert.IsTrue(_pond.IsFishingAsleep);
                Snapshot(User("self", false));
                renderer.OnPointerClick(pointer);
                Assert.IsTrue(packets.TryDequeue(out packet));
                Assert.AreEqual("422[\"fishing_attention\"]", packet);
                Call(_socket, "HandleAckPacket", "432[{\"ok\":true}]");
                Assert.IsFalse(_pond.IsFishingAsleep);
                var seated = User("self", false);
                seated.fishingPhase = "seated";
                Snapshot(seated);
                renderer.OnPointerClick(pointer);
                Assert.IsTrue(packets.IsEmpty);
            }
            finally
            {
                typeof(DesktopAppBootstrap).GetProperty("Instance").SetValue(null, previousBootstrap);
                UnityEngine.Object.DestroyImmediate(bootstrapHost);
            }
        }

        [Test]
        public void FallbackSleepInterruptsCatchWithoutDeferringAndSupportsClick()
        {
            var renderer = _host.AddComponent<SpriteFramePetRenderer>();
            SetField(renderer, "_outcomeFlow", true);
            SetField(renderer, "_holdingCatch", true);
            SetField(renderer, "_petId", PetArtLoader.CurrentOwnPetId());
            renderer.Apply(PetVisualState.Sleep);
            Assert.IsFalse(renderer.IsHoldingCatch);
            Assert.IsFalse((bool)GetField(renderer, "_outcomeFlow"));
            Assert.IsFalse((bool)GetField(renderer, "_hasDeferred"));
            Assert.AreEqual("fishing", Call(renderer, "ClipFor", PetVisualState.Sleep, PetVisualState.Catching));
            Assert.IsInstanceOf<IPointerClickHandler>(renderer);
        }

        static PondUserDto User(string playerId, bool asleep)
        {
            return new PondUserDto
            {
                id = playerId + "-user", playerId = playerId, spotId = playerId + "-spot",
                fishingPhase = "waiting", fishingAsleep = asleep,
                fishingSleepAt = asleep ? (long?)SleepAt : null, phaseEndsAt = SleepAt + 5000,
            };
        }

        void Snapshot(params PondUserDto[] users)
        {
            Dispatch("pond_snapshot", JsonConvert.SerializeObject(new PondSnapshotDto { users = users }));
        }

        void Dispatch(string eventName, string payload) => Call(_socket, "DispatchEvent", eventName, payload);

        static object Call(object target, string method, params object[] arguments)
        {
            return target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(target, arguments);
        }

        static object GetField(object target, string name)
        {
            return target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        }

        static void SetField(object target, string name, object value)
        {
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        }

        static void SetProperty(object target, string name, object value)
        {
            target.GetType().GetProperty(name).SetValue(target, value);
        }
    }
}
#endif
