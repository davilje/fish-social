#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FishSocial.Desktop.Tests
{
    public sealed class CozyHomeRoomTests
    {
        GameObject shell, events;
        CozyHomeRoom room;
        Camera camera;
        RenderTexture target;
        bool reduced, paused;
        readonly System.Collections.Generic.List<string> preferenceKeys = new System.Collections.Generic.List<string>();
        [SetUp] public void Setup()
        {
            reduced=CozyMotion.Reduced;paused=CozyMotion.Paused;
            CozyMotion.SetPaused(false);CozyMotion.SetReduced(false);
            shell=Object.Instantiate(Resources.Load<GameObject>("Desktop/Prefabs/DesktopShell"));
            camera=new GameObject("RoomTestCamera",typeof(Camera)).GetComponent<Camera>();
            camera.orthographic=true;camera.orthographicSize=470.5f;camera.transform.position=new Vector3(836,470.5f,-100);
            target=new RenderTexture(1672,941,24);camera.targetTexture=target;
            var canvas=shell.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=10;
            shell.GetComponent<CanvasScaler>().enabled=false;
            shell.transform.Find("LoginRoot").gameObject.SetActive(false);
            var main=shell.transform.Find("MainRoot");main.gameObject.SetActive(true);
            foreach(Transform slot in main.Find("Content"))slot.gameObject.SetActive(false);
            var homeSlot=main.Find("Content/Home");homeSlot.gameObject.SetActive(true);
            var home=Object.Instantiate(Resources.Load<GameObject>("Desktop/Prefabs/PanelHome"),homeSlot,false);
            var graphic=home.GetComponent<Graphic>();if(graphic!=null)graphic.enabled=false;
            room=CozyHomeRoom.Build(main);room.SetPageOpen(false);
            CatTheme.SkinTree(shell.transform);
            events=new GameObject("RoomTestEvents",typeof(EventSystem));Canvas.ForceUpdateCanvases();camera.Render();
        }
        [TearDown] public void Cleanup()
        {
            Object.DestroyImmediate(shell);Object.DestroyImmediate(events);
            camera.targetTexture=null;Object.DestroyImmediate(camera.gameObject);target.Release();Object.DestroyImmediate(target);
            CozyMotion.SetReduced(reduced);CozyMotion.SetPaused(paused);
            foreach (var key in preferenceKeys) PlayerPrefs.DeleteKey(key);
            preferenceKeys.Clear();
        }
        PointerEventData Pointer(Vector2 design)
        {
            var world=room.transform.TransformPoint(new Vector3(design.x,-design.y));
            var screen=RectTransformUtility.WorldToScreenPoint(camera,world);
            return new PointerEventData(events.GetComponent<EventSystem>()){position=screen,pressPosition=screen,button=PointerEventData.InputButton.Left,pointerId=1,pointerPressRaycast=new RaycastResult{module=shell.GetComponent<GraphicRaycaster>()}};
        }
        [Test] public void SavedRoomReplacesOutdoorAndBothLegacyCatPresentations()
        {
            Assert.IsFalse(shell.transform.Find("MainRoot/Background").gameObject.activeSelf);
            Assert.IsNull(shell.transform.Find("MainRoot/CozyCornerCat"));
            Assert.AreEqual(17,room.transform.Find("Props").childCount);
            var home=Resources.Load<GameObject>("Desktop/Prefabs/PanelHome");
            foreach(Transform child in home.transform)Assert.IsFalse(child.gameObject.activeSelf,child.name);
            Assert.AreEqual(0,room.GetComponentsInChildren<Shadow>(true).Length);
        }
        [Test] public void FloorPointerClickMovesTowardBoundedTarget()
        {
            ExecuteEvents.ExecuteHierarchy(room.transform.Find("Floor").gameObject,Pointer(new Vector2(2000,1000)),ExecuteEvents.pointerClickHandler);
            room.Advance(10);
            Assert.AreEqual(new Vector2(1070,775),room.Feet);
        }
        [Test] public void DragRetainsGrabOffsetAndCancelsWhenPageOpens()
        {
            var e=Pointer(new Vector2(850,650));
            room.OnBeginDrag(e);var next=Pointer(new Vector2(950,675));e.position=next.position;room.OnDrag(e);
            Assert.That(room.Feet.x,Is.EqualTo(935).Within(.01));Assert.That(room.Feet.y,Is.EqualTo(767).Within(.01));
            room.SetPageOpen(true);e.position=Pointer(new Vector2(580,700)).position;room.OnDrag(e);
            Assert.That(room.Feet.x,Is.EqualTo(935).Within(.01));
            room.SetPageOpen(false);room.Advance(1);Assert.That(room.Feet.x,Is.EqualTo(935).Within(.01));
        }
        [Test] public void TapWakesIdleCatAndReducedMotionDisablesWander()
        {
            room.Advance(30);Assert.IsTrue(room.IsSitting);
            room.OnPointerClick(Pointer(new Vector2(835,650)));Assert.IsFalse(room.IsSitting);
            CozyMotion.SetReduced(true);room.MoveTo(new Vector2(900,720));Assert.AreEqual(new Vector2(900,720),room.Feet);
            room.Advance(13);Assert.AreEqual(new Vector2(900,720),room.Feet);
        }
        [Test] public void PagesAndPauseSuspendCompanionWhilePreservingPosition()
        {
            room.SetPageOpen(true);room.Advance(60);Assert.IsFalse(room.IsSitting);
            Assert.IsFalse(room.transform.Find("Companion").gameObject.activeSelf);
            Assert.IsFalse(room.transform.Find("Floor").gameObject.activeSelf);
            room.SetPageOpen(false);CozyMotion.SetPaused(true);room.MoveTo(new Vector2(900,720));room.Advance(60);
            Assert.AreEqual(new Vector2(835,742),room.Feet);Assert.IsFalse(room.IsSitting);
        }
        [Test] public void FurnitureButtonsInvokeExistingRouteCallbacksOnce()
        {
            var equip=0;var aquarium=0;room.Bind(()=>equip++,()=>aquarium++);room.Bind(()=>equip++,()=>aquarium++);
            room.transform.Find("Equipment").GetComponent<Button>().onClick.Invoke();room.transform.Find("Aquarium").GetComponent<Button>().onClick.Invoke();
            Assert.AreEqual(1,equip);Assert.AreEqual(1,aquarium);
        }
        [TestCase(835,760,"Floor")]
        [TestCase(835,650,"Companion")]
        [TestCase(585,505,"Equipment")]
        public void NativeRaycastReachesRoomInsteadOfLegacyHome(float x,float y,string expected)
        {
            var hits=new System.Collections.Generic.List<RaycastResult>();
            // BaseRaycaster registers with EventSystem during Play Mode. Exercise its native
            // geometry directly here, after rendering the actual saved canvas in Edit Mode.
            shell.GetComponent<GraphicRaycaster>().Raycast(Pointer(new Vector2(x,y)),hits);
            var graphic=room.transform.Find(expected).GetComponent<Graphic>();
            Assert.IsNotEmpty(hits,"depth="+graphic.depth+", canvas="+graphic.canvas+", screen="+Screen.width+"x"+Screen.height);
            Assert.AreEqual(expected,hits[0].gameObject.name);
        }
        [TestCase("rod-1")]
        [TestCase("rod-2")]
        [TestCase("rod-3")]
        public void EachRodArtOpensEquipment(string id)
        {
            var opened=0;room.Bind(()=>opened++,()=>Assert.Fail("Rod opened aquarium"));
            var prop=room.Prop(id);prop.OnPointerClick(Pointer(prop.BottomCenter));
            Assert.AreEqual(1,opened);
        }
        [Test] public void BedClickMovesToNestAndUsesExistingSkinSleepPose()
        {
            room.Prop("cat-bed").OnPointerClick(Pointer(new Vector2(180,620)));
            Assert.AreEqual(CozyHomeRoom.Activity.GoingToBed,room.CurrentActivity);
            Assert.IsFalse(room.IsSleeping);
            room.Advance(10);Assert.IsTrue(room.IsSleeping);
            Assert.Less(room.Feet.x,400);Assert.Greater(room.Feet.y,600);
            var skin=FishSocial.Desktop.Pet.PetArtLoader.CurrentOwnPetId();
            Assert.AreEqual(Resources.Load<Sprite>(CozyHomeRoom.ResourceRoot+"poses/"+skin+"-sleep").texture,
                room.transform.Find("Companion").GetComponent<RawImage>().texture);
            Assert.IsTrue(room.transform.Find("BedFront").gameObject.activeSelf);
            room.MoveTo(new Vector2(835,742));Assert.IsFalse(room.IsSleeping);room.Advance(10);
            Assert.AreEqual(new Vector2(835,742),room.Feet);
        }
        [Test] public void SleepingBedFrontSamplesTheFullApprovedBackground()
        {
            CozyMotion.SetReduced(true);room.SleepInBed();
            var front=room.transform.Find("BedFront").GetComponent<RawImage>();
            Assert.AreSame(Resources.Load<Texture2D>(CozyHomeRoom.ResourceRoot+"room-base"),front.texture);
            Assert.Less(front.uvRect.width,1);Assert.Less(front.uvRect.height,.1f);
            room.SetPageOpen(true);Assert.IsFalse(front.gameObject.activeSelf);
            room.SetPageOpen(false);Assert.IsTrue(front.gameObject.activeSelf);
        }
        [TestCase("cat-bed")]
        [TestCase("food-bowls")]
        [TestCase("cat-climber")]
        public void PaintedFurnitureIgnoresDragInput(string id)
        {
            var prop=room.Prop(id);var origin=prop.BottomCenter;
            Assert.IsFalse(prop.CanDrag);
            var e=Pointer(origin);prop.OnBeginDrag(e);
            e.position=Pointer(origin+new Vector2(100,60)).position;
            prop.OnDrag(e);prop.OnEndDrag(e);
            Assert.AreEqual(origin,prop.BottomCenter);
        }
        [Test] public void PropInteractionsAreBlockedByOpenPageAndPause()
        {
            var opened=0;room.Bind(()=>opened++,()=>{});
            room.SetPageOpen(true);room.ActivateProp("rod-1");room.SleepInBed();
            Assert.AreEqual(0,opened);Assert.AreEqual(CozyHomeRoom.Activity.Free,room.CurrentActivity);
            room.SetPageOpen(false);CozyMotion.SetPaused(true);room.ActivateProp("rod-1");room.SleepInBed();
            Assert.AreEqual(0,opened);Assert.AreEqual(CozyHomeRoom.Activity.Free,room.CurrentActivity);
        }
        [Test] public void AccountChangesDoNotRestoreMovableFurnitureLayouts()
        {
            var owner="room-test-"+System.Guid.NewGuid().ToString("N");
            preferenceKeys.Add(CozyHomeRoom.LayoutKey(owner));
            var original=room.Prop("cat-bed").BottomCenter;
            PlayerPrefs.SetString(CozyHomeRoom.LayoutKey(owner),"{\"version\":1,\"items\":[{\"id\":\"cat-bed\",\"bottomCenter\":{\"x\":9999,\"y\":9999}}]}");
            room.SetLayoutOwner(owner);
            Assert.AreEqual(original,room.Prop("cat-bed").BottomCenter);
        }
        [Test] public void RuntimeBackgroundUsesApprovedPaintingBytesUnchanged()
        {
            CollectionAssert.AreEqual(System.IO.File.ReadAllBytes("../docs/美术/家园/pastel-interior-2026-10-31/room-pastel-v5.png"),
                System.IO.File.ReadAllBytes("Assets/Resources/Desktop/Theme/Cozy/home-room/room-base.png"));
        }
        [TestCase(550,400,"rod-1")]
        [TestCase(180,610,"cat-bed")]
        [TestCase(1480,655,"food-bowls")]
        [TestCase(200,470,"cat-climber")]
        public void DirectPropArtReceivesNativeRaycast(float x,float y,string expected)
        {
            var hits=new System.Collections.Generic.List<RaycastResult>();
            shell.GetComponent<GraphicRaycaster>().Raycast(Pointer(new Vector2(x,y)),hits);
            Assert.IsNotEmpty(hits);Assert.AreEqual(expected,hits[0].gameObject.name);
        }
        [Test] public void AllRegisteredCatsHaveAnActualSleepSprite()
        {
            foreach(var skin in new[]{"orange","black","blue","brown","calico","cow","cream","silver","white","dafeiyu"})
                Assert.IsNotNull(Resources.Load<Sprite>(CozyHomeRoom.ResourceRoot+"poses/"+skin+"-sleep"),skin);
        }
    }
}
#endif
