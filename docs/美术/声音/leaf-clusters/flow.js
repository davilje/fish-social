// The preview and runtime read one formal bank. Defaults also support deterministic reference generation.
export const defaultWeather={
  seed:2201030,sampleIntervalSeconds:.05,blockSeconds:30,initialWindGain:.18,initialLeavesGain:.30,
  stepMinSeconds:2.4,stepMaxSeconds:4.8,windMinGain:.15,windMaxGain:.49,
  windAdjustmentThreshold:.045,windAdjustment:.08,windAdjustedMinimum:.13,
  firstLullMinSeconds:26,firstLullMaxSeconds:44,lullGain:.015,lullHoldMinSeconds:.8,lullHoldMaxSeconds:1.6,
  lullGapMinSeconds:28,lullGapMaxSeconds:46,leavesDelaySeconds:.28,leavesResponseGain:.73,
  leavesReferenceWindGain:.55,leavesResponseExponent:.85,leavesRiseSeconds:.65,leavesFallSeconds:1.35,
  fadeInSeconds:2,fadeOutSeconds:1.2,monitorGain:5,waterOffsetSeconds:10,windOffsetSeconds:3.2,leavesOffsetSeconds:19.7
};
export let levels={water:.6,wind:.18,leaves:.65};
export let weather={...defaultWeather};
export let auditionGain=weather.monitorGain;
export let waterProfiles={
  calm:{path:'ambience/pond_lake_calm.wav',gain:.6},
  ripples:{path:'ambience/pond_lake_ripples.wav',gain:.65},
  shore:{path:'ambience/pond_shore.wav',gain:.45}
};
export function configureBank(bank){
  if(bank.version!==4||!bank.weather||!['water','wind','leaves'].every(id=>bank.loops?.some(p=>p.id===id))||!bank.waterProfiles?.length)throw Error('环境声配置暂时无法读取');
  const next={...defaultWeather,...bank.weather};
  if(Object.keys(defaultWeather).some(key=>!Number.isFinite(next[key])||next[key]<0)||next.sampleIntervalSeconds<=0||next.leavesReferenceWindGain<=0||next.leavesRiseSeconds<=0||next.leavesFallSeconds<=0)throw Error('环境声配置暂时无法读取');
  levels=Object.fromEntries(bank.loops.map(p=>[p.id,p.gain]));
  waterProfiles=Object.fromEntries(bank.waterProfiles.map(p=>[p.id,{path:p.path,gain:p.gain}]));
  weather=next;auditionGain=weather.monitorGain;
}
export const smooth=t=>{t=Math.max(0,Math.min(1,t));return t*t*(3-2*t);};
export function seededRandom(seed=weather.seed){return ()=>{seed=(Math.imul(seed,1664525)+1013904223)>>>0;return seed/4294967296;};}
export function valueAt(points,time){
  if(time<=points[0].time)return points[0].gain;
  let low=0,high=points.length-1;
  while(high-low>1){const mid=(low+high)>>1;if(points[mid].time>time)high=mid;else low=mid;}
  const a=points[low],b=points[high];return a.gain+(b.gain-a.gain)*smooth((time-a.time)/(b.time-a.time));
}
export class Weather{
  constructor(random=seededRandom(),config=weather){
    this.random=random;this.config={...config};const c=this.config;
    this.wind=[{time:-1,gain:c.initialWindGain},{time:0,gain:c.initialWindGain}];
    this.leaf=c.initialLeavesGain;this.time=0;this.nextQuiet=this.range(c.firstLullMinSeconds,c.firstLullMaxSeconds);
  }
  range(a,b){return a+(b-a)*this.random();}
  extend(end){
    const c=this.config;
    while(this.wind.at(-1).time<end){
      const last=this.wind.at(-1),time=last.time+this.range(c.stepMinSeconds,c.stepMaxSeconds);
      let gain=this.range(c.windMinGain,c.windMaxGain);
      if(time>=this.nextQuiet){
        this.wind.push({time,gain:c.lullGain},{time:time+this.range(c.lullHoldMinSeconds,c.lullHoldMaxSeconds),gain:c.lullGain});
        this.nextQuiet=time+this.range(c.lullGapMinSeconds,c.lullGapMaxSeconds);
      }else{
        if(Math.abs(gain-last.gain)<c.windAdjustmentThreshold)gain=Math.max(c.windAdjustedMinimum,gain-c.windAdjustment);
        this.wind.push({time,gain});
      }
    }
  }
  block(duration=this.config.blockSeconds){
    const c=this.config,dt=c.sampleIntervalSeconds,start=this.time,count=Math.round(duration/dt),wind=[],leaves=[];this.extend(start+duration+1);
    for(let i=0;i<=count;i++){
      const time=start+i*dt,force=valueAt(this.wind,time-c.leavesDelaySeconds),target=c.leavesResponseGain*Math.pow(force/c.leavesReferenceWindGain,c.leavesResponseExponent);
      if(i>0||start===0)this.leaf+=(target-this.leaf)*(1-Math.exp(-dt/(target>this.leaf?c.leavesRiseSeconds:c.leavesFallSeconds)));
      wind.push({time:i*dt,gain:valueAt(this.wind,time)});leaves.push({time:i*dt,gain:this.leaf});
    }
    this.time=start+duration;
    let first=this.wind.findIndex(p=>p.time>=this.time-1);first=Math.max(0,first-1);this.wind=this.wind.slice(first);
    return {duration,start,wind,leaves};
  }
}
export function makePlan(duration=60,seed=weather.seed){
  const p=new Weather(seededRandom(seed)).block(duration);
  return {...p,seed,master:.7,auditionGain,offsets:{water:weather.waterOffsetSeconds,wind:weather.windOffsetSeconds,leaves:weather.leavesOffsetSeconds},levels:{...levels}};
}
