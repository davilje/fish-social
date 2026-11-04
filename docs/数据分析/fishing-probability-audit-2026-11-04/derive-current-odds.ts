/** Read-only audit: shared pure functions and generated JSON; never imports the server or opens a DB. */
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  FISH_QUALITIES, calcQualitySizeBiteRate, calcSingleFishBiteProbability,
  calcEffectiveEscapeRate, calcSizeEscapeRate, calcSizeHookDurationMs,
  calcGroundbaitBiteBonus, rodBiteMultiplier, baitBiteBonus,
  type PondFishEntity,
} from '../../../shared/index.ts';

const folder=path.dirname(fileURLToPath(import.meta.url));
const root=path.resolve(folder,'../../..');
const registry=JSON.parse(fs.readFileSync(path.join(root,'scripts/fishing-rules/current-rules.json'),'utf8'));
const read=(name:string)=>JSON.parse(fs.readFileSync(path.join(root,'shared/generated/game-data',`${name}.json`),'utf8'));
const qualities=read('fish_quality_stats');
const qualityTable=FISH_QUALITIES.map(q=>{
  const stats=qualities.find((r:any)=>r.quality===q.id);
  return {quality:q.id,name:q.name,candidateWeight:q.weight,capM:stats.sizeCapM,
    unscaledBite:stats.biteBaseAtMaxSize,
    biteAtHalf:calcQualitySizeBiteRate(q.id,.5*stats.sizeCapM,undefined,stats.sizeCapM),
    biteAtCap:calcQualitySizeBiteRate(q.id,stats.sizeCapM,undefined,stats.sizeCapM),
  };
});
const lengths=[.04,.1,.2,.35,.351,.5,.8,1,3,10,15,40,46];
const sizeTable=lengths.map(sizeM=>({sizeM,escapeBase:calcSizeEscapeRate(sizeM),hookMs:calcSizeHookDurationMs(sizeM),
  escapeBamboo:calcEffectiveEscapeRate(sizeM,'rod-bamboo',1),escapeGiant:calcEffectiveEscapeRate(sizeM,'rod-giant',1)}));
const groundbait=read('groundbaits').map((r:any)=>({...r,bonusAt1:calcGroundbaitBiteBonus(1,r.maxBonus,r.stackK),
  bonusAt10:calcGroundbaitBiteBonus(10,r.maxBonus,r.stackK),bonusAt50:calcGroundbaitBiteBonus(50,r.maxBonus,r.stackK)}));
const initialCounts=[38,28,18,9,4,2,1];
const denom=FISH_QUALITIES.reduce((n,q,i)=>n+q.weight*initialCounts[i]!,0);
const selectionExample=qualityTable.map((q,i)=>({...q,count:initialCounts[i],selectionShare:initialCounts[i]!*q.candidateWeight/denom}));
const fish={id:'audit',pondId:'pond-calm',spotId:'audit',speciesId:'crucian',quality:'gray',
  sizeM:.2,individualCapM:.2,biteMultiplier:1,escapeMultiplier:1,bornAt:0,generation:0} as PondFishEntity;
const rodMul=rodBiteMultiplier('rod-bamboo','gray','crucian');
const workedExamples=['bait-basic','bait-veg'].map(baitId=>{
  const baitBonus=baitBiteBonus(baitId,fish.speciesId);
  const p=calcSingleFishBiteProbability(fish,1,baitBonus)*rodMul;
  const escape=calcEffectiveEscapeRate(fish.sizeM,'rod-bamboo',1);
  return {baitId,fish:'普通鲫鱼20cm／上限20cm',spotMultiplier:1,rodMul,baitBonus,pBite:p,pEscape:escape,
    meanEligibleChecksToBite:1/p,atLeastOneBiteIn60EligibleChecks:1-Math.pow(1-p,60),
    successPerEligibleCheck:p*(1-escape),successOpportunityPer60Checks:60*p*(1-escape),
    hookMs:calcSizeHookDurationMs(fish.sizeM)};
});
const categories=read('pond_category_quality_weights');
const categoryWeights=Array.from(new Set(categories.map((r:any)=>r.pondCategory))).map(category=>{
 const rows=categories.filter((r:any)=>r.pondCategory===category);
 const total=rows.reduce((n:number,r:any)=>n+r.spawnWeight,0);
 return {category,name:rows[0].pondCategoryName,total,weights:FISH_QUALITIES.map(q=>rows.find((r:any)=>r.quality===q.id)?.spawnWeight??0)};
});
const species=read('fish_species');
const pools=read('pond_fish_pool');
const rarityRank:any={common:1,uncommon:2,rare:3,legendary:4};
const spawnEligibility=read('ponds').map((pond:any)=>({pondId:pond.pondId,name:pond.name,category:pond.pondCategory,
 qualities:FISH_QUALITIES.map((q,i)=>{
  const legal=pools.filter((row:any)=>row.pondId===pond.pondId&&row.spawnWeight>0).map((row:any)=>({...row,def:species.find((s:any)=>s.speciesId===row.speciesId)}))
    .filter((row:any)=>row.def&&i+1>=row.def.qualityMin&&i+1<=row.def.qualityMax);
  const best=Math.max(0,...legal.map((r:any)=>rarityRank[r.def.rarityTier]??0));
  return {quality:q.id,legalCount:legal.length,actuallyEligible:legal.filter((r:any)=>(rarityRank[r.def.rarityTier]??0)===best)
   .map((r:any)=>({speciesId:r.speciesId,name:r.def.name,spawnWeight:r.spawnWeight,rarityTier:r.def.rarityTier}))};
 })}));
const result={rulesetId:registry.rulesetId,modelKind:'static-formula-audit',asOf:'2026-11-04',scope:'local source + generated defaults; live overrides not inspected',qualityTable,sizeTable,groundbait,
 selectionExample,workedExamples,categoryWeights,rods:read('rods'),baits:read('baits'),pondModifiers:read('pond_modifiers'),species,spawnEligibility};
fs.writeFileSync(path.join(folder,'current-odds.json'),JSON.stringify(result,null,2)+'\n');
console.log(JSON.stringify({qualityTable,workedExamples,sizeTable,categoryWeights,speciesCount:species.length,
 sampleSpawn:spawnEligibility.find((p:any)=>p.pondId==='pond-calm')},null,2));
