// Position-based flexible spine. Every node carries velocity; the pinned part
// drives constraints, rather than rotating the whole sprite around one pivot.
const clamp=(x,a,b)=>Math.max(a,Math.min(b,x));
const mix=(a,b,t)=>[a[0]+(b[0]-a[0])*t,a[1]+(b[1]-a[1])*t];
const sub=(a,b)=>[a[0]-b[0],a[1]-b[1]],len=a=>Math.hypot(...a);
const angle=a=>Math.atan2(a[1],a[0]);
const rot=(p,a)=>[p[0]*Math.cos(a)-p[1]*Math.sin(a),p[0]*Math.sin(a)+p[1]*Math.cos(a)];
const smooth=t=>(t=clamp(t,0,1))*t*(3-2*t);
const paws=['frontLeft','frontRight','hindLeft','hindRight'];
const headContours={
 headHang:[[80,0],[470,0],[519,120],[519,305],[440,357],[170,357],[70,280],[65,80]],
 front2:[[15,145],[50,70],[100,65],[130,110],[245,52],[293,80],[328,220],[317,300],[235,346],[85,323],[12,240]],
 leftFront:[[177,162],[227,107],[271,59],[314,100],[340,155],[432,163],[440,229],[378,291],[280,314],[205,265]],
 leftHind:[[205,267],[298,228],[377,240],[423,299],[444,383],[396,393],[348,415],[300,456],[255,437],[226,390],[181,353]],
 rightHind:[[48,270],[108,236],[200,246],[239,288],[254,370],[212,405],[181,478],[143,442],[102,422],[0,385],[8,326]],
 middle2:[[0,179],[35,145],[76,117],[101,55],[151,39],[211,98],[238,143],[256,194],[235,250],[170,297],[87,309],[12,275]],
};
function contourWeight(p,poly,feather){let inside=false,dist=Infinity;for(let i=0,j=poly.length-1;i<poly.length;j=i++){const a=poly[j],b=poly[i];if((a[1]>p[1])!==(b[1]>p[1])&&p[0]<(b[0]-a[0])*(p[1]-a[1])/(b[1]-a[1])+a[0])inside=!inside;const v=sub(b,a),d=v[0]**2+v[1]**2,t=clamp(((p[0]-a[0])*v[0]+(p[1]-a[1])*v[1])/d,0,1);dist=Math.min(dist,len(sub(p,mix(a,b,t))));}return inside?1:1-smooth(dist/feather);}
const guides={
 headHang:{neck:[303,351],chest:[303,413],waist:[300,508],hip:[300,641],roots:[[220,374],[391,382],[257,660],[351,660]],tail:[211,619]},
 front2:{neck:[226,324],chest:[239,351],waist:[240,405],hip:[240,469],roots:[[217,349],[280,322],[207,476],[276,471]],tail:[163,451]},
 front1:{neck:[215,305],chest:[226,334],waist:[229,386],hip:[237,452],roots:[[213,333],[276,312],[196,459],[269,453]],tail:[147,418]},
 front0:{neck:[205,281],chest:[207,311],waist:[214,355],hip:[215,405],roots:[[177,307],[264,303],[161,400],[268,402]],tail:[133,383]},
 leftFront:{neck:[244,282],chest:[216,307],waist:[197,348],hip:[194,389],roots:[[199,269],[248,311],[163,369],[218,395]],tail:[117,302]},
 leftHind:{neck:[258,267],chest:[245,237],waist:[230,186],hip:[208,124],roots:[[218,264],[277,281],[191,125],[174,187]],tail:[150,106]},
 rightHind:{neck:[203,270],chest:[225,235],waist:[251,185],hip:[289,126],roots:[[228,265],[189,285],[290,181],[308,122]],tail:[344,106]},
 leftHind0:{neck:[296,228],chest:[270,205],waist:[245,164],hip:[196,131],roots:[[260,235],[319,260],[156,132],[214,183]],tail:[101,142]},
 rightHind0:{neck:[234,225],chest:[263,203],waist:[288,163],hip:[314,130],roots:[[226,258],[283,237],[306,188],[331,130]],tail:[379,145]},
 middle2:{neck:[236,218],chest:[255,197],waist:[284,96],hip:[313,188],roots:[[195,261],[241,238],[313,222],[284,202]],tail:[348,238]},
 middle1:{neck:[224,194],chest:[251,174],waist:[284,89],hip:[317,184],roots:[[154,249],[223,243],[318,227],[341,225]],tail:[361,212]},
 middle0:{neck:[220,186],chest:[244,189],waist:[293,144],hip:[324,215],roots:[[135,246],[206,243],[315,248],[351,244]],tail:[373,222]},
};
export class RopeRig{
 constructor(def,id,mode,scale,pin,offset=[0,0]){
  this.def=def;this.id=id;this.mode=mode;this.scale=scale;this.offset=offset;this.nodes=[];this.links=[];this.spine=[];this.limbs=[];this.age=0;this.extension=1;
  const g=guides[id]||{neck:mix(def.head,def.body,.65),chest:mix(def.head,def.body,.9),waist:def.body,hip:mix(def.hindLeft,def.hindRight,.5),roots:paws.map(k=>mix(def.body,def[k],.35)),tail:mix(def.body,def.tail,.5)};
  this.guide=g;
  const a=mode==='head'?(def.headGrab||def.head):def[mode];this.contact=[a[0]*scale+offset[0],a[1]*scale+offset[1]];
  const add=(p,mass=1)=>{const r=p.map(v=>v*scale),world=[pin[0]+r[0]-this.contact[0],pin[1]+r[1]-this.contact[1]];this.nodes.push({ref:r,p:world,prev:[...world],inv:1/mass});return this.nodes.length-1;};
  const link=(a,b,kind='rigid')=>{this.links.push({a,b,length:len(sub(this.nodes[a].ref,this.nodes[b].ref)),kind});};
  this.head=add(def.head,3.5);this.spine.push(this.head);
  this.neck=add(g.neck,.7);this.spine.push(this.neck);link(this.head,this.neck);
  for(const target of [g.chest,g.waist,g.hip]){const previous=this.nodes[this.spine.at(-1)].ref.map(v=>v/scale);for(const t of [.5,1]){const node=add(mix(previous,target,t),.7);link(this.spine.at(-1),node,'tissue');this.spine.push(node);}}
  link(this.head,this.spine[3]);
  for(let i=1;i<this.spine.length-2;i++)link(this.spine[i],this.spine[i+2],'bend');
  for(let i=0;i<5;i++){
   const key=paws[i]||'tail',root=i<4?g.roots[i]:g.tail,tip=def[key],rootId=add(root,.35),endId=add(tip,i<4?.55:.4);
   // A small triangle attaches a limb socket to the local spinal segment.
   const near=this.spine.slice(1).map(j=>[j,len(sub(this.nodes[j].ref,this.nodes[rootId].ref))]).sort((a,b)=>a[1]-b[1]);link(rootId,near[0][0]);link(rootId,near[1][0]);link(rootId,endId);
   this.limbs.push({key,root:rootId,end:endId});if(key===mode)this.pinNode=endId;
  }
  if(mode==='head'){this.pinNode=this.head;this.nodes[this.head].inv=0;this.nodes[this.neck].inv=0;}
  else if(mode==='body'){this.pinNode=add(a,.5);const near=this.spine.slice(1).map(j=>[j,len(sub(this.nodes[j].ref,this.nodes[this.pinNode].ref))]).sort((a,b)=>a[1]-b[1]);link(this.pinNode,near[0][0]);link(this.pinNode,near[1][0]);}
  this.nodes[this.pinNode].inv=0;this.lastPin=[...pin];this.pin=[...pin];this.update(pin,1/120,.85);
 }
 update(pin,dt,soft){
  this.age+=dt;this.pin=[...pin];const speed=len(sub(pin,this.lastPin))/dt;this.lastPin=[...pin];this.extension+=(1.16+soft*.43+clamp(speed/1000,0,1)*.12-this.extension)*Math.min(1,dt*5);
  for(const n of this.nodes){if(!n.inv)continue;let v=sub(n.p,n.prev);const max=1400*dt,l=len(v);if(l>max)v=v.map(x=>x*max/l);n.prev=[...n.p];const damping=Math.exp(-(1.7-soft*.9)*dt);n.p[0]+=v[0]*damping;n.p[1]+=v[1]*damping+760*dt*dt;}
  const pinNodes=()=>{const n=this.nodes[this.pinNode];if(this.mode==='head'){for(const id of [this.head,this.neck]){const k=this.nodes[id];k.p=[pin[0]+k.ref[0]-this.contact[0],pin[1]+k.ref[1]-this.contact[1]];k.prev=[...k.p];}}else{n.p=[pin[0]-this.offset[0],pin[1]-this.offset[1]];n.prev=[...n.p];}};
  for(let pass=0;pass<14;pass++){
   pinNodes();
   for(const e of this.links){const a=this.nodes[e.a],b=this.nodes[e.b],v=sub(b.p,a.p),l=len(v)||1,w=a.inv+b.inv;if(!w)continue;const target=e.length*(e.kind==='tissue'?this.extension:e.kind==='bend'?this.extension:1),k=e.kind==='bend'?.025+(1-soft)*.10:e.kind==='tissue'?.78:1,c=(l-target)/l*k;
    a.p[0]+=v[0]*c*a.inv/w;a.p[1]+=v[1]*c*a.inv/w;b.p[0]-=v[0]*c*b.inv/w;b.p[1]-=v[1]*c*b.inv/w;
   }
  }pinNodes();
 }
 // Smooth displacement skinning keeps the painted silhouette continuous across
 // joints. Particle excursions are bounded before skinning, not discarded by
 // reverting the whole cat to one rigid transform.
 field(q){let dx=0,dy=0,total=0;const sigma=this.def.headWidth*this.scale*.60;
  for(const id of this.spine){const n=this.nodes[id],r=len(sub(q,n.ref)),w=1/((1+(r/sigma)**2)**2),base=[this.pin[0]+n.ref[0]-this.contact[0],this.pin[1]+n.ref[1]-this.contact[1]],d=sub(n.p,base),limit=110;
   dx+=Math.tanh(d[0]/limit)*limit*w;dy+=Math.tanh(d[1]/limit)*limit*w;total+=w;
  }return [this.pin[0]+q[0]-this.contact[0]+dx/total,this.pin[1]+q[1]-this.contact[1]+Math.max(0,dy/total)*.8];
 }
 map(pixel){const q=pixel.map(v=>v*this.scale),head=this.nodes[this.head],neck=this.nodes[this.neck],headRadius=this.def.headWidth*this.scale,wrap=a=>Math.atan2(Math.sin(a),Math.cos(a));
  const headAngle=this.mode==='head'?0:clamp(wrap(angle(sub(head.p,neck.p))-angle(sub(head.ref,neck.ref))),-.08,.08),headV=rot(sub(q,head.ref),headAngle),headCenter=this.mode==='head'?head.p:this.field(head.ref),headP=[headCenter[0]+headV[0],headCenter[1]+headV[1]],headWeight=headContours[this.id]?contourWeight(pixel,headContours[this.id],this.def.headWidth*.14):1-smooth((len(sub(q,head.ref))/headRadius-.53)/.18);
  if(headWeight>=.999)return headP;
  let body=this.field(q),best=null;
  for(const limb of this.limbs){const r=this.nodes[limb.root],end=this.nodes[limb.end],v=sub(end.ref,r.ref),l2=v[0]**2+v[1]**2,t=clamp(((q[0]-r.ref[0])*v[0]+(q[1]-r.ref[1])*v[1])/l2,0,1),dist=len(sub(q,mix(r.ref,end.ref,t))),radius=headRadius*(limb.key==='tail'?.15:.115),weight=Math.max((1-smooth((dist-radius*.6)/(radius*.9)))*smooth(t/.48),1-smooth((len(sub(q,end.ref))-headRadius*.12)/(headRadius*.10)));
   if(!best||weight>best.weight){const held=limb.key===this.mode,theta=clamp(wrap(angle(sub(end.p,r.p))-angle(v)),held?-.18:-.09,held?.18:.09),pivot=held?end:r,world=held?end.p:this.field(r.ref),z=rot(sub(q,pivot.ref),theta);best={weight,p:[world[0]+z[0],world[1]+z[1]]};}
  }
  if(best)body=mix(body,best.p,best.weight);return mix(body,headP,headWeight);
 }
 diagnostics(){return {spine:this.spine.map(i=>[...this.nodes[i].p]),extension:this.extension,limbs:this.limbs.map(l=>{const a=this.nodes[l.root],b=this.nodes[l.end];return {key:l.key,ratio:len(sub(b.p,a.p))/len(sub(b.ref,a.ref))};})};}
}

export {headContours,contourWeight};
