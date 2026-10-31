// Preview-only spring deformation. A rigid head and monotone vertical bands keep
// the face intact and prevent the textured mesh from folding inside out.
export const clamp=(n,a,b)=>Math.max(a,Math.min(b,n));
export class SoftCat {
 constructor(w=230,h=265){this.w=w;this.h=h;this.soft=.75;this.levels=[0,.57,.67,.78,.89,1];this.reset();}
 reset(){this.x=610;this.y=565-this.h;this.vx=0;this.vy=0;this.grab=null;this.grounded=true;this.impact=0;this.time=0;this.nodes=this.levels.map(()=>({x:0,y:0,vx:0,vy:0}));}
 sample(v){let i=1;while(i<this.levels.length-1&&v>this.levels[i])i++;const a=this.nodes[i-1],b=this.nodes[i],t=clamp((v-this.levels[i-1])/(this.levels[i]-this.levels[i-1]),0,1);return {x:a.x+(b.x-a.x)*t,y:a.y+(b.y-a.y)*t,slope:(b.y-a.y)/((this.levels[i]-this.levels[i-1])*this.h)};}
 point(u,v){const n=this.sample(v),width=clamp(1/Math.sqrt(1+n.slope),.62,1.12);return [this.x+n.x+(u-.5)*this.w*width,this.y+v*this.h+n.y];}
 pick(x,y){const v=this.levels;for(let i=1;i<v.length;i++){const a=this.point(.5,v[i-1]),b=this.point(.5,v[i]);if(y<a[1]||y>b[1])continue;const f=(y-a[1])/(b[1]-a[1]),vv=v[i-1]+f*(v[i]-v[i-1]);const l=this.point(0,vv),r=this.point(1,vv),u=(x-l[0])/(r[0]-l[0]);if(u>=0&&u<=1)return {u,v:vv};}return null;}
 begin(x,y,uv){this.grab={...uv,x,y,lastX:x,lastY:y};this.vx=this.vy=0;this.grounded=false;this.impact=0;}
 move(x,y){if(this.grab){this.grab.x=clamp(x,12,1188);this.grab.y=clamp(y,12,600);}}
 release(){if(!this.grab)return;this.grab=null;this.vx=clamp(this.vx*.38,-620,620);this.vy=clamp(this.vy*.3,-450,650);}
 step(dt){dt=clamp(dt,0,1/60);this.time+=dt;const g=this.grab;
  if(g){const blend=1-Math.exp(-dt*24);this.vx+=(clamp((g.x-g.lastX)/Math.max(dt,.001),-2400,2400)-this.vx)*blend;this.vy+=(clamp((g.y-g.lastY)/Math.max(dt,.001),-2400,2400)-this.vy)*blend;g.lastX=g.x;g.lastY=g.y;}
  else if(!this.grounded){this.vy+=1500*dt;this.vx*=Math.exp(-dt*1.8);this.x+=this.vx*dt;this.y+=this.vy*dt;}
  else {this.vx*=Math.exp(-dt*10);this.x+=this.vx*dt;}
  this.impact*=Math.exp(-dt*8);
  for(let i=2;i<this.nodes.length;i++){
   const n=this.nodes[i],f=(this.levels[i]-.57)/.43,k=110-this.soft*78;
   const tx=clamp(-this.vx*(g?.10:.035)*this.soft*f*f,-this.h*.52,this.h*.52);
   const ty=g?clamp(this.h*.34*this.soft*f-this.vy*.065*this.soft*f,-this.h*.1*f,this.h*.52*f):-this.impact*this.h*.12*f;
   const damping=1.45*Math.sqrt(k);
   n.vx+=(k*(tx-n.x)-damping*n.vx)*dt;n.vy+=(k*(ty-n.y)-damping*n.vy)*dt;n.x+=n.vx*dt;n.y+=n.vy*dt;
   n.x=clamp(n.x,-this.h*.55,this.h*.55);
   const prev=this.nodes[i-1],gap=(this.levels[i]-this.levels[i-1])*this.h;
   const constrained=clamp(n.y,prev.y-gap*.32,prev.y+gap*1.25);
   if(constrained!==n.y)n.vy=0;n.y=constrained;
  }
  if(g){const at=this.point(g.u,g.v);this.x+=g.x-at[0];this.y+=g.y-at[1];}
  const bottom=this.point(.5,1)[1];
  if(g&&bottom>565)this.y-=bottom-565;
  if(!g&&(this.grounded||bottom>=565)){
   this.y=565-this.h-this.nodes.at(-1).y;
   if(!this.grounded&&this.vy>0){this.impact=clamp(this.vy/1000,.12,1);if(this.vy>260){this.vy*=-.15;}else{this.vy=0;this.grounded=true;}}
  }
  // Stage boundaries keep the cat recoverable even after very fast throws.
  const xs=this.levels.flatMap(v=>[this.point(0,v)[0],this.point(1,v)[0]]);
  const left=Math.min(...xs),right=Math.max(...xs);
  if(left<14){this.x+=14-left;this.vx=Math.abs(this.vx)*.2;}if(right>1186){this.x-=right-1186;this.vx=-Math.abs(this.vx)*.2;}
  if(this.y<14){this.y=14;this.vy=Math.max(0,this.vy);}
 }
}
