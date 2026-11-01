import {SegmentSkin} from './segment-skin.js?v=tissue-segments-9';
import {RopeRig} from './rope-rig.js?v=tissue-segments-9';
const clamp=(v,a,b)=>Math.max(a,Math.min(b,v));
const cross=(a,b,c)=>(b[0]-a[0])*(c[1]-a[1])-(b[1]-a[1])*(c[0]-a[0]);
const distance=(a,b)=>Math.hypot(a[0]-b[0],a[1]-b[1]);

// Fixed-topology soft tissue: a three-column triangular cage around the spine.
// Unlike Slime Link, links never break/reconnect and texture coordinates persist.
export class TissueRig extends RopeRig {
 constructor(...args){
  super(...args);this.kind='tissue';this.cells=[];this.cage=[];
  
  const width=this.def.headWidth*this.scale;
  const centers=this.spine.slice(1);
  for(let i=0;i<centers.length;i++){
   const id=centers[i],c=this.nodes[id],prev=this.nodes[centers[Math.max(0,i-1)]].ref,next=this.nodes[centers[Math.min(centers.length-1,i+1)]].ref;
   const d=distance(prev,next)||1,n=[-(next[1]-prev[1])/d,(next[0]-prev[0])/d];
   // The arch at middle-grip is tighter; a wide cage would overlap itself.
   const hw=width*(this.mode==='body'?.105:(.13+Math.sin(i/(centers.length-1)*Math.PI)*.075));
   const add=sign=>{const ref=[c.ref[0]+n[0]*hw*sign,c.ref[1]+n[1]*hw*sign];this.nodes.push({ref,p:[0,0],prev:[0,0],inv:2.4});return this.nodes.length-1;};
   this.cage.push([add(-1),id,add(1)]);
  }
  this.edges=[];const used=new Set();
  const edge=(a,b)=>{const key=[a,b].sort((a,b)=>a-b).join(':');if(used.has(key))return;used.add(key);this.edges.push({a,b,length:distance(this.nodes[a].ref,this.nodes[b].ref)});};
  const tri=(a,b,c)=>{let area=cross(this.nodes[a].ref,this.nodes[b].ref,this.nodes[c].ref)/2;if(area<0){[b,c]=[c,b];area=-area;}if(area<.1)return;this.cells.push({ids:[a,b,c],area});edge(a,b);edge(b,c);edge(c,a);};
  for(let i=0;i<this.cage.length-1;i++)for(let j=0;j<2;j++){const a=this.cage[i][j],b=this.cage[i][j+1],c=this.cage[i+1][j],d=this.cage[i+1][j+1];tri(a,b,d);tri(a,d,c);}
  for(const n of this.nodes){n.p=[this.pin[0]+n.ref[0]-this.contact[0],this.pin[1]+n.ref[1]-this.contact[1]];n.prev=[...n.p];}
  this.skinNodes=this.nodes.map(n=>n.ref.map((v,k)=>v-this.contact[k]));this.segmentSkin=new SegmentSkin(this);this.age=0;this.extension=1;this.motionSpeed=0;this.dampingRate=6;
 }
 update(pin,dt,soft){
  // Super's constructor invokes update before the cage exists.
  if(!this.cells)return super.update(pin,dt,soft);
  this.segmentSkin.clear();this.age+=dt;
  this.motionSpeed+=(distance(pin,this.lastPin)/dt-this.motionSpeed)*(1-Math.exp(-dt*12));
  const moving=clamp(this.motionSpeed/80,0,1),drag=9-moving*(3+soft*1.5);
  this.dampingRate+=(drag-this.dampingRate)*(1-Math.exp(-dt*10));
  this.pin=[...pin];this.lastPin=[...pin];
  for(const n of this.nodes){if(!n.inv)continue;const damping=Math.exp(-this.dampingRate*dt);let vx=(n.p[0]-n.prev[0])*damping,vy=(n.p[1]-n.prev[1])*damping;const l=Math.hypot(vx,vy),cap=750*dt;if(l>cap){vx*=cap/l;vy*=cap/l;}n.prev=[...n.p];n.p[0]+=vx;n.p[1]+=vy+760*dt*dt;const memory=3.5; n.p[0]+=(pin[0]+n.ref[0]-this.contact[0]-n.p[0])*memory*dt*dt;n.p[1]+=(pin[1]+n.ref[1]-this.contact[1]-n.p[1])*memory*dt*dt;}
  const pinNodes=()=>{if(this.mode==='head'){for(const i of [this.head,this.neck]){const n=this.nodes[i];n.p=[pin[0]+n.ref[0]-this.contact[0],pin[1]+n.ref[1]-this.contact[1]];n.prev=[...n.p];}}else{const n=this.nodes[this.pinNode];n.p=[pin[0]-this.offset[0],pin[1]-this.offset[1]];n.prev=[...n.p];}};
  const solveEdge=(e,k,limit=false)=>{const a=this.nodes[e.a],b=this.nodes[e.b],dx=b.p[0]-a.p[0],dy=b.p[1]-a.p[1],l=Math.hypot(dx,dy)||.001,w=a.inv+b.inv;if(!w)return;const target=limit?clamp(l,e.length*.65,e.length*1.65):e.length,c=(l-target)/l*k/w;a.p[0]+=dx*c*a.inv;a.p[1]+=dy*c*a.inv;b.p[0]-=dx*c*b.inv;b.p[1]-=dy*c*b.inv;};
  const bend=()=>{for(let i=1;i<this.spine.length-1;i++){
   const ns=[this.spine[i-1],this.spine[i],this.spine[i+1]].map(k=>this.nodes[k]),[a,b,c]=ns.map(n=>n.p),[ra,rb,rc]=ns.map(n=>n.ref),u=[b[0]-a[0],b[1]-a[1]],v=[c[0]-b[0],c[1]-b[1]],lu=u[0]*u[0]+u[1]*u[1],lv=v[0]*v[0]+v[1]*v[1];if(lu<1||lv<1)continue;
   const rest=Math.atan2(rc[1]-rb[1],rc[0]-rb[0])-Math.atan2(rb[1]-ra[1],rb[0]-ra[0]),diff=Math.atan2(Math.sin(Math.atan2(v[1],v[0])-Math.atan2(u[1],u[0])-rest),Math.cos(Math.atan2(v[1],v[0])-Math.atan2(u[1],u[0])-rest)),limit=i===1?.14:.20,C=diff-clamp(diff,-limit,limit);if(!C)continue;
   const ga=[-u[1]/lu,u[0]/lu],gc=[-v[1]/lv,v[0]/lv],grads=[ga,[-ga[0]-gc[0],-ga[1]-gc[1]],gc],den=ns.reduce((sum,n,j)=>sum+n.inv*(grads[j][0]**2+grads[j][1]**2),0);if(!den)continue;
   ns.forEach((n,j)=>{n.p[0]-=.85*C*n.inv*grads[j][0]/den;n.p[1]-=.85*C*n.inv*grads[j][1]/den;});
  }};
  for(let pass=0;pass<16;pass++){
   pinNodes();
   for(const e of this.edges){solveEdge(e,.018+(1-soft)*.09);solveEdge(e,.7,true);}
   for(const cell of this.cells){const ns=cell.ids.map(i=>this.nodes[i]),[a,b,c]=ns.map(n=>n.p),area=cross(a,b,c)/2;
    const grad=[[(b[1]-c[1])/2,(c[0]-b[0])/2],[(c[1]-a[1])/2,(a[0]-c[0])/2],[(a[1]-b[1])/2,(b[0]-a[0])/2]];
    const den=ns.reduce((s,n,i)=>s+n.inv*(grad[i][0]**2+grad[i][1]**2),0)||1;
    const lambda=clamp((area-cell.area)/den,-3,3)*(area<cell.area*.4?.95:.72);
    ns.forEach((n,i)=>{n.p[0]-=lambda*n.inv*grad[i][0];n.p[1]-=lambda*n.inv*grad[i][1];});
   }
   for(const e of this.links)solveEdge(e,e.kind==='rigid'?1:e.kind==='bend'?.003:.015);bend();
  }
  // Finish with fixed-length appendages and the actual grab constraint.
  for(let i=0;i<6;i++){for(const e of this.links)if(e.kind==='rigid')solveEdge(e,1);bend();pinNodes();}
  const follow=1-Math.exp(-dt*16);this.nodes.forEach((n,i)=>{for(let k=0;k<2;k++)this.skinNodes[i][k]+=(n.p[k]-pin[k]-this.skinNodes[i][k])*follow;});

 }
 map(pixel){return this.mapAt(pixel,1);}
 mapAt(pixel,amount){return this.segmentSkin.map(pixel,amount);}

 diagnostics(){const base=super.diagnostics(),ratios=this.cells.map(c=>cross(...c.ids.map(i=>this.nodes[i].p))/2/c.area);return {...base,kind:this.kind,areaRatio:this.cells.reduce((s,c)=>s+cross(...c.ids.map(i=>this.nodes[i].p))/2,0)/this.cells.reduce((s,c)=>s+c.area,0),minCellAreaRatio:Math.min(...ratios),maxCellAreaRatio:Math.max(...ratios),cageNodes:this.cage.flat().length};}
}
