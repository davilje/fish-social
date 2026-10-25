using System;
using FishSocialOverlay;

static class OverlaySeatMotionTests
{
    static int passed;
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    static void Run(string name, Action test) { test(); passed++; Console.WriteLine("PASS " + name); }
    public static void Main()
    {
        Run("small camera shows more bank while standard and medium keep their framing", () =>
        {
            Check(OverlaySceneGeometry.CropTop(560,300)==236,"small camera did not rise 24 units");
            Check(OverlaySceneGeometry.CropTop(560,400)==160,"medium camera unexpectedly changed");
            Check(OverlaySceneGeometry.CropTop(560,560)==0,"standard camera unexpectedly changed");
            Check(OverlaySceneGeometry.CropTop(200,300)==0,"camera exposed negative scene coordinates");
            var waterScreenY=459.337-OverlaySceneGeometry.CropTop(560,300);
            Check(waterScreenY>200 && waterScreenY<240,"water contact is hidden by small-window bottom controls");
        });
        Run("actor ordering follows ground depth and horizontal position, not entry order", () =>
        {
            var left=OverlaySceneGeometry.ActorOrder(402,100);
            var right=OverlaySceneGeometry.ActorOrder(402,180);
            var arriving=OverlaySceneGeometry.ActorOrder(379,180);
            Check(left<right && arriving<left,"neighbour/arrival layering reversed");
            Check(OverlaySceneGeometry.ActorOrder(402.001,100)==left,"export rounding changes row ordering");
            Check(OverlaySceneGeometry.ActorOrder(403,0)>right,"front row fails to cover back row");
            Check(OverlaySceneGeometry.ActorOrder(double.NaN,100)==0,"invalid depth reaches ZIndex");
        });
        Run("seat release cannot move before stand completion", () =>
        {
            var motion = new OverlaySeatMotion(); motion.Snap(84.625,388);
            motion.WaitForStand(200,292);
            Check(!motion.Advance(60000) && motion.X==84.625 && motion.Y==388, "stand moved off chair");
            motion.Begin(60000); motion.Advance(60000);
            Check(motion.X==84.625 && motion.Y==388 && !motion.WaitingForStand, "departure jumped");
        });
        Run("arrival is continuous monotonic and exact", () =>
        {
            var motion = new OverlaySeatMotion(); motion.Snap(0,0); motion.Retarget(300,150); motion.Begin(100);
            double previous=0;
            for(var now=100;now<750;now+=10)
            { Check(!motion.Advance(now), "early arrival"); Check(motion.X>=previous && motion.X<300,"overshoot"); previous=motion.X; }
            Check(motion.Advance(750) && motion.X==300 && motion.Y==150, "arrival not exact");
            Check(!motion.Advance(1000),"arrival callback repeated");
        });
        Run("repeated snapshots cannot restart transit", () =>
        {
            var motion=new OverlaySeatMotion(); motion.Snap(0,0); motion.Retarget(100,0); motion.Begin(0);
            for(var now=0;now<200;now+=20) { motion.Retarget(100,0); motion.Advance(now); }
            Check(motion.Advance(200) && motion.X==100,"snapshots delayed arrival");
        });
        Run("new seat cancels stand hold", () =>
        {
            var motion=new OverlaySeatMotion(); motion.Snap(100,300); motion.WaitForStand(500,200);
            motion.Retarget(200,300); motion.Begin(20);
            Check(!motion.WaitingForStand,"old leave survived"); motion.Advance(1000);
            Check(motion.X==200 && motion.Y==300,"old waiting point won");
        });
        Run("redirect starts at current rendered feet", () =>
        {
            var motion=new OverlaySeatMotion(); motion.Snap(0,0); motion.Retarget(300,0); motion.Begin(0); motion.Advance(100);
            var x=motion.X; motion.Retarget(500,0); motion.Begin(100); motion.Advance(100);
            Check(motion.X==x,"redirect snapped to old origin"); motion.Advance(1000); Check(motion.X==500,"redirect lost");
        });
        Run("scene reset discards queued departure", () =>
        {
            var motion=new OverlaySeatMotion(); motion.Snap(100,300); motion.WaitForStand(500,200); motion.Snap(20,40);
            Check(!motion.Advance(60000) && motion.X==20 && motion.Y==40 && !motion.WaitingForStand,"old scene moved new actor");
        });
        Run("waiting lane reindex preserves rendered feet", () =>
        {
            var motion=new OverlaySeatMotion(); motion.Snap(0,0); motion.Retarget(300,0); motion.Begin(0); motion.Advance(100);
            var x=motion.X; motion.Retarget(500,100); motion.Advance(100);
            Check(motion.X==x && motion.Y==0,"waiting lane update teleported actor");
            motion.Advance(1000); Check(motion.X==500 && motion.Y==100,"new lane target lost");
        });
        Run("same point and hidden-time catchup terminate", () =>
        {
            var motion=new OverlaySeatMotion(); motion.Snap(20,40); motion.Begin(0); Check(!motion.Moving,"zero travel kept clock awake");
            motion.Retarget(50,60); motion.Begin(10); Check(motion.Advance(60000) && !motion.Moving,"hidden transit never settled");
        });
        Console.WriteLine("PASS " + passed + "/" + passed + " seat motion groups");
    }
}
