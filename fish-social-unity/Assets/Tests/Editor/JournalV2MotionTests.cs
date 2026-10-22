#if UNITY_INCLUDE_TESTS
using System;
using System.IO;
using System.Reflection;
using FishSocial.Desktop.JournalV2;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace FishSocial.Tests.Editor
{
    public sealed class JournalV2MotionTests
    {
        GameObject root;
        [Serializable] public class Golden { public GoldenCurve[] curves; }
        [Serializable] public class GoldenCurve { public string name; public float[] values; }
        [SetUp] public void SetUp()
        { JournalV2MotionState.SetPaused(false); JournalV2MotionState.SetReduced(false); root = new GameObject("JournalV2Tests",typeof(RectTransform)); }
        [TearDown] public void TearDown()
        {
            foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                if (behaviour != null && behaviour.GetType().Namespace == "FishSocial.Desktop.JournalV2") Lifecycle(behaviour,"OnDisable");
            Object.DestroyImmediate(root); JournalV2MotionState.SetPaused(false); JournalV2MotionState.SetReduced(false);
        }
        static void Lifecycle(MonoBehaviour value,string method)
        { value.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly)?.Invoke(value,null); }
        RectTransform Rect(string name,Transform parent = null)
        {
            var node = new GameObject(name,typeof(RectTransform)); node.transform.SetParent(parent != null ? parent : root.transform,false);
            var rect = node.GetComponent<RectTransform>(); rect.sizeDelta = new Vector2(200,150); return rect;
        }
        JournalV2Button Button(JournalV2ButtonKind kind,string source = "carp")
        {
            var hit = Rect("Hit"); hit.anchoredPosition = new Vector2(35,60); hit.gameObject.AddComponent<Image>().color = Color.clear;
            hit.gameObject.AddComponent<Button>(); var visual = Rect("Visual",hit); visual.pivot = Vector2.up;
            Rect("Plate",visual).gameObject.AddComponent<JournalV2Plate>(); Rect("Label",visual).gameObject.AddComponent<Text>();
            Rect("Icon",visual).gameObject.AddComponent<Image>(); Rect("LightIcon",visual).gameObject.AddComponent<Image>();
            Rect("Tape",visual).gameObject.AddComponent<Image>(); Rect("Selection",visual).gameObject.AddComponent<Image>();
            var button = hit.gameObject.AddComponent<JournalV2Button>(); button.Configure(visual,kind,source); Lifecycle(button,"OnEnable"); return button;
        }
        [Test]
        public void CubicBezierMatchesActualChromeWebAnimationSamplesIncludingOvershoot()
        {
            var golden = JsonUtility.FromJson<Golden>(File.ReadAllText(Path.Combine(Application.dataPath,"Tests/Editor/Fixtures/JournalV2WebEase.browser.json")));
            Assert.AreEqual(9,golden.curves.Length);
            foreach (var row in golden.curves)
            {
                var curve = (JournalV2WebEase.Curve)Enum.Parse(typeof(JournalV2WebEase.Curve),row.name);
                for (var i = 0; i < row.values.Length; i++)
                    Assert.That(JournalV2WebEase.Evaluate(curve,i/20f),Is.EqualTo(row.values[i]).Within(.00002f),row.name+" at "+i);
            }
            Assert.Greater(JournalV2WebEase.Evaluate(JournalV2WebEase.Curve.OutBack,.6f),1);
        }
        [Test]
        public void StickerPressSelectionAndTiltComposeOnVisualWithoutMovingHitRectangle()
        {
            var button = Button(JournalV2ButtonKind.Sticker);
            Assert.That(button.CssAngle,Is.EqualTo(.69f).Within(.0001f));
            button.SetSelected(true); button.Advance(.18f);
            button.OnPointerDown(null); button.Advance(.06f);
            Assert.AreEqual(new Vector2(1.02f,.96f),button.VisualScale);
            Assert.AreEqual(new Vector2(0,6),button.VisualOffset);
            Assert.AreEqual(new Vector2(35,60),((RectTransform)button.transform).anchoredPosition);
            Assert.AreEqual(Quaternion.identity,button.transform.localRotation); Assert.AreEqual(Vector3.one,button.transform.localScale);
            button.OnPointerUp(null); button.Advance(.22f); Assert.AreEqual(Vector2.one,button.VisualScale);
            button.ReplayTape(); Assert.AreEqual(0,button.TapeReveal); Assert.AreEqual(6,button.VisualOffset.y);
            button.Advance(.16f); Assert.AreEqual(1,button.TapeReveal);
        }
        [Test]
        public void IndexUsesSourceColorsAndSixteenPixelSelectionWhileNavigationNeverPaintsAPlate()
        {
            var index = Button(JournalV2ButtonKind.Index); index.SetSelected(true); index.Advance(.2f);
            Assert.AreEqual(new Vector2(-16,0),index.VisualOffset);
            Assert.AreEqual(Color.white,index.transform.Find("Visual/Label").GetComponent<Text>().color);
            var plate = index.transform.Find("Visual/Plate").GetComponent<JournalV2Plate>();
            Assert.AreEqual((Color)new Color32(252,146,60,255),plate.BorderColor);
            Assert.AreEqual((Color)new Color32(212,111,34,255),plate.ShadowColor);
            Assert.IsTrue(index.transform.Find("Visual/LightIcon").gameObject.activeSelf);
            Assert.AreEqual(1,index.transform.Find("Visual/LightIcon").GetComponent<Image>().color.a);
            var nav = Button(JournalV2ButtonKind.Nav); nav.SetSelected(true); nav.Advance(.16f);
            Assert.IsFalse(nav.transform.Find("Visual/Plate").GetComponent<Graphic>().enabled);
            Assert.That(Vector4.Distance((Color)new Color32(212,111,34,255),nav.transform.Find("Visual/Label").GetComponent<Text>().color),Is.LessThan(.000001f));
        }
        [Test]
        public void FirstEightCardEntranceUsesFortyMillisecondStaggerAndRankRowsHaveZeroTilt()
        {
            var card = Button(JournalV2ButtonKind.Sticker); card.PlayEntrance(2);
            card.Advance(.04f); Assert.AreEqual(0,card.Opacity); Assert.AreEqual(Vector2.one*1.12f,card.VisualScale);
            card.Advance(.3f); Assert.IsTrue(card.EntranceComplete); Assert.AreEqual(1,card.Opacity);
            card.PlayEntrance(8); Assert.IsTrue(card.EntranceComplete);
            var row = Button(JournalV2ButtonKind.RankRow); Assert.AreEqual(0,row.CssAngle);
        }
        [Test]
        public void DisabledPressShakesWithoutScaleAndReducedSettlesEveryActiveChannel()
        {
            var button = Button(JournalV2ButtonKind.Sticker); button.SetInteractable(false); button.OnPointerDown(null); button.Advance(.036f);
            Assert.AreEqual(Vector2.one,button.VisualScale); Assert.That(Mathf.Abs(button.VisualOffset.x),Is.GreaterThan(0));
            Assert.IsFalse(button.GetComponent<Button>().interactable);
            JournalV2MotionState.SetReduced(true); Assert.AreEqual(Vector2.zero,button.VisualOffset); Assert.AreEqual(Vector2.one,button.VisualScale);
            button.SetInteractable(true); button.SetSelected(true); button.ReplayTape();
            Assert.AreEqual(1,button.TapeReveal); Assert.AreEqual(new Vector2(0,6),button.VisualOffset);
        }
        [Test]
        public void PolaroidRetainsCssTiltAndUsesSourceRevealThenRestoresImmediatelyOnPause()
        {
            var host = Rect("Polaroid"); var visual = Rect("Visual",host);
            var motion = host.gameObject.AddComponent<JournalV2Polaroid>(); motion.Configure(visual,-2); Lifecycle(motion,"OnEnable");
            motion.PlayReveal(new Vector2(24,0),.26f,.3f);
            Assert.AreEqual(.9f,motion.VisualScale); Assert.AreEqual(.3f,motion.Opacity); Assert.AreEqual(new Vector2(24,0),motion.VisualOffset);
            motion.Advance(.1f); JournalV2MotionState.SetPaused(true);
            Assert.AreEqual(1,motion.VisualScale); Assert.AreEqual(1,motion.Opacity); Assert.AreEqual(Vector2.zero,motion.VisualOffset);
            Assert.Less(Quaternion.Angle(Quaternion.Euler(0,0,2),visual.localRotation),.001f);
            Assert.AreEqual(Quaternion.identity,host.localRotation);
        }
        [Test]
        public void PageOpeningMovesPaperButFlipMovesOnlyBodyAndTitleAndCancellationRestoresAllGroups()
        {
            var page = Rect("Page"); page.anchoredPosition = new Vector2(40,80);
            var body = Rect("Body",page); var title = Rect("Title",page); var rail = Rect("Rail",body);
            var middle = Rect("Middle",body); Rect("Heading",middle); var aside = Rect("Aside",body);
            var motion = page.gameObject.AddComponent<JournalV2PageAnimator>(); motion.Configure(body,title,rail,middle,aside); Lifecycle(motion,"OnEnable");
            motion.Enter(true); Assert.AreEqual(new Vector2(40,64),page.anchoredPosition); Assert.AreEqual(Vector2.zero,body.anchoredPosition);
            motion.Advance(.2f); Assert.AreEqual(new Vector2(40,80),page.anchoredPosition);
            motion.Enter(false); Assert.AreEqual(new Vector2(40,80),page.anchoredPosition); Assert.AreEqual(new Vector2(24,0),body.anchoredPosition); Assert.AreEqual(new Vector2(10,0),title.anchoredPosition);
            var callbacks = 0; motion.Exit(()=>callbacks++); motion.Advance(.06f); motion.Enter(false); motion.Advance(.2f);
            Assert.AreEqual(0,callbacks);
            JournalV2MotionState.SetPaused(true);
            Assert.AreEqual(Vector2.zero,body.anchoredPosition); Assert.AreEqual(Vector2.zero,title.anchoredPosition);
            Assert.AreEqual(1,aside.GetComponent<CanvasGroup>().alpha);
        }
        [Test]
        public void TogglePawWaitsEightyMillisecondsAndCancelsAtTheActualKnobCompletion()
        {
            var host = Rect("Toggle"); var knob = Rect("Knob",host); var paw = Rect("Paw",host); var track = Rect("Track",host).gameObject.AddComponent<Image>();
            var motion = host.gameObject.AddComponent<JournalV2Toggle>(); motion.Configure(knob,track,paw);
            motion.SetValue(true); motion.Sample(.08f); Assert.AreEqual(0,motion.KnobX); Assert.AreEqual(1,motion.PawOpacity);
            motion.Sample(.1f); Assert.AreEqual(0,motion.KnobX);
            motion.Sample(.23f); Assert.Greater(motion.KnobX,36);
            motion.Sample(.32f); Assert.AreEqual(36,motion.KnobX); Assert.AreEqual(0,motion.PawOpacity);
            motion.SetValue(false); motion.Sample(.1f); Assert.That(motion.KnobX,Is.GreaterThan(0).And.LessThan(36)); Assert.AreEqual(0,motion.PawOpacity);
            motion.Finish(); Assert.AreEqual(0,motion.KnobX);
        }
        [Test]
        public void MarkerStampUsesBackwardsFillDuringDelayAndLeavesStableHitGeometry()
        {
            var host = Rect("Marker"); var visual = Rect("Visual",host); var body = Rect("Body",visual);
            Rect("Color",body).gameObject.AddComponent<Image>(); Rect("Label",visual); Rect("LabelPill",visual); var paw = Rect("Paw",visual);
            var motion = host.gameObject.AddComponent<JournalV2MapMarker>(); motion.Configure(visual); motion.SetState(true,false,Color.red);
            Assert.AreEqual(0,paw.GetComponent<CanvasGroup>().alpha); Assert.AreEqual(Vector3.one*1.6f,paw.localScale);
            Assert.AreEqual(new Vector2(0,30),paw.anchoredPosition); motion.Advance(.06f); Assert.AreEqual(0,paw.GetComponent<CanvasGroup>().alpha);
            motion.Advance(.2f); Assert.Greater(paw.GetComponent<CanvasGroup>().alpha,0);
            Assert.AreEqual(Vector2.zero,host.anchoredPosition); Assert.AreEqual(Vector3.one,host.localScale);
        }
        [Test]
        public void ToastQueueHidesAndFreezesOnPauseThenResumesTheSameNotice()
        {
            var host = Rect("Toast"); var visual = Rect("Visual",host); var label = Rect("Label",visual).gameObject.AddComponent<Text>();
            var fish = Rect("Fish",visual); var cat = Rect("Cat",visual);
            var toast = host.gameObject.AddComponent<JournalV2Toast>(); toast.Configure(visual,label,fish,cat); Lifecycle(toast,"OnEnable");
            toast.Show("first"); toast.Show("second",true); toast.Advance(.42f);
            Assert.AreEqual("first",label.text); Assert.AreEqual(1,toast.PendingCount);
            JournalV2MotionState.SetPaused(true); toast.Advance(10); Assert.AreEqual(0,visual.GetComponent<CanvasGroup>().alpha);
            JournalV2MotionState.SetPaused(false); Assert.AreEqual("first",label.text); Assert.AreEqual(1,visual.GetComponent<CanvasGroup>().alpha);
            toast.Advance(3); Assert.AreEqual("second",label.text); Assert.IsTrue(cat.gameObject.activeSelf); Assert.IsFalse(fish.gameObject.activeSelf);
        }
        [Test]
        public void BakedButtonConfigurationSurvivesPrefabReloadWithNoLegacyComponentDependency()
        {
            const string path = "Assets/Tests/Editor/Fixtures/JournalV2MotionRoundTrip.prefab";
            var button = Button(JournalV2ButtonKind.Sticker,"golden_line"); button.SetSelected(true); button.Finish();
            try
            {
                PrefabUtility.SaveAsPrefabAsset(button.gameObject,path);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path)); instance.transform.SetParent(root.transform,false);
                var restored = instance.GetComponent<JournalV2Button>(); Lifecycle(restored,"OnEnable");
                Assert.AreEqual("golden_line",restored.SourceId); Assert.AreEqual(JournalV2ButtonKind.Sticker,restored.Kind); Assert.IsTrue(restored.Selected);
                Assert.That(restored.CssAngle,Is.EqualTo(-.53f).Within(.0001f)); Assert.AreEqual(new Vector2(0,6),restored.VisualOffset);
                restored.SetSelected(false); restored.Advance(.18f); Assert.AreEqual(Vector2.zero,restored.VisualOffset);
                foreach (var component in instance.GetComponentsInChildren<MonoBehaviour>(true)) Assert.IsFalse(component.GetType().Name.StartsWith("Cozy"));
            }
            finally { AssetDatabase.DeleteAsset(path); }
        }
    }
}
#endif
