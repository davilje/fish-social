#if OVERLAY_PERFORMANCE_TESTS
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using FishSocialOverlay;
using Data = OverlayFishingMotionTestData;

internal static class OverlayFishingMotionNativeTests
{
    static readonly BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;
    static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, PrivateInstance).GetValue(owner);
    static T StaticField<T>(string name) => (T)typeof(OverlayFishingLayers).GetField(name, BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
    static void Invoke(OverlayPetActor actor, string method)
        => typeof(OverlayPetActor).GetMethod(method, PrivateInstance).Invoke(actor, null);
    static void Wait(Func<bool> done)
    {
        var watch = Stopwatch.StartNew();
        while (!done())
        {
            Data.Check(watch.ElapsedMilliseconds < 15000, "motion native test timed out");
            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => frame.Continue = false));
            Dispatcher.PushFrame(frame);
            Thread.Sleep(1);
        }
    }
    static void Load(string pet)
    {
        var task = OverlayFishingLayers.EnsurePackAsync(pet);
        Wait(() => task.IsCompleted); Data.Check(task.Result, "motion pack missing: " + pet);
    }

    public static void PacksAndSourcePaths()
    {
        foreach (var pet in Data.Pets)
        {
            Load(pet);
            foreach (var name in Data.ExpandedClips(pet))
            {
                var original = Data.Original(pet, name);
                var expanded = OverlayFishingLayers.TryOpen(pet, name);
                Data.Check(expanded != null, "missing expanded clip: " + pet + "/" + name);
                Data.CheckExpansion(name, original, expanded);
                Data.CheckRig(name, expanded);
                var uniquePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (var index = 0; index < expanded.FrameCount; index++)
                    foreach (var layer in OverlayFishingLayers.LayerNames)
                    {
                        var body = layer == "body" || layer == "head" || layer == "far_hand" || layer == "hands";
                        var expected = Path.Combine(body ? Path.Combine(Data.Root, pet, "body") : Path.Combine(Data.Root, "shared"),
                            name, expanded.SourceIndex(index).ToString(), layer + ".png");
                        var actual = OverlayFishingLayers.LayerFile(pet, name, index, layer);
                        Data.Check(actual == expected && File.Exists(actual), "virtual frame requested a new PNG: " + actual);
                        uniquePaths.Add(actual);
                    }
                Data.Check(uniquePaths.Count == original.FrameCount * 9, "virtual frames multiplied PNG resources");
            }
        }
    }

    public static void CacheReferences()
    {
        Load("orange"); Load("calico");
        var cache = StaticField<OverlayResourceCache<BitmapImage>>("Bitmaps");
        var references = Field<Dictionary<string, int>>(cache, "_references");
        var users = StaticField<Dictionary<string, int>>("ClipUsers");
        var resources = StaticField<Dictionary<string, string[]>>("ClipResources");
        foreach (var name in Data.Clips)
        {
            var baseline = new Dictionary<string, int>(references, StringComparer.OrdinalIgnoreCase);
            var orangeKey = "orange|" + name; var calicoKey = "calico|" + name;
            Data.Check(!users.ContainsKey(orangeKey) && !users.ContainsKey(calicoKey), "test started with live clip users");
            var orangeUsers = 0; var calicoUsers = 0;
            try
            {
                OverlayFishingLayers.Acquire("orange", name); orangeUsers++;
                OverlayFishingLayers.Acquire("orange", name); orangeUsers++;
                OverlayFishingLayers.Acquire("calico", name); calicoUsers++;
                Data.Check(users[orangeKey] == 2 && users[calicoKey] == 1, "actor clip counts are not balanced");
                var expected = new Dictionary<string, int>(baseline, StringComparer.OrdinalIgnoreCase);
                foreach (var key in new[] { orangeKey, calicoKey })
                {
                    Data.Check(resources[key].Length == Data.Original("orange", name).FrameCount * 9, "retained a resource for every virtual frame");
                    foreach (var path in resources[key]) { expected.TryGetValue(path, out var count); expected[path] = count + 1; }
                }
                SameReferences(expected, references);
                var clip = OverlayFishingLayers.TryOpen("orange", name);
                var sampledIndex = Array.FindIndex(clip.Motion, sample => sample.Position != sample.Source);
                Data.Check(sampledIndex >= 0, "clip contains no intermediate sample");
                var sampledSource = clip.SourceIndex(sampledIndex);
                var firstIndex = clip.IndexAtSource(sampledSource);
                var decoded = OverlayFishingLayers.DecodeCount;
                var source = OverlayFishingLayers.BitmapFor(OverlayFishingLayers.LayerFile("orange", name, firstIndex, "rod"), 61, 49);
                Data.Check(source != null, "source PNG failed to decode");
                for (var index = firstIndex; index < clip.FrameCount && clip.SourceIndex(index) == sampledSource; index++)
                    Data.Check(ReferenceEquals(source, OverlayFishingLayers.BitmapFor(
                        OverlayFishingLayers.LayerFile("orange", name, index, "rod"), 61, 49)), "samples duplicated decoded PNGs");
                Data.Check(OverlayFishingLayers.DecodeCount - decoded <= 1, "virtual frames increased decode count");
                OverlayFishingLayers.Release("orange", name); orangeUsers--;
                Data.Check(users[orangeKey] == 1, "first release removed the second actor");
                SameReferences(expected, references);
            }
            finally
            {
                while (orangeUsers-- > 0) OverlayFishingLayers.Release("orange", name);
                while (calicoUsers-- > 0) OverlayFishingLayers.Release("calico", name);
            }
            SameReferences(baseline, references);
            Data.Check(!users.ContainsKey(orangeKey) && !users.ContainsKey(calicoKey), "final release leaked clip users");
        }
    }

    static void SameReferences(Dictionary<string, int> expected, Dictionary<string, int> actual)
    {
        Data.Check(expected.Count == actual.Count, "retained resource count changed");
        foreach (var pair in expected)
            Data.Check(actual.TryGetValue(pair.Key, out var count) && count == pair.Value, "reference count mismatch: " + pair.Key);
    }

    public static void ThinLineRendering()
    {
        Load("orange");
        var checkedLines = 0;
        foreach (var name in Data.Clips)
            foreach (var sample in OverlayFishingLayers.TryOpen("orange", name).Motion)
            {
                if (sample.Line == null) continue;
                var drawing = OverlayFishingMotionDrawing.Line(sample) as DrawingImage;
                Data.Check(drawing != null && drawing.IsFrozen && drawing.Width == 640 && drawing.Height == 512,
                    "line drawing lost canvas size or freezing");
                Data.Check(ReferenceEquals(drawing, OverlayFishingMotionDrawing.Line(sample)), "line drawing was recreated for the same sample");
                foreach (var scale in new[] { .25, .5, 1d }) AssertThinDrawing(drawing, Transform.Identity, scale);
                checkedLines++;
            }
        Data.Check(checkedLines > 10, "no intermediate lines exercised the WPF renderer");
    }

    static void AssertThinDrawing(ImageSource source, Transform transform, double scale)
    {
        var width = (int)Math.Round(640 * scale); var height = (int)Math.Round(512 * scale);
        var image = new Image { Source = source, Width = width, Height = height, Stretch = Stretch.Fill, RenderTransform = transform };
        image.Measure(new Size(width, height)); image.Arrange(new Rect(0, 0, width, height)); image.UpdateLayout();
        var rendered = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        rendered.Render(image);
        var pixels = new byte[width * height * 4]; rendered.CopyPixels(pixels, width * 4, 0);
        var visible = 0;
        for (var index = 3; index < pixels.Length; index += 4) if (pixels[index] > 0) visible++;
        Data.Check(visible > 0 && visible < width * height * .06, "line rendered as an empty image or opaque block");
        for (var top = 0; top + 8 <= height; top += 4)
            for (var left = 0; left + 8 <= width; left += 4)
            {
                var solid = true;
                for (var row = top; row < top + 8 && solid; row++)
                    for (var column = left; column < left + 8; column++)
                        if (pixels[(row * width + column) * 4 + 3] < 240) { solid = false; break; }
                Data.Check(!solid, "thin line produced a solid 8x8 pixel block");
            }
    }

    public static void ActorResize()
    {
        RootAndHeldFishResize();
        LoopWrap();
        GroundbaitDropResize();
        using (var rigDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData", "source-rig.json"))))
        foreach (var pet in new[] { "orange", "dafeiyu" })
        foreach (var name in new[] { "cast", "reel", "reel_empty" })
        {
            Load(pet);
            using (var actor = new OverlayPetActor("motion-resize-" + pet + name, null))
            {
                actor.Apply("cat", name, name == "cast" ? "casting" : "resolving", 0, 0, petId: pet);
                var timer = Field<OverlayAnimationSchedule.Subscription>(actor, "_frameTimer"); timer.Stop();
                var clip = Field<FishingClipPlayback>(actor, "_layered");
                var sampleIndex = Array.FindIndex(clip.Motion, sample => sample.Line != null);
                Data.Check(sampleIndex >= 0, "clip has no intermediate line");
                var sample = clip.Motion[sampleIndex];
                typeof(OverlayPetActor).GetField("_frameIndex", PrivateInstance).SetValue(actor, sampleIndex);
                Invoke(actor, "ShowLayered");
                DrawingImage previousLine = null;
                foreach (var size in new[] { 64d, 128, 192, 96 })
                {
                    actor.ApplyChrome(new OverlayActorChrome { Pet = new OverlayLayoutObjectDto { w = size, h = size } });
                    Wait(() => !actor.FramePending);
                    var layers = Field<Image[]>(actor, "_layerImages");
                    var line = layers[Array.IndexOf(OverlayFishingLayers.LayerNames, "line")];
                    var rod = layers[Array.IndexOf(OverlayFishingLayers.LayerNames, "rod")];
                    Data.Check(line.Source is DrawingImage && (previousLine == null || ReferenceEquals(previousLine, line.Source)),
                        "resizing replaced the cached line with a bitmap or a new drawing");
                    previousLine = (DrawingImage)line.Source;
                    var scale = Field<int>(actor, "_layerDrawH") / 512d;
                    var horizontalScale = Field<int>(actor, "_layerDrawW") / 640d;
                    var rig = rigDocument.RootElement.GetProperty("clips").GetProperty(name)[sample.Source];
                    var grip = new Point(rig.GetProperty("grip")[0].GetDouble() * horizontalScale, rig.GetProperty("grip")[1].GetDouble() * scale);
                    var actualGrip = rod.RenderTransform.Transform(grip);
                    Data.Check((actualGrip - grip).Length < .25, "resize moved the rod grip by more than a quarter pixel");
                    var tip = new Point(rig.GetProperty("tip")[0].GetDouble() * horizontalScale, rig.GetProperty("tip")[1].GetDouble() * scale);
                    var lineStart = line.RenderTransform.Transform(new Point(sample.Line[0] * horizontalScale, sample.Line[1] * scale));
                    Data.Check((rod.RenderTransform.Transform(tip) - lineStart).Length < .25, "resize separated the line from the rod");
                    foreach (var body in new[] { "body", "head", "far_hand", "hands" })
                        Data.Check(layers[Array.IndexOf(OverlayFishingLayers.LayerNames, body)].RenderTransform.Value.IsIdentity,
                            "resize transformed a body layer");
                    AssertThinDrawing(line.Source, line.RenderTransform, scale);
                    Data.Check(Field<int>(actor, "_frameIndex") == sampleIndex && !timer.IsEnabled, "resize restarted playback during the fixed-frame test");
                }
            }
        }
    }

    public static void SleepingRest()
    {
        foreach (var pet in Data.Pets)
        {
            Load(pet);
            var sleepHead = OverlayFishingLayers.SleepHead(pet);
            Data.Check(sleepHead != null && File.Exists(sleepHead), "missing closed-eye face: " + pet);
            using (var actor = new OverlayPetActor("sleep-test-" + pet, null))
            {
                actor.Apply("cat", "fishing", "waiting", 0, 0, petId: pet, fishingAsleep: true);
                Wait(() => !actor.FramePending);
                Field<OverlayAnimationSchedule.Subscription>(actor, "_frameTimer").Stop();
                var rest = Field<OverlayFishingRestMotion>(actor, "_restMotion");
                rest.Update(OverlayAnimationClock.Now - 1200, false, false);
                rest.Update(OverlayAnimationClock.Now - 1000, true, true);
                foreach (var size in new[] { 64d, 128, 192 })
                {
                    actor.ApplyChrome(new OverlayActorChrome { Pet = new OverlayLayoutObjectDto { w = size, h = size } });
                    Wait(() => !actor.FramePending);
                    var layers = Field<Image[]>(actor, "_layerImages");
                    var width = Field<int>(actor, "_layerDrawW");
                    var height = Field<int>(actor, "_layerDrawH");
                    var horizontal = width / 640d;
                    var vertical = height / 512d;
                    Data.Check(ReferenceEquals(Field<ImageSource[]>(actor,"_restSourceImages")[1], OverlayFishingLayers.BitmapFor(sleepHead, width, height)), "sleep face not published");
                    if (layers[1].Source is DrawingImage nodImage)
                    {
                        Data.Check(nodImage.IsFrozen, "nodding drawing is not frozen");
                        Data.Check(nodImage.Drawing.Bounds == new Rect(0,0,640,512), "nodding changes full-canvas sizing");
                    }
                    var foot = new Point(264 * horizontal, 450 * vertical);
                    foreach (var layer in new[] { 0, 1, 2, 6 })
                        Data.Check((layers[layer].RenderTransform.Transform(foot) - foot).Length < .001, "breathing foot drift");
                    var tip = new Point(541.5 * horizontal, 149 * vertical);
                    Data.Check((layers[3].RenderTransform.Transform(tip) - layers[4].RenderTransform.Transform(tip)).Length < .25,
                        "sleeping rod line disconnected at authored key");
                    Data.Check(Field<TextBlock>(actor, "_sleepLabel").Visibility == Visibility.Visible, "sleep label absent");
                }
                actor.Apply("cat", "fishing", "waiting", 0, 0, petId: pet, fishingAsleep: false);
                Wait(() => !actor.FramePending);
                var awake = OverlayFishingLayers.LayerFile(pet, "fishing", 0, "head");
                Data.Check(ReferenceEquals(Field<ImageSource[]>(actor, "_restSourceImages")[1],
                    OverlayFishingLayers.BitmapFor(awake, Field<int>(actor, "_layerDrawW"), Field<int>(actor, "_layerDrawH"))), "wake retained closed eyes");
                actor.Apply("cat", "sit", "seated", 0, 0, petId: pet);
                Wait(() => !actor.FramePending);
                Data.Check(Field<TextBlock>(actor, "_sleepLabel").Visibility == Visibility.Collapsed, "sleep label leaked after stop");
            }
        }
    }

    static Transform RootTransform(FishingMotionSample sample, double horizontalScale, double verticalScale)
    {
        if (sample.Root == null) return Transform.Identity;
        var root = sample.Root;
        return new MatrixTransform(root[0], root[1] * verticalScale / horizontalScale,
            root[2] * horizontalScale / verticalScale, root[3], root[4] * horizontalScale, root[5] * verticalScale);
    }

    static void FreezeSample(OverlayPetActor actor, int index)
    {
        var timer = Field<OverlayAnimationSchedule.Subscription>(actor, "_frameTimer");
        timer.Suspended = true;
        timer.Stop();
        typeof(OverlayPetActor).GetField("_frameIndex", PrivateInstance).SetValue(actor, index);
        Invoke(actor, "ShowLayered");
    }

    static void SameTransform(Transform actual, Transform expected, double horizontalScale, double verticalScale, string message)
    {
        foreach (var point in new[] { new Point(264 * horizontalScale, 450 * verticalScale),
            new Point(264 * horizontalScale, 300 * verticalScale), new Point(350 * horizontalScale, 360 * verticalScale) })
            Data.Check((actual.Transform(point) - expected.Transform(point)).Length < .00001, message);
    }

    static void RootAndHeldFishResize()
    {
        using (var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData", "source-rig.json"))))
        foreach (var pet in new[] { "orange", "dafeiyu" })
        foreach (var name in new[] { "sit", "leave", "bait", "catch", "reset_catch", "reset_empty" })
        {
            Load(pet);
            using (var actor = new OverlayPetActor("motion-root-" + pet + name, null))
            {
                actor.Apply("cat", name, "seated", 0, 0, petId: pet);
                var clip = Field<FishingClipPlayback>(actor, "_layered");
                var rig = document.RootElement.GetProperty("clips").GetProperty(name);
                var moving = Array.FindIndex(clip.Motion, sample => sample.Root != null && !Data.Identity(sample.Root));
                Data.Check(moving >= 0, "root test found no actual motion: " + name);
                var samples = new List<int> { moving };
                var held = Array.FindIndex(clip.Motion, sample => sample.Position > sample.Source &&
                    rig[sample.Source].TryGetProperty("fishHeld", out var value) && value.GetBoolean());
                if (held >= 0 && held != moving) samples.Add(held);
                samples.Add(0);
                foreach (var index in samples)
                foreach (var size in new[] { 64d, 128, 192, 96 })
                {
                    FreezeSample(actor, index);
                    actor.ApplyChrome(new OverlayActorChrome { Pet = new OverlayLayoutObjectDto { w = size, h = size } });
                    Wait(() => !actor.FramePending);
                    var images = Field<Image[]>(actor, "_layerImages");
                    var horizontalScale = Field<int>(actor, "_layerDrawW") / 640d;
                    var verticalScale = Field<int>(actor, "_layerDrawH") / 512d;
                    var root = RootTransform(clip.Motion[index], horizontalScale, verticalScale);
                    var foot = new Point(264 * horizontalScale, 450 * verticalScale);
                    foreach (var layer in new[] { "body", "head", "far_hand", "hands" })
                    {
                        var image = images[Array.IndexOf(OverlayFishingLayers.LayerNames, layer)];
                        SameTransform(image.RenderTransform, root, horizontalScale, verticalScale, "body layers split or lost root motion: " + name);
                        Data.Check((image.RenderTransform.Transform(foot) - foot).Length < .00001, "resize moved the root foot pivot");
                    }
                    if (rig[clip.SourceIndex(index)].TryGetProperty("fishHeld", out var fishHeld) && fishHeld.GetBoolean())
                    {
                        var fish = images[Array.IndexOf(OverlayFishingLayers.LayerNames, "fish")];
                        SameTransform(fish.RenderTransform, root, horizontalScale, verticalScale, "held fish separated from the root and hands");
                    }
                    Data.Check(Field<int>(actor, "_frameIndex") == index, "frozen root pose advanced during resize");
                }
            }
        }
    }

    static void LoopWrap()
    {
        Load("orange");
        foreach (var name in new[] { "fishing", "effort", "hooked" })
        using (var actor = new OverlayPetActor("motion-loop-" + name, null))
        {
            var phase = name == "hooked" ? "hooked" : "waiting";
            actor.Apply("cat", name, phase, 0, 0, petId: "orange");
            var clip = Field<FishingClipPlayback>(actor, "_layered");
            var source = Data.Original("orange", name);
            Data.Check(clip.Playback == "intro-loop" && Field<OverlayAnimationSchedule.Subscription>(actor, "_frameTimer").IsEnabled,
                "loop clip remained a static hold: " + name);
            FreezeSample(actor, clip.FrameCount - 1);
            Invoke(actor, "AdvanceLayered");
            Data.Check(Field<int>(actor, "_frameIndex") == clip.LoopStart && clip.SourceIndex(clip.LoopStart) == source.LoopStart,
                "actor did not wrap to the authored source loop start");
            actor.Apply("cat", name, phase, 1000, 0, petId: "orange");
            Data.Check(Field<int>(actor, "_frameIndex") == clip.LoopStart, "network refresh restarted the loop intro");
        }
    }

    static void GroundbaitDropResize()
    {
        Load("orange");
        using (var actor = new OverlayPetActor("motion-groundbait-depth", null))
        {
            actor.Apply("cat", "groundbait", "groundbaiting", 0, 0, petId: "orange");
            var clip = Field<FishingClipPlayback>(actor, "_layered");
            var falling = Array.FindIndex(clip.Motion, sample => sample.Position > 4 && sample.Position < 6);
            Data.Check(falling >= 0, "groundbait lacks airborne intermediate samples");
            foreach (var index in new[] { clip.IndexAtSource(2), falling, clip.IndexAtSource(6) })
            foreach (var size in new[] { 64d, 128, 192 })
            {
                FreezeSample(actor, index);
                actor.ApplyChrome(new OverlayActorChrome { Pet = new OverlayLayoutObjectDto { w = size, h = size } });
                Wait(() => !actor.FramePending);
                var images = Field<Image[]>(actor, "_layerImages");
                var image = images[Array.IndexOf(OverlayFishingLayers.LayerNames, "bait")];
                var scale = Field<int>(actor, "_layerDrawH") / 512d;
                var local = clip.Motion[index].Transforms[8];
                var actualDrop = image.RenderTransform.Transform(new Point(0, 0)).Y - local[5] * scale;
                var expectedDrop = OverlayFishingWaterContact.GroundbaitOffset(clip.SourcePosition(index), scale, image.Margin.Top, size + 6);
                Data.Check(Math.Abs(actualDrop - expectedDrop) < .00001 && (index != falling || actualDrop > 0),
                    "actor drop ignored source position, surface height or resize");
            }
        }
    }
}
#endif
