export const clamp=(v,a,b)=>Math.max(a,Math.min(b,v));
export const partsSpec={
 head:{w:218,h:184,pivot:[.53,.91]},
 farArm:{w:36,h:78,pivot:[.5,.16],v:.34,airV:.11,u:.75,rest:-.06,length:66,damping:2.8},
 nearArm:{w:40,h:80,pivot:[.5,.16],v:.35,airV:.13,u:.30,rest:.06,length:68,damping:2.3},
 farLeg:{w:43,h:62,pivot:[.5,.18],v:.59,airV:.94,u:.72,rest:-1.0,length:51,damping:3.0},
 nearLeg:{w:47,h:64,pivot:[.5,.18],v:.62,airV:.96,u:.28,rest:.95,length:53,damping:2.5},
 tail:{w:54,h:120,pivot:[.34,.10],v:.71,u:.14,rest:1.75,length:108,damping:1.6}
};
const order=['tail','farLeg','nearLeg','torso','farArm','nearArm','head'];
const spring=(s,target,k,d,dt)=>{s.v+=(k*(target-s.p)-d*s.v)*dt;s.p+=s.v*dt;};
export class LayeredCat{
 constructor(){this.soft=.8;this.reset();}
 reset(){this.x=610;this.y=440;this.vx=this.vy=0;this.grab=null;this.grounded=true;this.impact=0;this.air=0;this.length={p:95,v:0};this.sway={p:0,v:0};this.head={p:0,v:0};this.joints=Object.fromEntries(Object.entries(partsSpec).filter(([k])=>k!=='head').map(([k,s])=>[k,{p:s.rest,v:0,ax:0,previous:null,velocity:null}]));this.time=0;this.fitGround();}
 body(u,v){const f=v*v*(3-2*v);return [this.x+this.sway.p*f+(u-.5)*138,this.y+v*this.length.p];}
 jointV(s){return s.v+((s.airV??s.v)-s.v)*this.air;}
 partPoint(id,u,v){if(id==='torso')return this.body(u,v);const s=partsSpec[id],a=id==='head'?[this.x+7,this.y+5]:this.body(s.u,this.jointV(s));const theta=id==='head'?this.head.p:this.joints[id].p,c=Math.cos(theta),sn=Math.sin(theta),dx=(u-s.pivot[0])*s.w,dy=(v-s.pivot[1])*s.h;return [a[0]+c*dx-sn*dy,a[1]+sn*dx+c*dy];}
 items(){return order.map(id=>({id,points:[[0,0],[1,0],[1,1],[0,1]].map(p=>this.partPoint(id,...p))}));}
 bounds(){const pts=this.items().flatMap(i=>i.points);return {l:Math.min(...pts.map(p=>p[0])),r:Math.max(...pts.map(p=>p[0])),t:Math.min(...pts.map(p=>p[1])),b:Math.max(...pts.map(p=>p[1]))};}
 supportBottom(){return Math.max(...['farArm','nearArm','farLeg','nearLeg'].flatMap(id=>[.35,.5,.65].map(u=>this.partPoint(id,u,.96)[1])),this.body(.5,.97)[1]);}
 fitGround(){this.y+=565-this.supportBottom();}
 tailFloor(){const j=this.joints.tail;for(let i=0;i<45;i++){const bottom=Math.max(...[[0,0],[1,0],[1,1],[0,1]].map(p=>this.partPoint('tail',...p)[1]));if(bottom<=565.01)break;j.p+=(partsSpec.tail.rest-j.p)*.18;j.v*=.7;}}
 begin(x,y,hit){this.grab={...hit,x,y,lastX:x,lastY:y};this.vx=this.vy=0;this.grounded=false;this.impact=0;}
 move(x,y){if(this.grab){this.grab.x=clamp(x,10,1190);this.grab.y=clamp(y,10,620);}}
 release(){if(!this.grab)return;this.grab=null;this.vx=clamp(this.vx*.65,-800,800);this.vy=clamp(this.vy*.48,-500,750);}
 step(dt){dt=clamp(dt,0,1/60);this.time+=dt;const g=this.grab;let ax=0;
  if(g){const old=this.vx,blend=1-Math.exp(-dt*18);this.vx+=(clamp((g.x-g.lastX)/Math.max(.001,dt),-2200,2200)-this.vx)*blend;this.vy+=(clamp((g.y-g.lastY)/Math.max(.001,dt),-2000,2000)-this.vy)*blend;ax=clamp((this.vx-old)/Math.max(.001,dt),-5500,5500);g.lastX=g.x;g.lastY=g.y;
   const p=this.partPoint(g.id,g.u,g.v);this.x+=g.x-p[0];this.y+=g.y-p[1];
  }else if(!this.grounded){this.vy+=1300*dt;this.vx*=Math.exp(-dt*.65);this.x+=this.vx*dt;this.y+=this.vy*dt;}else{this.vx*=Math.exp(-dt*9);this.x+=this.vx*dt;}
  const lift=g?clamp((440-this.y)/75,0,1):this.grounded?0:1;
  this.air+=(lift-this.air)*(1-Math.exp(-dt*8));this.impact*=Math.exp(-dt*7);
  spring(this.length,95+this.air*(35+65*this.soft)-this.impact*15,45,6.8,dt);this.length.p=clamp(this.length.p,78,210);
  const previousSwayVelocity=this.sway.v;
  spring(this.sway,clamp(-this.vx*(.07+this.soft*.10),-82,82)*this.air,19,4.2,dt);this.sway.p=clamp(this.sway.p,-90,90);
  const swayAcceleration=clamp((this.sway.v-previousSwayVelocity)/dt,-3500,3500);
  spring(this.head,clamp(-this.vx*.00022-ax*.000018,-.24,.24)*this.air,48,5.2,dt);this.head.p=clamp(this.head.p,-.32,.32);
  for(const [id,j] of Object.entries(this.joints)){
   const s=partsSpec[id],v=this.jointV(s),acceleration=clamp(ax+swayAcceleration*v*v*(3-2*v),-4500,4500);
   j.ax+=(acceleration-j.ax)*(1-Math.exp(-dt*13));
   const rest=s.rest*(1-this.air),gravity=850/s.length;
   const torque=-gravity*Math.sin(j.p-rest)-j.ax/s.length*Math.cos(j.p)*(.5+.55*this.soft)*this.air;
   j.v+=(torque-(s.damping+(1-this.air)*5)*j.v)*dt;j.v=clamp(j.v,-7,7);j.p+=j.v*dt;
   const limit=id==='tail'?2.7:1.1;if(Math.abs(j.p)>limit){j.p=clamp(j.p,-limit,limit);j.v*=-.12;}
  }
  // Attachment translation is shared, but every part keeps its own angular state.
  if(g){const p=this.partPoint(g.id,g.u,g.v);this.x+=g.x-p[0];this.y+=g.y-p[1];}
  let b=this.bounds(),bottom=this.supportBottom();
  if(g&&bottom>565)this.y-=bottom-565;
  else if(!g&&(this.grounded||bottom>=565)){
   this.y+=565-bottom;if(!this.grounded&&this.vy>0){this.impact=clamp(this.vy/900,.12,1);if(this.vy>280)this.vy*=-.13;else{this.vy=0;this.grounded=true;}}
  }
  this.tailFloor();
  b=this.bounds();if(b.l<12){this.x+=12-b.l;this.vx=Math.abs(this.vx)*.15;}if(b.r>1188){this.x-=b.r-1188;this.vx=-Math.abs(this.vx)*.15;}if(b.t<12){this.y+=12-b.t;this.vy=Math.max(0,this.vy);}
 }
}
