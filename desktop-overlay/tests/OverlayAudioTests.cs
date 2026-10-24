using System;
using System.Collections.Generic;
using FishSocialOverlay;

static class OverlayAudioTests
{
    static int passed;
    static void Check(bool value, string reason) { if (!value) throw new Exception(reason); }
    static void Run(string name, Action action) { action(); passed++; Console.WriteLine("PASS " + name); }
    public static void Main()
    {
        Run("audio identity survives actual IPC serialization", RoundTrip);
        Run("old protocol, owner and missing actor are silent", InvalidCapabilities);
        Run("snapshot baseline never replays after authoritative refresh", Snapshot);
        Run("baseline bite does not swallow new settlement of the same attempt", BaselineBiteThenCatch);
        Run("same result once, different results without cue cooldown", Results);
        Run("late cold decode is valid only for current revision and generation", RevisionFence);
        Run("cancel, skin change and replacement instances reject old completion", CancelAndReplace);
        Run("hidden actions do not replay on restore", Hide);
        Run("expired bite and predicted result are silent", Authority);
        Run("current action must own its actual source clip", ClipOwnership);
        Run("bounded long-loop ledger", Bounded);
        Run("source windows do not catch up a skipped cast marker", SourceWindow);
        Run("float contact and missing water are validated across slot sizes", FloatContact);
        Run("current submerged fish never fabricate surface or slap", SubmergedFish);
        Run("groundbait contact follows interpolated actual geometry", Groundbait);
        Run("groundbait occurrence follows real source loop and skips history", GroundbaitLoops);
        Run("semantic UI whitelist and navigation ownership", Ui);
        Run("visible tier matches shared action identity, not unlike event IDs or hidden future size", VisibleTier);
        Run("cat gesture distinguishes click, drag out and back, cancel and long press", CatGesture);
        Console.WriteLine("PASS " + passed + "/" + passed + " portable audio marker/policy tests");
    }

    static OverlayAudioPresentation Context(string eventId = "bite-1", string kind = "bite", string action = "session:command:1", string source = "authoritative")
        => new OverlayAudioPresentation { ProtocolVersion = 2, PresentationSessionId = "session", ActorId = "self",
            PondId = "pond", SpotId = "spot", OwnerEpoch = 1, Owner = "overlay", Enabled = true,
            ActionId = action, EventId = eventId, EventKind = kind, Source = source, WaterSizeTier = "M", EventDeadlineMs = 10000 };
    static void VisibleTier()
    {
        var audio=Context(); audio.WaterSizeTier="XL";
        var water=new FishingPresentationEnvelope { PresentationSessionId=audio.PresentationSessionId,ActorId=audio.ActorId,
            PondId=audio.PondId,SpotId=audio.SpotId,ActionId=audio.ActionId,OwnerEpoch=audio.OwnerEpoch,Owner="overlay",
            EventId="different-H-phase-uuid",WaterSizeTier="L" };
        Check(OverlayAudioMarkerPolicy.VisibleTier(audio,water,"hooked")=="L","unlike event IDs prevented same visible action tier");
        Check(OverlayAudioMarkerPolicy.VisibleTier(audio,water,"waiting")=="S","future hidden size leaked into waiting sound");
        water.ActionId="previous-action";
        Check(OverlayAudioMarkerPolicy.VisibleTier(audio,water,"hooked")=="S","stale visible action supplied current size");
        water.ActionId=audio.ActionId; water.OwnerEpoch++;
        Check(OverlayAudioMarkerPolicy.VisibleTier(audio,water,"hooked")=="S","old owner supplied size");
        water.OwnerEpoch=audio.OwnerEpoch;water.WaterSizeTier="invalid";
        Check(OverlayAudioMarkerPolicy.VisibleTier(audio,water,"effort")=="S","unknown visible tier not conservative");
    }
    static OverlayAudioMarkerPolicy Policy(OverlayAudioPresentation c = null, string phase = "hooked")
    { var p = new OverlayAudioMarkerPolicy(); p.Update(c ?? Context(), phase, "orange"); return p; }
    static OverlayAudioMarker Emit(OverlayAudioMarkerPolicy p, string marker = "bite.onset", long occurrence = 0, long now = 1000)
        => p.Consume(marker, occurrence, p.Revision, p.Generation, p.ActionId, true, now);

    static void RoundTrip()
    {
        var message = new IpcMessage { Type = "state", AudioPresentation = Context(), ActorGeneration = 42,
            Marker = "bite.onset", OwnerEpoch = 9, ElapsedMs = 17, ProtocolVersion = 2 };
        var parsed = IpcMessage.Parse(message.ToJson());
        Check(parsed.AudioPresentation.EventId == "bite-1" && parsed.AudioPresentation.ProtocolVersion == 2 &&
            parsed.ActorGeneration == 42 && parsed.Marker == "bite.onset" && parsed.OwnerEpoch == 9 && parsed.ElapsedMs == 17,
            "nested capability or flat marker lost in real serializer");
    }
    static void InvalidCapabilities()
    {
        foreach (var change in new Action<OverlayAudioPresentation>[] {
            c=>c.ProtocolVersion=1,c=>c.ActorId=null,c=>c.Owner="unity",c=>c.Enabled=false,c=>c.SpotId=null,c=>c.ActionId=null })
        {
            var c = Context(); change(c); var p = Policy(c);
            Check(Emit(p, "cast.release") == null, "invalid action capability accepted");
            if (c.ActionId != null) Check(Emit(p) == null, "invalid event capability accepted");
        }
    }
    static void Snapshot()
    {
        var p = Policy(Context(source:"snapshot")); Check(Emit(p) == null, "snapshot sounded");
        p.Update(Context(), "hooked", "orange"); Check(Emit(p) == null, "snapshot replayed after refresh");
        p.Update(Context("bite-new"), "hooked", "orange"); Check(Emit(p) != null, "new authoritative bite was swallowed");
    }
    static void Results()
    {
        var p = Policy(Context("catch-a", "catch"), "resolving");
        Check(Emit(p, "result.presented") != null, "first catch lost");
        Check(Emit(p, "result.presented") == null, "duplicate catch replayed");
        p.Update(Context("catch-b", "catch"), "resolving", "orange");
        Check(Emit(p, "result.presented") != null, "different close catch lost");
    }
    static void BaselineBiteThenCatch()
    {
        var p=Policy(Context("attempt-a","bite",source:"snapshot"));
        p.Update(Context("attempt-a","bite"),"hooked","orange");Check(Emit(p)==null,"baseline bite replayed");
        p.Update(Context("attempt-a","catch"),"resolving","orange");
        Check(Emit(p,"result.presented")!=null,"new catch of baseline attempt was swallowed");
        Check(Emit(p,"result.presented")==null,"fresh catch duplicate replayed");
    }
    static void RevisionFence()
    {
        foreach (var decodeMs in new[] { 0, 150, 400 })
        {
            var p = Policy(); var revision = p.Revision; var gen = p.Generation;
            Check(p.ConsumedCount == 0, "load consumed an unpublished marker");
            Check(p.Consume("bite.onset",0,revision,gen,p.ActionId,true,1000+decodeMs)!=null,"valid decoded frame lost");
            p = Policy(); revision = p.Revision; gen = p.Generation;
            p.Update(Context(),"seated","orange");
            Check(p.Consume("bite.onset",0,revision,gen,p.ActionId,true,1000+decodeMs)==null,"late phase completion sounded");
        }
    }
    static void CancelAndReplace()
    {
        var p = Policy(); var revision = p.Revision; var gen = p.Generation; p.Cancel();
        Check(p.Consume("bite.onset",0,revision,gen,p.ActionId,true,1000)==null && Emit(p)==null,"cancel did not retire current work");
        p.Update(Context("bite-new",action:"session:command:2"),"hooked","calico");
        Check(p.Generation > gen && p.Consume("bite.onset",0,revision,gen,p.ActionId,true,1000)==null,"skin accepted old frame");
        Check(Emit(p)!=null,"new action after cancel did not sound");
        var replacement=Policy(); Check(replacement.Generation>p.Generation,"replacement actor reused generation domain");
    }
    static void Hide()
    {
        var p=Policy();p.SetVisible(false);Check(Emit(p)==null,"hidden actor sounded");
        p.SetVisible(true);Check(Emit(p)==null,"restore replayed hidden event");
        p.Update(Context("bite-new",action:"session:command:2"),"hooked","orange");Check(Emit(p)!=null,"new visible bite lost");
    }
    static void Authority()
    {
        Check(Emit(Policy(Context(source:"prediction")))==null,"predicted bite sounded");
        Check(Emit(Policy(),now:10000)==null,"expired bite sounded");
        var p=Policy(Context(kind:"escape"),"stopping");
        Check(p.Consume("escape.contact",0,p.Revision,p.Generation,p.ActionId,false,1000)==null,"escape invented missing water contact");
        Check(Emit(p,"result.presented")==null,"escape became successful catch");
    }
    static void ClipOwnership()
    {
        var p=Policy(Context(source:"prediction"),"casting");
        Check(p.Consume("cast.release",0,p.Revision,p.Generation,"previous-action",true,1000)==null,"old clip bound to new action");
        Check(Emit(p,"cast.release")!=null,"current predicted cast lost");
        p.Update(Context(),"waiting","orange");Check(Emit(p,"cast.release")==null,"finished phase replayed wind");
    }
    static void Bounded()
    {
        var p=Policy(Context(kind:"",eventId:""),"groundbaiting");
        for(var i=0;i<10000;i++)Check(Emit(p,"groundbait.contact",i)!=null,"fresh loop lost");
        Check(p.ConsumedCount<=512,"unbounded consumed keys");
        Check(Emit(p,"groundbait.contact",0)==null,"current-action old occurrence replayed after ledger rotation");
    }
    static void SourceWindow()
    {
        var release=Array.Find(OverlayAudioSourceMarkers.Metadata.Markers,m=>m.Marker=="cast.release");
        Check(release.SourceIndex==2&&release.SourceMs==280,"release source contract drift");
        Check(OverlayAudioSourceMarkers.InWindow(release,"cast",2.75),"interpolated source window lost");
        Check(!OverlayAudioSourceMarkers.InWindow(release,"cast",3)&&!OverlayAudioSourceMarkers.InWindow(release,"fishing",2),"skipped marker replayed");
    }
    static void FloatContact()
    {
        var frame=OverlayFishingWaterContact.Frame("cast",3);
        foreach(var scale in new[]{.125,.25,.5})
        {
            var origin=-20d;var surface=500*scale;
            var pose=OverlayFishingWaterContact.Solve(frame,scale,origin,surface,1);
            Check(OverlayAudioSourceMarkers.Contact("bobber","cast",3,null,frame,pose,scale,origin,surface),"published float missed water");
            Check(!OverlayAudioSourceMarkers.Contact("fish","cast",3,null,frame,pose,scale,origin,surface),"empty fish claimed contact");
            Check(!OverlayAudioSourceMarkers.Contact("bobber","cast",3,null,null,pose,scale,origin,surface),"missing solver became contact");
        }
    }
    static void SubmergedFish()
    {
        foreach(var clip in new[]{"hooked","effort"})
        for(var source=0;source<(clip=="hooked"?10:6);source++)
        foreach(var scale in new[]{.125,.25,.5})
        {
            var frame=OverlayFishingWaterContact.Frame(clip,source);var surface=600*scale;
            var pose=OverlayFishingWaterContact.Solve(frame,scale,0,surface,1);
            Check(!OverlayAudioSourceMarkers.Contact("fish",clip,source,null,frame,pose,scale,0,surface),"submerged fish fabricated a surface marker");
            Check(OverlayAudioSourceMarkers.Contact("line",clip,source,null,frame,pose,scale,0,surface),"valid bite lost its water-line connection");
        }
    }
    static void Groundbait()
    {
        var motion=OverlayFishingMotion.Find("groundbait",new[]{100,120,120,120,120,120,120,160});
        Check(motion!=null,"real interpolated motion absent");var touched=false;var above=false;
        foreach(var sample in motion.Frames)
        {
            if(sample.Source!=5)continue;
            var scale=.25;var surface=150d;var origin=-20d;
            var pose=new FishingContactPose{LineScale=1,BaitOffset=OverlayFishingWaterContact.GroundbaitOffset(sample.Position,scale,origin,surface)};
            var contact=OverlayAudioSourceMarkers.Contact("bait","groundbait",sample.Source,sample,sample.Contact,pose,scale,origin,surface);
            touched|=contact;above|=!contact;
        }
        Check(touched&&above,"groundbait emitted by source number alone instead of crossing actual water geometry");
    }
    static void GroundbaitLoops()
    {
        var orders=new List<string[]>();for(var i=0;i<8;i++)orders.Add(Array.Empty<string>());
        var clip=new FishingClipPlayback("intro-loop",1,orders,new[]{100,120,120,120,120,120,120,160},null);
        Check(OverlayAudioSourceMarkers.GroundbaitOccurrence(clip,600)==0,"first loop shifted");
        Check(OverlayAudioSourceMarkers.GroundbaitOccurrence(clip,100+720*20+600)==20,"low cadence replayed skipped loops");
    }
    static void Ui()
    {
        Check(!OverlayUiAudioPolicy.Allowed("cast")&&!OverlayUiAudioPolicy.Allowed("../../sound.wav"),"raw IPC sound/path allowed");
        Check(OverlayUiAudioPolicy.Allowed("manual_meow")&&OverlayUiAudioPolicy.IsNavigation("MenuShop_OnClick"),"semantic route lost");
        Check(OverlayUiAudioPolicy.LocalCue("ChatDockToggle",null,false)=="ui_open"&&
            OverlayUiAudioPolicy.LocalCue("ChatDockToggle",null,true)=="ui_close","toggle open-close inverted");
    }
    static void CatGesture()
    {
        var p=new OverlayPetClickGesture();p.Begin(10,10,0);Check(p.Complete(10,10,100,4,4,true),"click lost");
        p.Begin(10,10,0);p.WindowMoved();Check(!p.Complete(10,10,100,4,4,true),"drag out/back meowed");
        p.Begin(10,10,0);p.Cancel();Check(!p.Complete(10,10,100,4,4,true),"cancel meowed");
        p.Begin(10,10,0);Check(!p.Complete(10,10,1000,4,4,true),"long press meowed");
        p.Begin(10,10,0);Check(!p.Complete(10,10,100,4,4,false),"unreleased drag meowed");
    }
}
