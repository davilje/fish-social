#if OVERLAY_MOTION_TESTS
using System;
using System.Text.Json.Nodes;
using FishSocialOverlay;
using Data = OverlayFishingMotionTestData;

public static class OverlayFishingMotionTests
{
    static int _passed;
    public static void Main()
    {
        Run("ten pets preserve source poses and durations in four expanded clips", AllPets);
        Run("body layers stay fixed and rod grips and line tips stay connected", Rig);
        Run("malformed JSON and unsupported documents fall back", BadJson);
        Run("missing required motion fields fall back", MissingFields);
        Run("NaN and infinity cannot reach drawing transforms", NonFinite);
        Run("invalid source indices and positions fall back", InvalidMapping);
        Run("scaled, reflected, sheared or misplaced rigid layers fall back", InvalidTransforms);
        Run("missing source sequence and missing first sample fields fall back", MissingSequence);
        Run("duration mismatches preserve original playback", InvalidDurations);
        Run("motion application is idempotent and preserves loop mapping", Idempotence);
        Run("water contact stays finite across sample positions and sizes", WaterContact);
        Run("real groundbait samples retain 720ms loops and 280ms hand-return outro", GroundbaitSegments);
        Console.WriteLine("PASS: " + _passed + "/" + _passed + " motion core tests");
    }
    static void Run(string name, Action test) { test(); _passed++; Console.WriteLine("PASS " + name); }
    static void AllPets()
    {
        foreach (var pet in Data.Pets)
            for (var index = 0; index < Data.Clips.Length; index++)
            {
                var original = Data.Original(pet, Data.Clips[index]);
                var expanded = Data.Original(pet, Data.Clips[index]);
                expanded.ApplyMotion(OverlayFishingMotion.Find(Data.Clips[index], expanded.DurationsMs));
                Data.CheckExpansion(original, expanded, Data.ExpandedCounts[index]);
            }
    }
    static void Rig()
    {
        foreach (var name in Data.Clips)
        {
            var clip = Data.Original("orange", name); clip.ApplyMotion(Data.Motion()[name]);
            Data.CheckRig(name, clip);
        }
    }
    static void Fallback(FishingMotionClip motion)
    {
        var clip = Data.Original("orange", "cast");
        var orders = clip.Orders; var durations = clip.DurationsMs; var next = clip.NextClip;
        clip.ApplyMotion(motion);
        Data.Check(clip.Motion == null && ReferenceEquals(clip.Orders, orders) && ReferenceEquals(clip.DurationsMs, durations)
            && clip.NextClip == next && clip.FrameCount == 4, "invalid motion partially mutated original clip");
        for (var index = 0; index < clip.FrameCount; index++)
            Data.Check(clip.SourceIndex(index) == index && clip.SourcePosition(index) == index && clip.IndexAtSource(index) == index,
                "fallback left virtual frame indices");
    }
    static void BadJson()
    {
        foreach (var json in new[] { "", "{", "[]", "null", "{}", "{\"Version\":2,\"Clips\":{}}", "{\"Version\":1,\"Clips\":[]}",
            "{\"Version\":1,\"Clips\":{\"cast\":{\"Frames\":[{\"Position\":NaN}]}}}",
            "{\"Version\":1,\"Clips\":{\"cast\":{\"Frames\":[{\"Position\":\"NaN\"}]}}}" })
        {
            var parsed = OverlayFishingMotion.Parse(json);
            Data.Check(parsed.Count == 0, "invalid document accepted: " + json);
            Fallback(null);
        }
    }
    static void MissingFields()
    {
        foreach (var field in new[] { "DurationsMs", "Frames", "Transforms", "Contact", "Source", "Position", "DurationMs" })
        {
            var json = JsonNode.Parse(Data.MotionJson);
            var clip = json["Clips"]["cast"].AsObject();
            if (field == "DurationsMs" || field == "Frames") clip.Remove(field);
            else
            {
                var index = 1;
                if (field == "Source")
                    while (clip["Frames"][index]["Source"].GetValue<int>() == 0) index++;
                clip["Frames"][index].AsObject().Remove(field);
            }
            Fallback(OverlayFishingMotion.Parse(json.ToJsonString())["cast"]);
        }
    }
    static void NonFinite()
    {
        foreach (var value in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            foreach (var target in new[] { "position", "matrix", "contact", "line" })
            {
                var clip = Data.Motion()["cast"]; var frame = clip.Frames[1];
                if (target == "position") frame.Position = value;
                if (target == "matrix") frame.Transforms[3][0] = value;
                if (target == "contact") frame.Contact.LineTop = value;
                if (target == "line") frame.Line = new[] { value, 0d, 0d, 0d, 0d, 0d };
                Fallback(clip);
            }
    }
    static void InvalidMapping()
    {
        foreach (var change in new Action<FishingMotionClip>[] {
            clip => clip.Frames[1].Source = -1,
            clip => clip.Frames[1].Source = 99,
            clip => clip.Frames[1].Position = clip.Frames[0].Position,
            clip => clip.Frames[1].Position = 1.1,
            clip => clip.Frames[0].Position = .1,
            clip => clip.Frames[1].Transforms = new double[8][],
            clip => clip.Frames[1].Transforms[0] = new double[5],
            clip => clip.Frames[1].Line = new double[5],
            clip => clip.Frames[1] = null })
        { var clip = Data.Motion()["cast"]; change(clip); Fallback(clip); }
    }
    static void InvalidDurations()
    {
        foreach (var change in new Action<FishingMotionClip>[] {
            clip => clip.DurationsMs[0]++,
            clip => clip.Frames[1].DurationMs++,
            clip => clip.Frames[1].DurationMs = 0,
            clip => clip.Frames[1].DurationMs = 60001,
            clip => clip.DurationsMs = new[] { 100 } })
        { var clip = Data.Motion()["cast"]; change(clip); Fallback(clip); }
    }
    static void InvalidTransforms()
    {
        foreach (var change in new Action<FishingMotionClip>[] {
            clip => clip.Frames[1].Transforms[3] = new double[] { 2, 0, 0, .5, 0, 0 },
            clip => clip.Frames[1].Transforms[5] = new double[] { 2, 0, 0, 2, 0, 0 },
            clip => clip.Frames[1].Transforms[3] = new double[] { -1, 0, 0, 1, 0, 0 },
            clip => clip.Frames[1].Transforms[3] = new double[] { 1, 0, .1, 1, 0, 0 },
            clip => clip.Frames[1].Transforms[7] = new double[] { 0, 1, -1, 0, 0, 0 },
            clip => clip.Frames[1].Transforms[8] = new double[] { 0, 1, -1, 0, 0, 0 },
            clip => clip.Frames[1].Transforms[4][4] = 10,
            clip => clip.Frames[0].Transforms[3] = new double[] { 0, 1, -1, 0, 0, 0 },
            clip => clip.Frames[0].Transforms[5][4] = 10 })
        { var clip = Data.Motion()["cast"]; change(clip); Fallback(clip); }
        foreach (var layer in new[] { 0, 1, 2, 6 })
        {
            var moved = Data.Motion()["cast"]; moved.Frames[1].Transforms[layer][4] = 1; Fallback(moved);
            var rotated = Data.Motion()["cast"]; rotated.Frames[1].Transforms[layer] = new double[] { 0, 1, -1, 0, 0, 0 }; Fallback(rotated);
        }
    }
    static void MissingSequence()
    {
        foreach (var property in new[] { "Source", "Position" })
        {
            var json = JsonNode.Parse(Data.MotionJson);
            json["Clips"]["cast"]["Frames"][0].AsObject().Remove(property);
            Fallback(OverlayFishingMotion.Parse(json.ToJsonString())["cast"]);
        }
        var skipped = Data.Motion()["cast"];
        skipped.Frames = Array.FindAll(skipped.Frames, frame => frame.Source != 1);
        Fallback(skipped);
        var truncated = Data.Motion()["cast"];
        truncated.Frames = Array.FindAll(truncated.Frames, frame => frame.Source != 3);
        Fallback(truncated);
    }
    static void Idempotence()
    {
        var source = Data.Original("orange", "groundbait");
        var clip = new FishingClipPlayback("intro-loop", 1, source.Orders, source.DurationsMs, "ready");
        var motion = Data.Motion()["groundbait"]; clip.ApplyMotion(motion);
        var durations = clip.DurationsMs; var orders = clip.Orders;
        clip.ApplyMotion(motion); clip.ApplyMotion(null);
        Data.Check(ReferenceEquals(durations, clip.DurationsMs) && ReferenceEquals(orders, clip.Orders), "motion expanded twice");
        Data.Check(clip.LoopStart == clip.IndexAtSource(1) && clip.SourcePosition(clip.LoopStart) == 1, "loop did not retain source semantics");
    }
    static void WaterContact()
    {
        foreach (var pair in Data.Motion())
            foreach (var sample in pair.Value.Frames)
                foreach (var scale in new[] { .125, .25, .5, 1d })
                {
                    var weight = OverlayFishingWaterContact.Weight(pair.Key, sample.Position, pair.Value.DurationsMs.Length);
                    var pose = OverlayFishingWaterContact.Solve(sample.Contact, scale, -10, 270, weight);
                    Data.Check(double.IsFinite(pose.LineScale) && pose.LineScale >= 1 && double.IsFinite(pose.BobberOffset),
                        "sample water contact became invalid");
                }
    }
    static void GroundbaitSegments()
    {
        var clip = Data.Original("orange", "groundbait"); clip.ApplyMotion(Data.Motion()["groundbait"]);
        var timeline = new OverlayGroundbaitTimeline(clip.DurationsMs, clip.IndexAtSource(1), clip.IndexAtSource(7), clip.IndexAtSource(6), 0);
        Data.Check(clip.SourceIndex(timeline.Sample(99).Index) == 0, "intro no longer lasts 100ms");
        Data.Check(clip.SourceIndex(timeline.Sample(100).Index) == 1 && clip.SourceIndex(timeline.Sample(820).Index) == 1,
            "expanded groundbait changed the 720ms loop");
        timeline.End(1000);
        Data.Check(clip.SourceIndex(timeline.Sample(1000).Index) == 6 && clip.SourceIndex(timeline.Sample(1119).Index) == 6,
            "outro did not preserve the 120ms hand return");
        Data.Check(clip.SourceIndex(timeline.Sample(1120).Index) == 7 && !timeline.Sample(1279).Complete && timeline.Sample(1280).Complete,
            "outro did not preserve the final 160ms pose");
    }
}
#endif
