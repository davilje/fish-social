#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using FishSocial.Desktop.Auth;
using FishSocial.Desktop.Pet;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FishSocial.Desktop.Tests
{
    public sealed class CozyHomeAnimationTests
    {
        [System.Serializable]
        sealed class Manifest
        {
            public string revision;
            public CozyHomeAnimation.Clip[] clips;
        }

        GameObject shell, events;
        CozyHomeRoom room;
        Camera camera;
        RenderTexture target;
        PlayerProfileDto previousProfile;
        bool reduced, paused, hadReducedPreference;
        int reducedPreference;
        Random.State randomState;

        RawImage Artwork => room.transform.Find("Companion").GetComponent<RawImage>();
        RawImage Food => room.transform.Find("FoodLayer").GetComponent<RawImage>();
        RawImage Pour => room.transform.Find("FoodPour").GetComponent<RawImage>();

        [SetUp]
        public void Setup()
        {
            previousProfile = DesktopProfileCache.Latest;
            reduced = CozyMotion.Reduced;
            paused = CozyMotion.Paused;
            hadReducedPreference = PlayerPrefs.HasKey(CozyMotion.ReducePrefsKey);
            reducedPreference = PlayerPrefs.GetInt(CozyMotion.ReducePrefsKey);
            randomState = Random.state;
            SelectSkin("orange");
            CozyMotion.SetPaused(false);
            CozyMotion.SetReduced(false);
            var prefab = Resources.Load<GameObject>("Desktop/Prefabs/DesktopShell");
            Assert.IsNotNull(prefab);
            shell = Object.Instantiate(prefab);
            camera = new GameObject("HomeAnimationTestCamera", typeof(Camera)).GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 470.5f;
            camera.transform.position = new Vector3(836, 470.5f, -100);
            target = new RenderTexture(1672, 941, 24);
            camera.targetTexture = target;
            var canvas = shell.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 10;
            shell.GetComponent<CanvasScaler>().enabled = false;
            shell.transform.Find("LoginRoot").gameObject.SetActive(false);
            var main = shell.transform.Find("MainRoot");
            main.gameObject.SetActive(true);
            foreach (Transform slot in main.Find("Content")) slot.gameObject.SetActive(false);
            var homeSlot = main.Find("Content/Home");
            homeSlot.gameObject.SetActive(true);
            var home = Object.Instantiate(Resources.Load<GameObject>("Desktop/Prefabs/PanelHome"), homeSlot, false);
            var graphic = home.GetComponent<Graphic>();
            if (graphic != null) graphic.enabled = false;
            room = CozyHomeRoom.Build(main);
            room.SetPageOpen(false);
            room.MoveTo(new Vector2(835, 742), true);
            CatTheme.SkinTree(shell.transform);
            events = new GameObject("HomeAnimationTestEvents", typeof(EventSystem));
            Canvas.ForceUpdateCanvases();
            camera.Render();
            Assert.AreEqual("orange", PetArtLoader.CurrentOwnPetId());
            Assert.IsTrue(room.CanInteract);
        }

        [TearDown]
        public void Cleanup()
        {
            if (shell != null) Object.DestroyImmediate(shell);
            if (events != null) Object.DestroyImmediate(events);
            if (camera != null)
            {
                camera.targetTexture = null;
                Object.DestroyImmediate(camera.gameObject);
            }
            if (target != null)
            {
                target.Release();
                Object.DestroyImmediate(target);
            }
            DesktopProfileCache.Latest = previousProfile;
            CozyMotion.SetReduced(reduced);
            CozyMotion.SetPaused(paused);
            if (hadReducedPreference) PlayerPrefs.SetInt(CozyMotion.ReducePrefsKey, reducedPreference);
            else PlayerPrefs.DeleteKey(CozyMotion.ReducePrefsKey);
            PlayerPrefs.Save();
            Random.state = randomState;
        }

        static void SelectSkin(string skin)
        {
            DesktopProfileCache.Latest = new PlayerProfileDto
            {
                playerId = "home-animation-regression",
                avatarUrl = "/image/profile/cat_avatar_" + skin + ".png"
            };
        }

        PointerEventData ActorPointer()
        {
            var actor = Artwork.rectTransform;
            var screen = RectTransformUtility.WorldToScreenPoint(camera, actor.TransformPoint(actor.rect.center));
            return new PointerEventData(events.GetComponent<EventSystem>())
            {
                position = screen,
                pressPosition = screen,
                button = PointerEventData.InputButton.Left,
                pointerId = 1,
                pointerPressRaycast = new RaycastResult { module = shell.GetComponent<GraphicRaycaster>() }
            };
        }

        void SetApplicationFocus(bool focused)
        {
            var callback = typeof(CozyHomeRoom).GetMethod("OnApplicationFocus", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(callback);
            callback.Invoke(room, new object[] { focused });
        }

        PointerEventData BowlPointer(bool overlapsActor, out GameObject hitTarget)
        {
            var source = room.Prop("food-bowls");
            Assert.IsNotNull(source);
            Assert.IsFalse(source.GetComponent<Image>().enabled);
            var view = room.transform.Find("food-bowls");
            Assert.IsNotNull(view);
            Assert.IsNotNull(view.GetComponent<CozyRoomBowlView>());
            Assert.IsTrue(view.gameObject.activeInHierarchy);
            Assert.IsTrue(view.GetComponent<Image>().enabled);
            var rect = view.GetComponent<RectTransform>();
            Canvas.ForceUpdateCanvases();
            camera.Render();
            var contact = new Vector2(rect.rect.xMin + rect.rect.width * 76f / 270f, rect.rect.yMax - rect.rect.height * 24f / 88f);
            var screen = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(contact));
            var pointer = new PointerEventData(events.GetComponent<EventSystem>())
            {
                position = screen,
                pressPosition = screen,
                button = PointerEventData.InputButton.Left,
                pointerId = 2,
                eligibleForClick = true
            };
            if (overlapsActor)
                Assert.IsTrue(RectTransformUtility.RectangleContainsScreenPoint(Artwork.rectTransform, screen, camera), "Raycast must exercise the bowl/actor overlap");
            var hits = new List<RaycastResult>();
            shell.GetComponent<GraphicRaycaster>().Raycast(pointer, hits);
            Assert.IsNotEmpty(hits);
            Assert.AreEqual(view.gameObject, hits[0].gameObject, "The visible bowl proxy must win the native raycast");
            Assert.IsFalse(hits.Exists(hit => hit.gameObject == source.gameObject), "Disabled source image must not receive raycasts");
            pointer.pointerPressRaycast = hits[0];
            pointer.pointerCurrentRaycast = hits[0];
            hitTarget = hits[0].gameObject;
            Assert.AreEqual(view.gameObject, ExecuteEvents.GetEventHandler<IPointerClickHandler>(hitTarget));
            Assert.AreEqual(view.gameObject, ExecuteEvents.GetEventHandler<IDragHandler>(hitTarget));
            return pointer;
        }

        void AssertRenderedRoot(CozyHomeAnimation.Clip clip, Vector2 expected)
        {
            var rect = Artwork.rectTransform;
            var registration = new Vector2(clip.registration[0] * rect.rect.width / clip.canvas[0], clip.registration[1] * rect.rect.height / clip.canvas[1]);
            var renderedRoot = new Vector2(rect.anchoredPosition.x + registration.x, -rect.anchoredPosition.y + registration.y);
            Assert.That(Vector2.Distance(expected, renderedRoot), Is.LessThan(.01f));
            Assert.AreEqual(expected, room.Feet);
        }

        void AdvanceUntil(CozyHomeRoom.Activity expected, float timeout = 8f)
        {
            for (var elapsed = 0f; room.CurrentActivity != expected && elapsed < timeout; elapsed += 1f / 120f)
                room.Advance(1f / 120f);
            Assert.AreEqual(expected, room.CurrentActivity, "Timed out waiting for " + expected);
        }

        void AssertFoodHidden()
        {
            Assert.IsFalse(Food.gameObject.activeSelf, "Food overlay survived the interaction");
            Assert.IsFalse(Pour.gameObject.activeSelf, "Pour overlay survived the interaction");
        }

        [Test]
        public void ManifestLoadsAllTwelveClipsAndEveryResourceIncludingTwentyEightEatFrames()
        {
            var json = Resources.Load<TextAsset>(CozyHomeAnimation.ResourceRoot + "manifest");
            Assert.IsNotNull(json);
            var manifest = JsonUtility.FromJson<Manifest>(json.text);
            Assert.AreEqual("2026-10-24-eat-scale-2", manifest.revision);
            Assert.AreEqual(12, manifest.clips.Length);
            var files = new HashSet<string>();
            var eatFiles = new HashSet<string>();
            var ids = new HashSet<string>();
            foreach (var entry in manifest.clips)
            {
                Assert.IsTrue(ids.Add(entry.id), entry.id);
                var clip = CozyHomeAnimation.Find(entry.skinId, entry.action, entry.direction);
                Assert.IsNotNull(clip, entry.id);
                Assert.AreEqual(entry.id, clip.id);
                CollectionAssert.AreEqual(entry.canvas, clip.canvas);
                CollectionAssert.AreEqual(entry.registration, clip.registration);
                Assert.AreEqual(entry.frames.Length, clip.textures.Length);
                for (var index = 0; index < clip.frames.Length; index++)
                {
                    var frame = clip.frames[index];
                    StringAssert.EndsWith(".png", frame.file);
                    Assert.Greater(frame.durationMs, 0);
                    var resource = CozyHomeAnimation.ResourceRoot + frame.file.Substring(0, frame.file.Length - 4);
                    var texture = Resources.Load<Texture2D>(resource);
                    Assert.IsNotNull(texture, resource);
                    Assert.AreSame(texture, clip.textures[index], resource);
                    Assert.AreEqual(clip.canvas[0], texture.width, resource);
                    Assert.AreEqual(clip.canvas[1], texture.height, resource);
                    Assert.IsNotNull(Resources.Load<Sprite>(resource), resource);
                    files.Add(frame.file);
                    if (entry.action == "eat") eatFiles.Add(frame.file);
                }
            }
            Assert.AreEqual(112, files.Count);
            Assert.AreEqual(28, eatFiles.Count);
            Assert.IsTrue(room.HasFeedingAnimation);
        }

        [TestCase("left")]
        [TestCase("right")]
        public void EatingSamplesIntroThreeLoopsAndOutroAcrossTwoPointFiveSeconds(string direction)
        {
            var clip = CozyHomeAnimation.Find("orange", "eat", direction);
            Assert.IsNotNull(clip);
            Assert.AreEqual(14, clip.frames.Length);
            CollectionAssert.AreEqual(new[] { 3, 11 }, clip.loop);
            Assert.That(clip.Duration, Is.EqualTo(14f / 12f).Within(.0001f));
            Assert.That(clip.PlaybackDuration(3), Is.EqualTo(2.5f).Within(.0001f));
            var expected = new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 3, 4, 5, 6, 7, 8, 9, 10, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13 };
            for (var index = 0; index < expected.Length; index++)
                Assert.AreEqual(expected[index], clip.FrameAt((index + .5f) / 12f, 3), "Playback sample " + index);
            Assert.AreEqual(0, clip.FrameAt(-1, 3));
            Assert.AreEqual(13, clip.FrameAt(2.501f, 3));
            Assert.AreEqual(13, clip.FrameAt(10, 3));
        }

        [TestCase("left")]
        [TestCase("right")]
        public void WalkingSamplesEveryFrameAndWrapsAcrossSeveralCycles(string direction)
        {
            var clip = CozyHomeAnimation.Find("orange", "walk", direction);
            Assert.IsNotNull(clip);
            for (var index = 0; index < 36; index++)
                Assert.AreEqual(index % 12, clip.FrameAt((index + .5f) / 12f, repeat: true), "Playback sample " + index);
        }

        [TestCase(CozyHomeRoom.Activity.Pouring)]
        [TestCase(CozyHomeRoom.Activity.GoingToFood)]
        [TestCase(CozyHomeRoom.Activity.Eating)]
        public void RepeatedFeedDoesNotRestartAnyFeedingPhase(CozyHomeRoom.Activity phase)
        {
            room.Feed();
            AdvanceUntil(phase);
            room.Advance(.125f);
            Assert.AreEqual(phase, room.CurrentActivity);
            var feet = room.Feet;
            var action = room.AnimationAction;
            var frame = room.AnimationFrame;
            var texture = Artwork.texture;
            var food = Food.texture;
            var pour = Pour.texture;
            for (var attempt = 0; attempt < 5; attempt++) room.Feed();
            Assert.AreEqual(phase, room.CurrentActivity);
            Assert.AreEqual(feet, room.Feet);
            Assert.AreEqual(action, room.AnimationAction);
            Assert.AreEqual(frame, room.AnimationFrame);
            Assert.AreSame(texture, Artwork.texture);
            Assert.AreSame(food, Food.texture);
            Assert.AreSame(pour, Pour.texture);
            for (var elapsed = 0f; room.IsFeeding && elapsed < 9f; elapsed += .05f)
            {
                room.Feed();
                room.Advance(.05f);
            }
            Assert.IsFalse(room.IsFeeding, "Repeated Feed extended the feeding sequence indefinitely");
            Assert.AreEqual(CozyHomeRoom.Activity.Free, room.CurrentActivity);
            AssertFoodHidden();
        }

        [Test]
        public void FeedingPoursApproachesEatsThreeCyclesAndHidesFoodWhenFinished()
        {
            var origin = room.Feet;
            room.Feed();
            Assert.IsTrue(room.IsFeeding);
            Assert.AreEqual(CozyHomeRoom.Activity.Pouring, room.CurrentActivity);
            Assert.IsTrue(Pour.gameObject.activeSelf);
            Assert.IsTrue(Food.gameObject.activeSelf);
            var firstPour = Pour.texture;
            var firstFood = Food.texture;
            room.Advance(.4f);
            Assert.AreNotSame(firstPour, Pour.texture);
            Assert.AreNotSame(firstFood, Food.texture);
            Assert.AreEqual(origin, room.Feet);
            AdvanceUntil(CozyHomeRoom.Activity.GoingToFood);
            Assert.IsFalse(Pour.gameObject.activeSelf);
            room.Advance(.5f);
            Assert.Greater(Vector2.Distance(origin, room.Feet), 1);
            Assert.AreEqual("walk", room.AnimationAction);
            AdvanceUntil(CozyHomeRoom.Activity.Eating);
            Assert.AreEqual("eat", room.AnimationAction);
            var eatingFeet = room.Feet;
            var states = CozyHomeAnimation.Find("shared", "bowl-food");
            Assert.AreSame(states.textures[1], Food.texture);
            var frames = new List<int>();
            var previousFrame = -1;
            var elapsedEating = 0f;
            var sawTwoThirds = false;
            var sawOneThird = false;
            while (room.IsFeeding && elapsedEating < 3f)
            {
                Assert.AreEqual(CozyHomeRoom.Activity.Eating, room.CurrentActivity);
                Assert.That(Vector2.Distance(eatingFeet, room.Feet), Is.LessThan(.01f));
                var frame = room.AnimationFrame;
                if (frame != previousFrame) frames.Add(frame);
                previousFrame = frame;
                var clip = CozyHomeAnimation.Find("orange", "eat", "right");
                Assert.AreSame(clip.textures[frame], Artwork.texture);
                sawTwoThirds |= Food.texture == states.textures[2];
                sawOneThird |= Food.texture == states.textures[3];
                room.Advance(1f / 120f);
                elapsedEating += 1f / 120f;
            }
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 3, 4, 5, 6, 7, 8, 9, 10, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13 }, frames);
            Assert.That(elapsedEating, Is.EqualTo(2.5f).Within(.025f));
            Assert.IsTrue(sawTwoThirds);
            Assert.IsTrue(sawOneThird);
            Assert.IsFalse(room.IsFeeding);
            Assert.AreEqual(CozyHomeRoom.Activity.Free, room.CurrentActivity);
            AssertFoodHidden();
            room.Advance(.25f);
            AssertFoodHidden();
        }

        [Test]
        public void PetRollChangesTexturesWithoutRootDriftAndHoldsItsFinalFrameWithoutLooping()
        {
            var feet = room.Feet;
            var clip = CozyHomeAnimation.Find("orange", "pet-roll");
            Assert.IsNotNull(clip);
            room.Respond();
            Assert.AreEqual(CozyHomeRoom.Activity.Rolling, room.CurrentActivity);
            Assert.AreEqual("pet-roll", room.AnimationAction);
            var first = Artwork.texture;
            room.Advance(.2f);
            Assert.AreNotSame(first, Artwork.texture);
            AssertRenderedRoot(clip, feet);
            room.Advance(1.4f);
            Assert.AreEqual(CozyHomeRoom.Activity.Free, room.CurrentActivity);
            AssertRenderedRoot(clip, feet);
            var finalFrame = clip.frames.Length - 1;
            Assert.AreEqual(finalFrame, room.AnimationFrame);
            Assert.AreSame(clip.textures[finalFrame], Artwork.texture);
            var position = Artwork.rectTransform.anchoredPosition;
            var size = Artwork.rectTransform.sizeDelta;
            for (var sample = 0; sample < 16; sample++)
            {
                room.Advance(.1f);
                Assert.AreEqual(CozyHomeRoom.Activity.Free, room.CurrentActivity);
                Assert.AreEqual(finalFrame, room.AnimationFrame, "Completed roll restarted");
                Assert.AreSame(clip.textures[finalFrame], Artwork.texture);
                AssertRenderedRoot(clip, feet);
                Assert.That(Vector2.Distance(position, Artwork.rectTransform.anchoredPosition), Is.LessThan(.01f));
                Assert.AreEqual(size, Artwork.rectTransform.sizeDelta);
            }
        }

        [TestCase("page", false)]
        [TestCase("page", true)]
        [TestCase("drag", false)]
        [TestCase("drag", true)]
        [TestCase("furniture", false)]
        [TestCase("furniture", true)]
        [TestCase("focus", false)]
        [TestCase("focus", true)]
        [TestCase("skin", false)]
        [TestCase("skin", true)]
        [TestCase("pause", false)]
        [TestCase("pause", true)]
        public void InterruptingFeedingClearsOverlaysAndDoesNotResume(string interruption, bool eating)
        {
            room.Feed();
            if (eating) AdvanceUntil(CozyHomeRoom.Activity.Eating);
            room.Advance(.2f);
            Assert.IsTrue(room.IsFeeding);
            var pointer = ActorPointer();
            switch (interruption)
            {
                case "page": room.SetPageOpen(true); break;
                case "drag": room.OnBeginDrag(pointer); break;
                case "furniture": room.BeginFurnitureDrag(); break;
                case "focus": SetApplicationFocus(false); break;
                case "skin": SelectSkin("dafeiyu"); room.Advance(.01f); break;
                case "pause": CozyMotion.SetPaused(true); room.Advance(.01f); break;
            }
            Assert.IsFalse(room.IsFeeding, interruption);
            Assert.AreEqual(CozyHomeRoom.Activity.Free, room.CurrentActivity);
            Assert.IsNull(room.AnimationAction);
            AssertFoodHidden();
            var stopped = room.Feet;
            if (interruption == "page" || interruption == "focus" || interruption == "pause")
            {
                room.Feed();
                room.Advance(.5f);
                Assert.IsFalse(room.IsFeeding);
                Assert.AreEqual(stopped, room.Feet);
            }
            switch (interruption)
            {
                case "page": room.SetPageOpen(false); break;
                case "drag": room.OnEndDrag(pointer); break;
                case "furniture": room.EndFurnitureDrag(); break;
                case "focus": SetApplicationFocus(true); break;
                case "pause": CozyMotion.SetPaused(false); break;
            }
            room.Advance(2f);
            Assert.IsFalse(room.IsFeeding);
            Assert.AreEqual(stopped, room.Feet);
            AssertFoodHidden();
            if (interruption == "skin")
            {
                var rest = Resources.Load<Sprite>(CozyJournalTheme.ResourceRoot + "characters/dafeiyu/rest");
                Assert.IsNotNull(rest);
                Assert.AreSame(rest.texture, Artwork.texture);
                Assert.IsFalse(room.HasFeedingAnimation);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void BowlProxyReceivesActualRaycastBeforeAndDuringEating(bool eating)
        {
            if (eating)
            {
                room.Feed();
                AdvanceUntil(CozyHomeRoom.Activity.Eating);
                room.Advance(.2f);
            }
            var feet = room.Feet;
            var frame = room.AnimationFrame;
            var texture = Artwork.texture;
            var pointer = BowlPointer(eating, out var hitTarget);
            var handler = ExecuteEvents.ExecuteHierarchy(hitTarget, pointer, ExecuteEvents.pointerClickHandler);
            Assert.AreEqual(room.transform.Find("food-bowls").gameObject, handler);
            Assert.IsTrue(room.IsFeeding);
            if (eating)
            {
                Assert.AreEqual(CozyHomeRoom.Activity.Eating, room.CurrentActivity);
                Assert.AreEqual(feet, room.Feet);
                Assert.AreEqual(frame, room.AnimationFrame);
                Assert.AreSame(texture, Artwork.texture);
            }
            else Assert.AreEqual(CozyHomeRoom.Activity.Pouring, room.CurrentActivity);
        }

        [Test]
        public void DraggingRaycastBowlProxyDuringEatingCancelsFeedingAndImmediatelyMovesSourceAndView()
        {
            room.Feed();
            AdvanceUntil(CozyHomeRoom.Activity.Eating);
            room.Advance(.2f);
            var source = room.Prop("food-bowls");
            var sourceRect = source.GetComponent<RectTransform>();
            var view = room.transform.Find("food-bowls").GetComponent<RectTransform>();
            var origin = source.BottomCenter;
            var pointer = BowlPointer(true, out var hitTarget);
            var press = room.PointerPosition(pointer);
            var handler = ExecuteEvents.ExecuteHierarchy(hitTarget, pointer, ExecuteEvents.beginDragHandler);
            Assert.AreEqual(view.gameObject, handler);
            Assert.IsFalse(room.IsFeeding);
            Assert.AreEqual(CozyHomeRoom.Activity.Free, room.CurrentActivity);
            Assert.IsFalse(pointer.eligibleForClick);
            AssertFoodHidden();
            var offset = new Vector2(40, 25);
            var destination = press + offset;
            var world = room.transform.TransformPoint(new Vector3(destination.x, -destination.y));
            pointer.position = RectTransformUtility.WorldToScreenPoint(camera, world);
            pointer.pointerDrag = hitTarget;
            pointer.dragging = true;
            Assert.AreEqual(view.gameObject, ExecuteEvents.ExecuteHierarchy(hitTarget, pointer, ExecuteEvents.dragHandler));
            var expected = CozyHomeRoom.ClampFurniture("food-bowls", origin + offset);
            Assert.Greater(Vector2.Distance(origin, source.BottomCenter), 1);
            Assert.That(Vector2.Distance(expected, source.BottomCenter), Is.LessThan(.02f));
            Assert.That(Vector2.Distance(sourceRect.anchoredPosition, view.anchoredPosition), Is.LessThan(.01f), "Proxy must follow during the drag event, without Advance");
            Assert.AreEqual(sourceRect.rect.size, view.rect.size);
            Assert.IsFalse(source.GetComponent<Image>().enabled);
            Assert.AreEqual(view.gameObject, ExecuteEvents.ExecuteHierarchy(hitTarget, pointer, ExecuteEvents.endDragHandler));
            ExecuteEvents.ExecuteHierarchy(hitTarget, pointer, ExecuteEvents.pointerClickHandler);
            Assert.IsFalse(room.IsFeeding, "Drag release must not feed again");
            room.Advance(.25f);
            Assert.IsFalse(room.IsFeeding);
            Assert.That(Vector2.Distance(expected, source.BottomCenter), Is.LessThan(.02f));
            Assert.That(Vector2.Distance(sourceRect.anchoredPosition, view.anchoredPosition), Is.LessThan(.01f));
            AssertFoodHidden();
        }

        [Test]
        public void ReducedMotionUsesStaticEatingCompletesFeedingAndMovesImmediately()
        {
            CozyMotion.SetReduced(true);
            var destination = new Vector2(950, 720);
            room.MoveTo(destination);
            Assert.AreEqual(destination, room.Feet);
            var rest = Artwork.texture;
            room.Respond();
            room.Advance(.2f);
            Assert.AreEqual(CozyHomeRoom.Activity.Free, room.CurrentActivity);
            Assert.IsNull(room.AnimationAction);
            Assert.AreSame(rest, Artwork.texture);
            room.Feed();
            Assert.AreEqual(CozyHomeRoom.Activity.Eating, room.CurrentActivity);
            Assert.IsFalse(Pour.gameObject.activeSelf);
            var contact = room.Feet;
            var eating = Artwork.texture;
            room.Advance(.6f);
            Assert.AreEqual(contact, room.Feet);
            Assert.AreSame(eating, Artwork.texture);
            room.Advance(.6f);
            Assert.AreSame(eating, Artwork.texture);
            room.Advance(1.4f);
            Assert.IsFalse(room.IsFeeding);
            Assert.AreEqual(CozyHomeRoom.Activity.Free, room.CurrentActivity);
            AssertFoodHidden();
        }

        [Test]
        public void EnablingReducedMotionDuringRollCancelsItAtTheOriginalRoot()
        {
            var origin = room.Feet;
            var rest = Artwork.texture;
            room.Respond();
            room.Advance(.25f);
            Assert.AreEqual("pet-roll", room.AnimationAction);
            CozyMotion.SetReduced(true);
            room.Advance(.01f);
            Assert.AreEqual(CozyHomeRoom.Activity.Free, room.CurrentActivity);
            Assert.IsNull(room.AnimationAction);
            Assert.AreEqual(origin, room.Feet);
            Assert.AreSame(rest, Artwork.texture);
        }

        [Test]
        public void DafeiyuNeverFallsBackToOrangeFeedingRollingOrWalkingTextures()
        {
            SelectSkin("dafeiyu");
            room.Advance(.01f);
            var rest = Resources.Load<Sprite>(CozyJournalTheme.ResourceRoot + "characters/dafeiyu/rest");
            Assert.IsNotNull(rest);
            Assert.AreSame(rest.texture, Artwork.texture);
            Assert.IsFalse(room.HasFeedingAnimation);
            Assert.IsFalse(CozyHomeAnimation.CanWalk("dafeiyu"));
            Assert.IsNull(CozyHomeAnimation.Find("dafeiyu", "pet-roll"));
            foreach (var direction in new[] { "left", "right" })
                Assert.IsNull(CozyHomeAnimation.Find("dafeiyu", "eat", direction));
            room.Feed();
            room.Advance(.2f);
            Assert.IsFalse(room.IsFeeding);
            AssertFoodHidden();
            Assert.AreSame(rest.texture, Artwork.texture);
            room.Respond();
            room.Advance(.2f);
            Assert.AreEqual(CozyHomeRoom.Activity.Free, room.CurrentActivity);
            Assert.IsNull(room.AnimationAction);
            Assert.AreSame(rest.texture, Artwork.texture);
            var origin = room.Feet;
            room.MoveTo(new Vector2(1000, 742));
            room.Advance(.25f);
            Assert.Greater(room.Feet.x, origin.x);
            Assert.IsNull(room.AnimationAction);
            Assert.AreSame(rest.texture, Artwork.texture);
        }

        [TestCase(1030f, "right")]
        [TestCase(600f, "left")]
        public void LocomotionChangesStartWalkAndStopTexturesAndReachesDestination(float destinationX, string direction)
        {
            var origin = room.Feet;
            var destination = new Vector2(destinationX, origin.y);
            room.MoveTo(destination);
            room.Advance(.12f);
            Assert.AreEqual("walk-start", room.AnimationAction);
            Assert.AreEqual(origin, room.Feet);
            var start = Artwork.texture;
            room.Advance(.12f);
            Assert.AreNotSame(start, Artwork.texture);
            room.Advance(.2f);
            Assert.AreEqual("walk", room.AnimationAction);
            Assert.Greater(Vector2.Distance(origin, room.Feet), 0);
            var walk = CozyHomeAnimation.Find("orange", "walk", direction);
            Assert.AreSame(walk.textures[room.AnimationFrame], Artwork.texture);
            var walking = Artwork.texture;
            room.Advance(.12f);
            Assert.AreNotSame(walking, Artwork.texture);
            for (var elapsed = 0f; Vector2.Distance(room.Feet, destination) > .01f && elapsed < 4f; elapsed += .02f)
                room.Advance(.02f);
            Assert.That(Vector2.Distance(destination, room.Feet), Is.LessThan(.01f));
            Assert.AreEqual("walk-stop", room.AnimationAction);
            var stop = Artwork.texture;
            room.Advance(.12f);
            Assert.AreNotSame(stop, Artwork.texture);
            Assert.AreEqual(destination, room.Feet);
            room.Advance(.5f);
            Assert.AreEqual(destination, room.Feet);
        }
    }
}
#endif
