using System;
using System.Collections.Generic;

namespace FishSocial.Desktop
{
    [Serializable]
    public sealed class DesktopWeatherConfiguration
    {
        public int seed=2201030;
        public double sampleIntervalSeconds=.05, blockSeconds=30, initialWindGain=.18, initialLeavesGain=.30;
        public double stepMinSeconds=2.4, stepMaxSeconds=4.8, windMinGain=.15, windMaxGain=.49;
        public double windAdjustmentThreshold=.045, windAdjustment=.08, windAdjustedMinimum=.13;
        public double firstLullMinSeconds=26, firstLullMaxSeconds=44, lullGain=.015;
        public double lullHoldMinSeconds=.8, lullHoldMaxSeconds=1.6, lullGapMinSeconds=28, lullGapMaxSeconds=46;
        public double leavesDelaySeconds=.28, leavesResponseGain=.73, leavesReferenceWindGain=.55, leavesResponseExponent=.85;
        public double leavesRiseSeconds=.65, leavesFallSeconds=1.35, fadeInSeconds=2, fadeOutSeconds=1.2;
        public double monitorGain=5, waterOffsetSeconds=10, windOffsetSeconds=3.2, leavesOffsetSeconds=19.7;
        public bool IsValid()
        {
            foreach(var field in typeof(DesktopWeatherConfiguration).GetFields())
                if(field.FieldType==typeof(double)){var value=(double)field.GetValue(this);if(double.IsNaN(value)||double.IsInfinity(value)||value<0)return false;}
            return sampleIntervalSeconds>=.01&&sampleIntervalSeconds<=.2&&blockSeconds>=10&&blockSeconds<=60&&
                Math.Abs(blockSeconds/sampleIntervalSeconds-Math.Round(blockSeconds/sampleIntervalSeconds))<.000001&&
                stepMinSeconds>=.5&&stepMaxSeconds>=stepMinSeconds&&stepMaxSeconds<=10&&
                initialWindGain<=1&&initialLeavesGain<=1&&windMinGain<=windMaxGain&&windMaxGain<=1&&windAdjustedMinimum<=1&&
                windAdjustment<=1&&windAdjustmentThreshold<=1&&lullGain<=1&&
                firstLullMinSeconds>=1&&firstLullMaxSeconds>=firstLullMinSeconds&&firstLullMaxSeconds<=120&&
                lullHoldMinSeconds>=.1&&lullHoldMaxSeconds>=lullHoldMinSeconds&&lullHoldMaxSeconds<=10&&
                lullGapMinSeconds>=1&&lullGapMaxSeconds>=lullGapMinSeconds&&lullGapMaxSeconds<=120&&
                leavesDelaySeconds<=.9&&leavesResponseGain<=1&&leavesReferenceWindGain>0&&leavesReferenceWindGain<=1&&
                leavesResponseExponent>0&&leavesResponseExponent<=4&&leavesRiseSeconds>=.05&&leavesFallSeconds>=.05&&
                leavesRiseSeconds<=10&&leavesFallSeconds<=10&&fadeInSeconds>=.1&&fadeInSeconds<=5&&fadeOutSeconds>=.1&&fadeOutSeconds<=5;
        }
    }
    public sealed class DesktopWeatherBlock
    {
        public readonly double start,duration,interval;
        public readonly double[] wind,leaves;
        public DesktopWeatherBlock(double start,double duration,double interval,double[] wind,double[] leaves)
        {this.start=start;this.duration=duration;this.interval=interval;this.wind=wind;this.leaves=leaves;}
        // WebAudio's linearRampToValueAtTime interpolates the same 50ms samples.
        public double Evaluate(double time,bool leaf)
        {
            var values=leaf?leaves:wind;var position=Math.Max(0,Math.Min(values.Length-1,(time-start)/interval));
            var index=(int)position;if(index>=values.Length-1)return values[values.Length-1];
            return values[index]+(values[index+1]-values[index])*(position-index);
        }
    }
    /// <summary>Exact double-precision port of leaf-clusters/flow.js; generate only on the main thread.</summary>
    public sealed class DesktopWeather
    {
        struct Point {public double time,gain;public Point(double time,double gain){this.time=time;this.gain=gain;}}
        readonly DesktopWeatherConfiguration config;
        readonly List<Point> points=new List<Point>();
        uint seed;
        double leaf,time,nextQuiet;
        public int RetainedWindPoints=>points.Count;
        public DesktopWeather(DesktopWeatherConfiguration config)
        {
            if(config==null||!config.IsValid())throw new ArgumentException("Invalid weather configuration");
            this.config=config;seed=unchecked((uint)config.seed);leaf=config.initialLeavesGain;
            points.Add(new Point(-1,config.initialWindGain));points.Add(new Point(0,config.initialWindGain));
            nextQuiet=Range(config.firstLullMinSeconds,config.firstLullMaxSeconds);
        }
        double Random(){seed=unchecked(seed*1664525u+1013904223u);return seed/4294967296.0;}
        double Range(double a,double b)=>a+(b-a)*Random();
        static double Smooth(double t){t=Math.Max(0,Math.Min(1,t));return t*t*(3-2*t);}
        double ValueAt(double at)
        {
            if(at<=points[0].time)return points[0].gain;
            var low=0;var high=points.Count-1;
            while(high-low>1){var mid=(low+high)>>1;if(points[mid].time>at)high=mid;else low=mid;}
            var a=points[low];var b=points[high];return a.gain+(b.gain-a.gain)*Smooth((at-a.time)/(b.time-a.time));
        }
        void Extend(double end)
        {
            while(points[points.Count-1].time<end)
            {
                var last=points[points.Count-1];var at=last.time+Range(config.stepMinSeconds,config.stepMaxSeconds);
                var gain=Range(config.windMinGain,config.windMaxGain);
                if(at>=nextQuiet)
                {
                    points.Add(new Point(at,config.lullGain));points.Add(new Point(at+Range(config.lullHoldMinSeconds,config.lullHoldMaxSeconds),config.lullGain));
                    nextQuiet=at+Range(config.lullGapMinSeconds,config.lullGapMaxSeconds);
                }
                else
                {
                    if(Math.Abs(gain-last.gain)<config.windAdjustmentThreshold)gain=Math.Max(config.windAdjustedMinimum,gain-config.windAdjustment);
                    points.Add(new Point(at,gain));
                }
            }
        }
        public DesktopWeatherBlock NextBlock()
        {
            var start=time;var duration=config.blockSeconds;var dt=config.sampleIntervalSeconds;var count=(int)Math.Round(duration/dt);
            var wind=new double[count+1];var leaves=new double[count+1];Extend(start+duration+1);
            for(var i=0;i<=count;i++)
            {
                var at=start+i*dt;var force=ValueAt(at-config.leavesDelaySeconds);
                var target=config.leavesResponseGain*Math.Pow(force/config.leavesReferenceWindGain,config.leavesResponseExponent);
                if(i>0||start==0)leaf+=(target-leaf)*(1-Math.Exp(-dt/(target>leaf?config.leavesRiseSeconds:config.leavesFallSeconds)));
                wind[i]=ValueAt(at);leaves[i]=leaf;
            }
            time=start+duration;
            var first=points.FindIndex(p=>p.time>=time-1);first=Math.Max(0,first-1);if(first>0)points.RemoveRange(0,first);
            return new DesktopWeatherBlock(start,duration,dt,wind,leaves);
        }
    }
    /// <summary>Immutable window published once for both audio filters. Audio reads allocate nothing and take no locks.</summary>
    public sealed class DesktopWeatherWindow
    {
        readonly DesktopWeatherBlock[] blocks;
        public int BlockCount=>blocks.Length;
        public double End=>blocks[blocks.Length-1].start+blocks[blocks.Length-1].duration;
        public DesktopWeatherWindow(DesktopWeatherBlock[] blocks){this.blocks=blocks;}
        public double Evaluate(double time,bool leaves)
        {
            foreach(var block in blocks)if(time<=block.start+block.duration)return block.Evaluate(time,leaves);
            return blocks[blocks.Length-1].Evaluate(time,leaves);
        }
    }
    public sealed class DesktopWeatherPlayback
    {
        readonly DesktopWeather model;
        readonly List<DesktopWeatherBlock> blocks=new List<DesktopWeatherBlock>();
        readonly double blockSeconds;
        public readonly double origin;
        public volatile DesktopWeatherWindow window;
        public int RetainedWindPoints=>model.RetainedWindPoints;
        public DesktopWeatherPlayback(DesktopWeatherConfiguration config,double origin)
        {this.origin=origin;blockSeconds=config.blockSeconds;model=new DesktopWeather(config);Prepare(origin);}
        public void Prepare(double dspTime)
        {
            var relative=Math.Max(0,dspTime-origin);
            // Keep the active block plus at least 60 seconds ahead (90 with the adopted 30-second block).
            if(window!=null&&window.End>=relative+60)return;
            while(blocks.Count==0||blocks[blocks.Count-1].start+blockSeconds<relative+60)
            {
                blocks.Add(model.NextBlock());
                while(blocks.Count>1&&blocks[0].start+blockSeconds<relative)blocks.RemoveAt(0);
            }
            window=new DesktopWeatherWindow(blocks.ToArray());
        }
        public double Evaluate(double dspTime,bool leaves)=>window.Evaluate(dspTime-origin,leaves);
    }
}
