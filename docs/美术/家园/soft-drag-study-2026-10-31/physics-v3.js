// A connected illustration is skinned continuously. Rigid distal limbs share
// their original contour with the torso through short, blended joint regions.
export const clamp=(x,a,b)=>Math.max(a,Math.min(b,x));
const smooth=(a,b,x)=>{const t=clamp((x-a)/(b-a),0,1);return t*t*(3-2*t);};
export const bones={
 leftArm:{pivot:[.42,.45],length:70,damping:3.4,limit:.28,polygon:[[.34,.43],[.53,.44],[.62,.58],[.65,.68],[.58,.72],[.47,.70],[.42,.61],[.34,.53]]},
 rightArm:{pivot:[.81,.44],length:65,damping:3.0,limit:.25,polygon:[[.76,.42],[.86,.43],[.91,.51],[.90,.64],[.86,.70],[.75,.71],[.70,.65],[.73,.55]]},
 leftLeg:{pivot:[.44,.76],length:67,damping:2.8,limit:.24,polygon:[[.32,.66],[.48,.71],[.55,.83],[.60,.95],[.55,1.02],[.40,1.02],[.35,.90],[.31,.80]]},
 rightLeg:{pivot:[.74,.77],length:63,damping:2.5,limit:.24,polygon:[[.65,.73],[.80,.69],[.85,.78],[.82,.90],[.83,1.02],[.65,1.02],[.61,.94],[.64,.84]]},
 tail:{pivot:[.32,.77],length:80,damping:2.1,limit:.35,polygon:[[-.03,.55],[.23,.54],[.29,.65],[.38,.74],[.38,.85],[.18,.88],[-.03,.79]]}
};
function polygonWeight(u,v,poly){let inside=false,min=Infinity;for(let i=0,j=poly.length-1;i<poly.length;j=i++){
 const a=poly[i],b=poly[j];if((a[1]>v)!==(b[1]>v)&&u<(b[0]-a[0])*(v-a[1])/(b[1]-a[1])+a[0])inside=!inside;
 const dx=b[0]-a[0],dy=b[1]-a[1],t=clamp(((u-a[0])*dx+(v-a[1])*dy)/(dx*dx+dy*dy),0,1);min=Math.min(min,Math.hypot(u-a[0]-dx*t,v-a[1]-dy*t));
 }return inside?1:1-smooth(0,.12,min);}
export function weights(u,v){const head=1-smooth(.405,.455,v);if(head===1)return {head:1,body:0,limbs:[]};
 const scores=Object.entries(bones).map(([id,b])=>{
 const joint=id==='tail'?1-smooth(.22,.40,u):smooth(b.pivot[1]-.015,b.pivot[1]+.095,v);
 return [id,Math.pow(polygonWeight(u,v,b.polygon)*joint,3)];
 });
 const body=scores.reduce((r,[,s])=>r*(1-s),1),sum=body+scores.reduce((r,[,s])=>r+s,0);
 return {head,body:body/sum*(1-head),limbs:scores.map(([id,s])=>[id,s/sum*(1-head)])};}

const spring=(s,target,k,d,dt)=>{s.v+=(k*(target-s.p)-d*s.v)*dt;s.p+=s.v*dt;};
export class ConnectedCat{
 constructor(aspect=.65){this.w=365*aspect;this.h=365;this.soft=.75;this.reset();}
 reset(){this.x=610;this.y=565-270;this.idleHeight=270;this.gain=1;this.vx=this.vy=0;this.air=0;this.poseMix=0;this.openPose=false;this.grab=null;this.grounded=true;this.stretch={p:.78,v:0};this.sway={p:0,v:0};this.head={p:0,v:0};this.pendulum={p:0,v:0};this.joints=Object.fromEntries(Object.keys(bones).map(id=>[id,{p:0,v:0}]));this.time=0;this.impact=0;this.fitGround();}
 bodyPoint(u,v){const t=smooth(.43,.79,v),delta=clamp(v-.43,0,.36)*this.h*(this.stretch.p-1),width=1+(1/Math.sqrt(this.stretch.p)-1)*t*.65;return [this.x+(u-.60)*this.w*width+this.sway.p*t,this.y+v*this.h+delta];}
 headPoint(u,v){const dx=(u-.60)*this.w,dy=(v-.40)*this.h,c=Math.cos(this.head.p),s=Math.sin(this.head.p);return [this.x+c*dx-s*dy,this.y+.40*this.h+s*dx+c*dy];}
 bonePoint(id,u,v){const b=bones[id],p=this.bodyPoint(...b.pivot),theta=this.joints[id].p,c=Math.cos(theta),s=Math.sin(theta),dx=(u-b.pivot[0])*this.w,dy=(v-b.pivot[1])*this.h;return [p[0]+dx*c-dy*s,p[1]+dx*s+dy*c];}
 skinPoint(u,v,ws=weights(u,v)){const a=this.bodyPoint(u,v),h=this.headPoint(u,v);let x=a[0]*ws.body+h[0]*ws.head,y=a[1]*ws.body+h[1]*ws.head;for(const [id,w] of ws.limbs){if(!w)continue;const p=this.bonePoint(id,u,v);x+=p[0]*w;y+=p[1]*w;}return [x,y];}
 rotate(p){const px=this.x,py=this.y+.20*this.h,dx=p[0]-px,dy=p[1]-py,c=Math.cos(this.pendulum.p),s=Math.sin(this.pendulum.p);return [px+c*dx-s*dy,py+s*dx+c*dy];}
 basePoint(u,v){return this.rotate([this.x+(u-.60)*this.w,this.y+v*this.h]);}
 rawPoint(u,v,ws=weights(u,v)){return this.rotate(this.skinPoint(u,v,ws));}
 point(u,v,ws=weights(u,v)){const p=this.rawPoint(u,v,ws),base=this.basePoint(u,v);return [base[0]+(p[0]-base[0])*this.gain,base[1]+(p[1]-base[1])*this.gain];}
 hangingBottom(){return Math.max(...[.43,.48,.52,.68,.73,.77].map(u=>this.point(u,.977)[1]));}
 supportBottom(){return (this.y+this.idleHeight)*(1-this.poseMix)+this.hangingBottom()*this.poseMix;}
 fitGround(){this.y+=565-this.supportBottom();}
 begin(x,y,uv){this.grab={...uv,x,y,lastX:x,lastY:y};this.vx=this.vy=0;this.grounded=false;}
 move(x,y){if(this.grab){this.grab.x=clamp(x,80,1120);this.grab.y=clamp(y,30,570);}}
 release(){if(!this.grab)return;this.grab=null;this.vx=clamp(this.vx*.5,-620,620);this.vy=clamp(this.vy*.4,-400,600);}
 step(dt){dt=clamp(dt,0,1/60);if(!dt)return;this.time+=dt;const g=this.grab;let ax=0;
 if(g){const old=this.vx,k=1-Math.exp(-dt*16);this.vx+=(clamp((g.x-g.lastX)/dt,-1600,1600)-this.vx)*k;this.vy+=(clamp((g.y-g.lastY)/dt,-1600,1600)-this.vy)*k;ax=clamp((this.vx-old)/dt,-3500,3500);g.lastX=g.x;g.lastY=g.y;const p=this.point(g.u,g.v);this.x+=g.x-p[0];this.y+=g.y-p[1];}
 else if(!this.grounded){this.vy+=1300*dt;this.vx*=Math.exp(-dt*.9);this.x+=this.vx*dt;this.y+=this.vy*dt;}else{this.vx*=Math.exp(-dt*9);this.x+=this.vx*dt;}
 const lift=g?clamp((565-this.idleHeight-this.y)/(this.h*.977-this.idleHeight),0,1):this.grounded?0:1;this.air+=(lift-this.air)*(1-Math.exp(-dt*9));this.impact*=Math.exp(-dt*9);
 if(g&&this.air>.85)this.openPose=true;if(this.grounded)this.openPose=false;
 this.poseMix+=clamp((this.openPose?1:0)-this.poseMix,-dt/.18,dt/.18);
 spring(this.stretch,.78+this.air*(.27+.20*this.soft)-this.impact*.05,65,10,dt);this.stretch.p=clamp(this.stretch.p,.70,1.34);
 spring(this.pendulum,clamp(this.vx*(.00016+.00015*this.soft),-.22,.22)*this.air,22,4.2,dt);this.pendulum.p=clamp(this.pendulum.p,-.26,.26);
 const oldSway=this.sway.v;spring(this.sway,clamp(-this.vx*(.035+.055*this.soft),-36,36)*this.air,27,5,dt);this.sway.p=clamp(this.sway.p,-45,45);
 spring(this.head,clamp(-this.vx*.00009,-.10,.10)*this.air,65,9,dt);
 const baseAccel=clamp((this.sway.v-oldSway)/dt,-2200,2200);
 for(const [id,j] of Object.entries(this.joints)){const b=bones[id],force=clamp(ax+baseAccel*(id.includes('Leg')?.9:.25),-3000,3000);j.v+=(-22*j.p-force/b.length*.20*this.air-(b.damping+6*(1-this.air))*j.v)*dt;j.p+=j.v*dt;if(Math.abs(j.p)>b.limit){j.p=clamp(j.p,-b.limit,b.limit);j.v*=.3;}}
 if(g){const p=this.point(g.u,g.v);this.x+=g.x-p[0];this.y+=g.y-p[1];}
 const bottom=this.supportBottom();if(g&&bottom>565)this.y-=bottom-565;
 else if(!g&&(bottom>=565||this.grounded)){this.y+=565-bottom;if(!this.grounded){this.impact=clamp(this.vy/900,0,1);if(this.vy>260)this.vy*=-.10;else{this.vy=0;this.grounded=true;}}}
 this.x=clamp(this.x,160,1060);if(this.y<12){this.y=12;this.vy=Math.max(0,this.vy);}
 }
}
