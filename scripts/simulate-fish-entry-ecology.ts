/** FISH-ECOLOGY-01 deterministic stocking stress model; no database imports or writes. */
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  FISH_QUALITIES, FISH_DAY_MS, FISH_BITE_CHECK_MS, getGamePondDef,
  getPondModifier, getPondStockConfig, pickSpawnFish, pickSupplementFish,
  createFishEntryGrowth, calcEntryFishLength, calcPondSupplementCount,
  calcSupplementIdealCounts, getPondEcologyDayKey, calcSingleFishBiteProbability,
  calcEffectiveEscapeRate, rodBiteMultiplier, baitBiteBonus, formatFishSize,
  type FishEntryGrowthState, type FishQuality, type PondFishEntity,
} from '../shared/index';

const ROOT=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..');
const OUT=path.join(ROOT,'docs/数据分析/fish-growth-opening-2026-11-02');
const START=Date.UTC(2026,10,3,20); // 2026-11-04 04:00 Asia/Shanghai
const HOUR=3_600_000, DAYS=30, REPLICATES=10;
const SCENARIOS=[
  ['pond-calm','高级塘'],['pond-crystal','老手塘'],['pond-stone','野外塘'],
  ['pond-coral','水库塘'],['pond-ridge','禁止塘'],['pond-frost','巨物塘'],
] as const;
type Fish=PondFishEntity & FishEntryGrowthState;
type Counts=Record<FishQuality,number>;
type Snapshot={hour:number; count:number; meanM:number; medianM:number; meanCapRatio:number; over80Share:number; over95Share:number; meanResidenceDays:number; initialSurvivors:number; quality:Counts; theoreticalSuccessesPerHour:number};
type Day={day:number; caught:number; supplied:number; hourlyRefills:number; dailyRefills:number; minStock:number; catchMeanM:number; catchMedianM:number; catchP90M:number; catchMeanCapRatio:number; catchQuality:Counts; beforeSettlement:Snapshot; afterSettlement:Snapshot};
const empty=():Counts=>Object.fromEntries(FISH_QUALITIES.map(q=>[q.id,0])) as Counts;
const mean=(xs:number[])=>xs.length?xs.reduce((a,b)=>a+b,0)/xs.length:0;
function percentile(xs:number[],p:number):number {
  if(!xs.length)return 0;
  const values=[...xs].sort((a,b)=>a-b),index=(values.length-1)*p,lo=Math.floor(index),hi=Math.ceil(index);
  return values[lo]!+(values[hi]!-values[lo]!)*(index-lo);
}
function seeded(seed:number):()=>number {
  let value=seed>>>0;
  return ()=>{value+=0x6D2B79F5;let t=value;t=Math.imul(t^t>>>15,t|1);t^=t+Math.imul(t^t>>>7,t|61);return ((t^t>>>14)>>>0)/4294967296;};
}
const checks={generated:0,lengthChecks:0,conservationChecks:0,highQualityInsertChecks:0};

function run(pondId:string,anglers:number,seed:number) {
  const savedRandom=Math.random;
  Math.random=seeded(seed);
  try {
    const category=getGamePondDef(pondId)!.pondCategory;
    const modifiers=getPondModifier(category);
    const maxPopulation=getPondStockConfig(pondId)!.maxPopulation;
    const ideal=calcSupplementIdealCounts(maxPopulation,category);
    let serial=0,caught=0,supplied=0,lowRefills=0,dailyRefills=0,minStock=100;
    const stock:Fish[]=[],quality=empty();
    const firstDayHours:Array<{hour:number;before:number;after:number;refill:number;daily:boolean;snapshot:Snapshot}>=[];
    const days:Day[]=[];
    const lowRefillHours:number[]=[];
    let dayCatchSizes:number[]=[],dayCatchRatios:number[]=[],dayCatchQuality=empty();
    let dayStartSupplied=0,dayStartLow=0,dayStartDaily=0,dayMin=100;
    function checkLength(fish:Fish,now:number) {
      fish.sizeM=calcEntryFishLength(fish.quality,fish,now);
      assert(fish.sizeM>=fish.entrySizeM-1e-10 && fish.sizeM<=fish.individualCapM+1e-10);
      assert(fish.enteredAt<=now && fish.growthBonusMinutes===0 && (fish.outOfPondMs??0)===0);
      checks.lengthChecks++;
    }
    function generate(now:number,initial:boolean) {
      const chosen=initial?pickSpawnFish(pondId):pickSupplementFish(pondId,quality,maxPopulation);
      assert(chosen,`No legal stock candidate: ${pondId}`);
      if(!initial && FISH_QUALITIES.findIndex(q=>q.id===chosen.quality)>=3) {
        assert(quality[chosen.quality]<ideal[chosen.quality],`High quality overfill ${pondId}/${chosen.quality}`);
        checks.highQualityInsertChecks++;
      }
      const id=`simulation-${pondId}-${anglers}-${seed}-${serial++}`;
      const growth=createFishEntryGrowth(chosen.quality,id,now,initial?'pre_opening_seed':'supplement');
      const fish:Fish={id,pondId,spotId:'fully-mixed-model',...chosen,...growth,
        sizeM:0,bornAt:0,generation:initial?0:1,biteMultiplier:1,escapeMultiplier:1};
      if(initial)assert(growth.enteredAt>=START-14*FISH_DAY_MS && growth.entrySizeM===growth.juvenileEntrySizeM);
      else assert(growth.enteredAt===now);
      checkLength(fish,now);stock.push(fish);quality[fish.quality]++;checks.generated++;
    }
    function conservation() {
      assert.equal(stock.length,100+supplied-caught);
      assert.equal(Object.values(quality).reduce((a,b)=>a+b,0),stock.length);
      assert(stock.length<=100 && stock.length>=0);
      checks.conservationChecks++;
    }
    function weights(now:number) {
      return stock.map(fish=>{
        checkLength(fish,now);
        const candidateWeight=FISH_QUALITIES.find(q=>q.id===fish.quality)!.weight;
        const hook=Math.min(1,Math.max(0,calcSingleFishBiteProbability(fish,1,baitBiteBonus('bait-basic',fish.speciesId))
          *modifiers.biteRateMul*rodBiteMultiplier('rod-bamboo',fish.quality,fish.speciesId)));
        const escape=Math.min(1,Math.max(0,calcEffectiveEscapeRate(fish.sizeM,'rod-bamboo',1)*modifiers.escapeRateMul));
        return {candidateWeight,successWeight:candidateWeight*hook*(1-escape)};
      });
    }
    function snapshot(now:number):Snapshot {
      const probabilities=weights(now),sizes=stock.map(f=>f.sizeM);
      const ratios=stock.map(f=>f.sizeM/f.individualCapM);
      return {hour:(now-START)/HOUR,count:stock.length,meanM:mean(sizes),medianM:percentile(sizes,.5),meanCapRatio:mean(ratios),
        over80Share:ratios.filter(r=>r>=.8).length/stock.length,over95Share:ratios.filter(r=>r>=.95).length/stock.length,
        meanResidenceDays:mean(stock.map(f=>(now-f.enteredAt)/FISH_DAY_MS)),initialSurvivors:stock.filter(f=>f.source==='pre_opening_seed').length,
        quality:{...quality},theoreticalSuccessesPerHour:probabilities.reduce((sum,p)=>sum+p.successWeight,0)
          /probabilities.reduce((sum,p)=>sum+p.candidateWeight,0)*(HOUR/FISH_BITE_CHECK_MS)};
    }
    for(let i=0;i<100;i++)generate(START,true);
    const opening=snapshot(START);
    let dayKey=getPondEcologyDayKey(START);
    firstDayHours.push({hour:0,before:100,after:100,refill:0,daily:false,snapshot:opening});
    for(let hour=1;hour<=DAYS*24;hour++) {
      for(let ordinal=0;ordinal<anglers;ordinal++) {
        const at=START+(hour-1+(ordinal+.5)/anglers)*HOUR;
        assert(stock.length>0,'Given load exhausted the pond before a stock check');
        const successWeights=weights(at).map(p=>p.successWeight);
        const total=successWeights.reduce((a,b)=>a+b,0);
        assert(total>0,'No success-conditioned capture candidate');
        let roll=Math.random()*total,selected=successWeights.length-1;
        for(let i=0;i<successWeights.length;i++){roll-=successWeights[i]!;if(roll<=0){selected=i;break;}}
        const fish=stock.splice(selected,1)[0]!;
        quality[fish.quality]--;caught++;dayCatchQuality[fish.quality]++;
        dayCatchSizes.push(fish.sizeM);dayCatchRatios.push(fish.sizeM/fish.individualCapM);
        minStock=Math.min(minStock,stock.length);dayMin=Math.min(dayMin,stock.length);
        conservation();
      }
      const now=START+hour*HOUR;
      const daily=getPondEcologyDayKey(now)!==dayKey;
      const beforeCount=stock.length;
      const beforeSettlement=daily?snapshot(now):undefined;
      const refill=calcPondSupplementCount(stock.length,daily,maxPopulation);
      if(refill>0) {
        if(daily)dailyRefills++;else{lowRefills++;lowRefillHours.push(hour);}
        for(let i=0;i<refill;i++){generate(now,false);supplied++;conservation();}
      }
      dayKey=getPondEcologyDayKey(now);
      const after=snapshot(now);
      if(hour<=24)firstDayHours.push({hour,before:beforeCount,after:stock.length,refill,daily,snapshot:after});
      if(daily) {
        assert.equal(stock.length,100);
        days.push({day:hour/24,caught:dayCatchSizes.length,supplied:supplied-dayStartSupplied,
          hourlyRefills:lowRefills-dayStartLow,dailyRefills:dailyRefills-dayStartDaily,minStock:dayMin,
          catchMeanM:mean(dayCatchSizes),catchMedianM:percentile(dayCatchSizes,.5),catchP90M:percentile(dayCatchSizes,.9),
          catchMeanCapRatio:mean(dayCatchRatios),catchQuality:{...dayCatchQuality},
          beforeSettlement:beforeSettlement!,afterSettlement:after});
        dayCatchSizes=[];dayCatchRatios=[];dayCatchQuality=empty();
        dayStartSupplied=supplied;dayStartLow=lowRefills;dayStartDaily=dailyRefills;dayMin=stock.length;
      }
      conservation();
    }
    return {pondId,category,anglers,seed,opening,firstDayHours,days,
      totals:{caught,supplied,returned:0,remaining:stock.length,minStock,lowRefills,dailyRefills,
        lowRefillIntervalHoursMedian:percentile(lowRefillHours.slice(1).map((h,i)=>h-lowRefillHours[i]!),.5)}};
  } finally {Math.random=savedRandom;}
}

const runs:ReturnType<typeof run>[]=[];
for(const [pondId] of SCENARIOS)for(const anglers of [0,5,20]) {
  for(let i=0;i<REPLICATES;i++)runs.push(run(pondId,anglers,20261104+i));
  console.log(`Simulated ${pondId}: ${anglers} anglers × ${REPLICATES} seeds × ${DAYS} days`);
}
function aggregateSnapshot(values:Snapshot[]):Omit<Snapshot,'hour'> {
  const keys=['count','meanM','medianM','meanCapRatio','over80Share','over95Share','meanResidenceDays','initialSurvivors','theoreticalSuccessesPerHour'] as const;
  return {...Object.fromEntries(keys.map(key=>[key,mean(values.map(v=>v[key]))])) as Record<typeof keys[number],number>,
    quality:Object.fromEntries(FISH_QUALITIES.map(q=>[q.id,mean(values.map(v=>v.quality[q.id]))])) as Counts};
}
const summaries=SCENARIOS.flatMap(([pondId,label])=>[0,5,20].map(anglers=>{
  const selected=runs.filter(r=>r.pondId===pondId && r.anglers===anglers);
  return {pondId,label,anglers,replicates:REPLICATES,opening:aggregateSnapshot(selected.map(r=>r.opening)),
    totalCaught:mean(selected.map(r=>r.totals.caught)),totalSupplied:mean(selected.map(r=>r.totals.supplied)),
    firstDay:selected[0]!.firstDayHours.map((_,i)=>({hour:i,before:mean(selected.map(r=>r.firstDayHours[i]!.before)),
      after:mean(selected.map(r=>r.firstDayHours[i]!.after)),refill:mean(selected.map(r=>r.firstDayHours[i]!.refill)),
      ...aggregateSnapshot(selected.map(r=>r.firstDayHours[i]!.snapshot))})),
    daily:Array.from({length:DAYS},(_,i)=>({day:i+1,caught:mean(selected.map(r=>r.days[i]!.caught)),
      supplied:mean(selected.map(r=>r.days[i]!.supplied)),hourlyRefills:mean(selected.map(r=>r.days[i]!.hourlyRefills)),
      dailyRefills:mean(selected.map(r=>r.days[i]!.dailyRefills)),minStock:Math.min(...selected.map(r=>r.days[i]!.minStock)),
      catchMeanM:mean(selected.map(r=>r.days[i]!.catchMeanM)),catchMedianM:mean(selected.map(r=>r.days[i]!.catchMedianM)),
      catchP90M:mean(selected.map(r=>r.days[i]!.catchP90M)),catchMeanCapRatio:mean(selected.map(r=>r.days[i]!.catchMeanCapRatio)),
      catchQuality:Object.fromEntries(FISH_QUALITIES.map(q=>[q.id,mean(selected.map(r=>r.days[i]!.catchQuality[q.id]))])),
      beforeSettlement:aggregateSnapshot(selected.map(r=>r.days[i]!.beforeSettlement)),
      afterSettlement:aggregateSnapshot(selected.map(r=>r.days[i]!.afterSettlement))})),
    lastSevenCatchMeanM:mean(selected.flatMap(r=>r.days.slice(-7).map(d=>d.catchMeanM))),
    lastSevenCatchMedianM:mean(selected.flatMap(r=>r.days.slice(-7).map(d=>d.catchMedianM))),
    pondMeanWeeks:[0,1,2,3].map(w=>mean(selected.flatMap(r=>r.days.slice(w*7,w*7+7).map(d=>d.afterSettlement.meanM)))),
  };
}));

const result={version:'FISH-ECOLOGY-01 implementation stress model',date:'2026-11-04',startAt:START,days:DAYS,seeds:Array.from({length:REPLICATES},(_,i)=>20261104+i),
  assumptions:{successLoad:'Exactly one successful catch per active angler-hour; a stress input, not a measured probability result.',
    captureAllocation:'Fully mixed pond; weight per fish = quality candidate weight × current shared single-fish bite rate × bamboo rod multiplier × pond bite multiplier × (1 - rod-adjusted escape rate × pond escape multiplier).',
    stock:'Actual shared opening/mixed growth, original category/species pool, hourly <=40 to80, Shanghai04:00 to100, strict high-quality cap.',
    excluded:['actual random bite timing','hook duration and locking','spot migration and local density','bait nutrition','groundbait','returns','rod breakage','forbidden-pond eviction','mortality','emigration'],
    shifts:'5/20 continuously active seats require 15/60 users in three 8-hour shifts per day. Scenarios are alternatives, not simultaneous demands on one 100-user line.',
    referenceRate:'Bamboo rod + free basic bait + spot multiplier1, 60 independent checks/hour, no busy time. Theoretical success opportunities, not observed catch throughput.'},
  checks,totals:{runs:runs.length,simulatedPondDays:runs.length*DAYS},summaries,runs};
fs.mkdirSync(OUT,{recursive:true});
fs.writeFileSync(path.join(OUT,'implementation-simulation.json'),JSON.stringify(result,null,2));

const table=(headers:string[],rows:unknown[][])=>'| '+headers.join(' | ')+' |\n| '+headers.map(()=>'---').join(' | ')+' |\n'+rows.map(row=>'| '+row.join(' | ')+' |').join('\n');
const pct=(n:number)=>`${(n*100).toFixed(1)}%`;
const length=(n:number)=>formatFishSize(n);
const advanced=summaries.filter(s=>s.pondId==='pond-calm');
const rows24=advanced.map(s=>{const d=s.daily[0]!;return [s.anglers,d.caught,d.supplied,d.minStock,d.hourlyRefills,d.dailyRefills,
  s.anglers?length(d.catchMedianM):'—',length(Number(d.afterSettlement.medianM)),pct(Number(d.afterSettlement.over80Share))];});
const rows30=summaries.map(s=>{const d=s.daily[29]!;return [s.label,s.anglers,s.totalCaught,s.totalSupplied,
  s.anglers?length(s.lastSevenCatchMedianM):'—',length(Number(d.afterSettlement.medianM)),pct(Number(d.afterSettlement.over80Share)),Number(d.afterSettlement.initialSurvivors).toFixed(1)];});
const qualityRows=advanced.filter(s=>s.anglers>0).flatMap(s=>[0,29].map(day=>{
  const d=s.daily[day]!;return [`${s.anglers}人·第${day+1}天`,...FISH_QUALITIES.map(q=>`${d.catchQuality[q.id]!.toFixed(1)} / ${Number(d.afterSettlement.quality[q.id]).toFixed(1)}`)];
}));
const weekRows=advanced.map(s=>[s.anglers,...s.pondMeanWeeks.map(length),
  s.anglers?`${((s.pondMeanWeeks[3]!-s.pondMeanWeeks[2]!)/s.pondMeanWeeks[2]!*100).toFixed(1)}%`:'持续趋近上限']);
const rateRows=advanced.map(s=>[s.anglers,Number(s.opening.theoreticalSuccessesPerHour).toFixed(3),Number(s.daily[0]!.afterSettlement.theoreticalSuccessesPerHour).toFixed(3),Number(s.daily[29]!.afterSettlement.theoreticalSuccessesPerHour).toFixed(3)]);
const report=`# FISH-ECOLOGY-01：实现后的24小时与30天生态压力模拟

2026-11-04。复用当前共享业务函数，六类正式塘×0／5／20人在钓×10个固定种子，共${runs.length}次独立模拟、${runs.length*DAYS}塘日。结果用于验证成长与补鱼的变化，未连接数据库。

**在给定每人每小时成功捕获1条的压力下，≤40补80和每日04:00补100能形成可重复的库存波动；混合规格补鱼能持续提供不同长度。长期无人捕捞时，鱼仍会接近各自上限。当前自然成长方案不等于已经实现人均每小时成功1条。**

## 怎样模拟

- 初始100条；原品质权重和合法鱼种池；幼鱼基准3.4～4.6厘米、独立上限±15%、T80=1／2／3／5／7／10／14天。初始体长先抽取，再倒推前14～0天入塘。
- 使用实际共享函数生成50%幼鱼／40%成长鱼／10%较成熟鱼；每小时检查≤40补80，每日上海时间04:00补100，重合仅日结；史诗及以上达到原表库存目标即停止补入。
- 0／5／20是同时在钓人数，全天持续。后两者每天120／480条成功捕获是输入负载；需15／60名用户按8小时轮班。一条100人线路每日最多800条的目标预算下，这些是单塘独立压力情景，不能把40塘都按满员相加。
- 在已发生成功捕获的条件下，按当前品质候选权重、咬钩与脱钩公式分配被捕获个体。采用全塘充分混合近似，位置系数1、竹竿、基础免费饵、个体咬脱偏置均为1。
- 未模拟实际咬钩时间、收杆占用、钓位局部密度与迁徙、摄食营养、打窝、回鱼、断竿、禁止塘驱离、死亡或迁出。巨物塘作为其配置的压力测试，不改变正式锁闭状态。
- 表内统计是10个种子的均值；“钓获中位体长”是各次模拟中位数的均值，并非混合全部样本后的中位数。小数条数是跨模拟均值。

## 第一天：高级塘

从04:00开塘开始计时，表内“日末”在次日04:00补足100之后。

${table(['同时在钓','成功捕获','补入','最低库存','小时补鱼次数','日结补鱼次数','钓获中位体长','日末在塘中位','日末≥80%上限'],rows24)}

20人满负载时，库存从100降到80、60、40，首次在第3小时补至80；随后通常每2小时触发一次低水位补鱼，日结恢复100。5人情景第12、20小时触发低水位补鱼。因为捕获量固定、始终有鱼可捕，这些数量轨迹不受随机品质和尺寸影响；真实随机捕获会让触发时间前后移动。

![24小时库存与体长、30天变化](implementation-simulation.png)

## 不同品质被钓走与补入后的分布

高级塘单格为“当天捕获数 / 次日04:00补足后的在塘数”。高品质自动补入受库存上限限制；开塘随机超额个体允许继续存在，不能为了凑配额删除。

${table(['情景',...FISH_QUALITIES.map(q=>q.name)],qualityRows)}

## 30天后：六类塘对照

所有情景30天末均补足100条；捕获与补入严格守恒。钓获中位体长取最后7天的日统计均值。

${table(['鱼塘类别','在钓人数','30天捕获','30天补入','末7天钓获中位','第30天在塘中位','≥80%上限','初始鱼仍在塘'],rows30)}

## 稳定态与周期性

数量上存在由固定捕获负载、低水位补鱼和04:00日结共同产生的周期；它不是鱼自然繁殖形成的生态平衡。以下高级塘每周“每日04:00补鱼后平均体长”的均值，可以观察尺寸是否仍在缓慢漂移。

${table(['在钓人数','第1周均长','第2周均长','第3周均长','第4周均长','第4周相对第3周'],weekRows)}

繁忙塘的大多数低品质鱼在捕获和补入间更替，尺寸分布可进入有波动的重复区间；较少被捕获的高品质鱼会继续长大，因此总平均体长可能在低品质已稳定后仍上升。30天和10个种子只能支持这一条件下的趋势，不能证明严格平稳分布或全部玩家行为下都稳定。无人塘没有捕获与自然更替，会逐渐堆积接近个体上限的鱼；±15%仅保留个体尺寸差异，不会自动更新鱼群。

## 与当前捕获概率的边界

下表仅把当前共享概率在全塘充分混合、位置系数1、竹竿＋免费饵、每小时60次独立判定的条件下，折算为“单人每小时理论成功机会”。尚未扣除收杆占用、空钓位、断竿等实际损失，也未加入收费饵与打窝帮助；不是实际用户产量模拟。

${table(['库存来自的压力情景','开塘时','第1天补鱼后','第30天补鱼后'],rateRows)}

这说明“补鱼能够承受1条／人／小时”与“当前咬钩算法能产出1条／人／小时”需要分别验收。后者仍需结合真实装备、饵料、局部钓位和在线过程校准。

## 校验与复现

- ${checks.generated.toLocaleString('en-US')}条生成记录：初始历史入塘范围、实际补入时刻和上限合法。
- ${checks.lengthChecks.toLocaleString('en-US')}次尺寸检查：从实际入塘长度连续增长、不超过个体上限；本次营养与离塘时间均为0。
- ${checks.conservationChecks.toLocaleString('en-US')}次库存守恒检查：当前库存＝100＋累计补入−累计捕获＋回鱼（本次0）。
- ${checks.highQualityInsertChecks.toLocaleString('en-US')}次高品质新增检查：每条新增前真实计数严格低于原浮点理想数，批次内实时更新。
- 所有日结后库存100、无空塘负载失败、无超100补入。这里未替代服务端事务、重启幂等或并发测试。

脚本：scripts/simulate-fish-entry-ecology.ts。数据：[implementation-simulation.json](implementation-simulation.json)。图表由同目录绘图脚本使用标准Matplotlib从该JSON生成。固定种子20261104～20261113；运行前构建shared，使用tsx运行模拟。
`;
fs.writeFileSync(path.join(OUT,'implementation-simulation.md'),report);
console.log(JSON.stringify({output:OUT,runs:runs.length,checks}));
