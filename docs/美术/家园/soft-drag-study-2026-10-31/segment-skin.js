import {headContours, contourWeight} from './rope-rig.js?v=tissue-segments-9';
const clamp=(v,a,b)=>Math.max(a,Math.min(b,v));
const mix=(a,b,t)=>[a[0]+(b[0]-a[0])*t,a[1]+(b[1]-a[1])*t];
const distance=(a,b)=>Math.hypot(a[0]-b[0],a[1]-b[1]);
const smooth=t=>{t=clamp(t,0,1);return t*t*(3-2*t);};
const wrap=a=>Math.atan2(Math.sin(a),Math.cos(a));
const transform=(q,t)=>{const x=q[0]-t.ref[0],y=q[1]-t.ref[1];return [t.p[0]+x*t.c-y*t.s,t.p[1]+x*t.s+y*t.c];};
const frame=(ref,p,a)=>({ref,p,c:Math.cos(a),s:Math.sin(a)});

// Each spinal section carries its own displacement. Fixed material weights
// avoid switching influences as the arch bends. The head-grab pose uses a
// local rigid MLS fit; other grabs blend displacement to preserve the curved
// reference anatomy. There is no shared whole-cat rotation.
export class SegmentSkin {
 constructor(rig){this.rig=rig;this.ids=rig.spine.slice(1);this.refs=this.ids.map(i=>rig.nodes[i].ref);this.weights=new Map();this.parts=new Map();this.clear();}
 clear(){this.frames=new Map();this.fieldCache=new Map();this.partFrames=new Map();}
 field(q,amount){
  const r=this.rig,key=q.join(','),cacheKey=amount+':'+key;
  if(this.fieldCache.has(cacheKey))return this.fieldCache.get(cacheKey);
  let weights=this.weights.get(key);
  if(!weights){const sigma=r.def.headWidth*r.scale*.23;weights=this.refs.map(p=>1/(1+(distance(p,q)/sigma)**2)**3);const sum=weights.reduce((a,b)=>a+b);weights=weights.map(w=>w/sum);this.weights.set(key,weights);}
  let frames=this.frames.get(amount);
  if(!frames){
   const nearest=this.refs.reduce((best,p,i)=>distance(p,r.contact)<distance(this.refs[best],r.contact)?i:best,0),arc=this.refs.map((_,i)=>{let length=0;for(let j=Math.min(i,nearest);j<Math.max(i,nearest);j++)length+=distance(this.refs[j],this.refs[j+1]);return length;}),width=r.def.headWidth*r.scale;
   frames=this.refs.map((ref,i)=>{const base=[r.pin[0]+ref[0]-r.contact[0],r.pin[1]+ref[1]-r.contact[1]],raw=r.skinNodes[this.ids[i]].map((v,k)=>v+r.contact[k]-ref[k]),limit=Math.min(width*.48,arc[i]*.65+width*.025),length=Math.hypot(...raw),factor=length>1e-6?limit*Math.tanh(length/limit)/length:1;return frame(ref,[base[0]+raw[0]*factor*amount,base[1]+raw[1]*factor*amount],0);});
   // Bound displacement differences locally, before skinning. A collapse at one
   // shoulder must not force the entire cat back toward a rigid rest pose.
   const offsets=frames.map(f=>[f.p[0]-r.pin[0]-f.ref[0]+r.contact[0],f.p[1]-r.pin[1]-f.ref[1]+r.contact[1]]);
   for(let pass=0;pass<5;pass++)for(let i=0;i<offsets.length;i++)for(let j=i+1;j<offsets.length;j++){const dx=offsets[j][0]-offsets[i][0],dy=offsets[j][1]-offsets[i][1],length=Math.hypot(dx,dy),limit=distance(this.refs[i],this.refs[j])*.55;if(length>limit){const k=(length-limit)/length*.5;offsets[i][0]+=dx*k;offsets[i][1]+=dy*k;offsets[j][0]-=dx*k;offsets[j][1]-=dy*k;}}
   frames.forEach((f,i)=>{f.p=[r.pin[0]+f.ref[0]-r.contact[0]+offsets[i][0],r.pin[1]+f.ref[1]-r.contact[1]+offsets[i][1]];});this.frames.set(amount,frames);
  }
  const center=[0,0],reference=[0,0];frames.forEach((f,i)=>{for(let k=0;k<2;k++){center[k]+=f.p[k]*weights[i];reference[k]+=f.ref[k]*weights[i];}});let dot=0,cross=0;frames.forEach((f,i)=>{const x=f.ref[0]-reference[0],y=f.ref[1]-reference[1],u=f.p[0]-center[0],v=f.p[1]-center[1];dot+=weights[i]*(x*u+y*v);cross+=weights[i]*(x*v-y*u);});const result=transform(q,frame(reference,center,r.mode==='head'?Math.atan2(cross,dot):0));this.fieldCache.set(cacheKey,result);return result;
 }
 map(pixel,amount){
  const r=this.rig,q=pixel.map(v=>v*r.scale),w=r.def.headWidth*r.scale,head=r.nodes[r.head];
  const key=pixel.join(',');let bind=this.parts.get(key);
  if(!bind){
   const hw=headContours[r.id]?contourWeight(pixel,headContours[r.id],r.def.headWidth*.20):1-smooth((distance(q,head.ref)/w-.53)/.23);
   const limbs=r.limbs.map(l=>{const root=r.nodes[l.root].ref,end=r.nodes[l.end].ref,x=end[0]-root[0],y=end[1]-root[1],t=clamp(((q[0]-root[0])*x+(q[1]-root[1])*y)/(x*x+y*y),0,1),radius=w*(l.key==='tail'?.14:.10);return Math.max((1-smooth((distance(q,mix(root,end,t))-radius*.4)/radius))*smooth(t/.55),1-smooth((distance(q,end)-w*.055)/(w*.10)));});
   // Blend all overlapping influences continuously. Selecting only the largest
   // one produced visible seams between the closely spaced dangling paws.
   const maximum=Math.max(...limbs),sum=limbs.reduce((a,b)=>a+b**4,0);bind={hw,body:1-maximum,limbs:limbs.map(v=>sum?maximum*v**4/sum:0)};this.parts.set(key,bind);
  }
  let parts=this.partFrames.get(amount);
  if(!parts){
   const hp=this.field(head.ref,amount),probe=this.field([head.ref[0]+w*.1,head.ref[1]],amount),ha=r.mode==='head'?0:clamp(Math.atan2(probe[1]-hp[1],probe[0]-hp[0]),-1.1,1.1);
   const limbs=r.limbs.map(l=>{const root=r.nodes[l.root],end=r.nodes[l.end],pivot=l.key===r.mode?end:root,rootField=this.field(root.ref,amount),probe=this.field(mix(root.ref,end.ref,.1),amount),local=wrap(Math.atan2(probe[1]-rootField[1],probe[0]-rootField[0])-Math.atan2(end.ref[1]-root.ref[1],end.ref[0]-root.ref[0])),physical=wrap(Math.atan2(end.p[1]-root.p[1],end.p[0]-root.p[0])-Math.atan2(end.ref[1]-root.ref[1],end.ref[0]-root.ref[0])),a=local+.12*Math.sin(physical-local)*amount;return frame(pivot.ref,this.field(pivot.ref,amount),a);});
   parts={head:frame(head.ref,hp,ha),limbs};this.partFrames.set(amount,parts);
  }
  const hp=transform(q,parts.head);if(bind.hw>=.999)return hp;
  const body=this.field(q,amount).map(v=>v*bind.body);parts.limbs.forEach((f,i)=>{if(!bind.limbs[i])return;const p=transform(q,f);body[0]+=p[0]*bind.limbs[i];body[1]+=p[1]*bind.limbs[i];});return mix(body,hp,bind.hw);
 }
}
