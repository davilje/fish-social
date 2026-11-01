// Tests visible motion, not only stability: a frozen mesh cannot pass this check.
const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const {chromium}=require(process.env.PLAYWRIGHT_MODULE||'playwright');
(async()=>{const out=path.resolve(__dirname,'../output/home-soft-drag-segments-v9');fs.mkdirSync(out,{recursive:true});const browser=await chromium.launch({headless:true,...(process.env.CHROME_PATH?{executablePath:process.env.CHROME_PATH}:{}),args:['--enable-unsafe-swiftshader']});try{
 const page=await browser.newPage({viewport:{width:1500,height:1200}}),errors=[];page.on('pageerror',e=>errors.push(e.message));await page.goto((process.env.PREVIEW_BASE||'http://127.0.0.1:8769')+'/现行预览.html#home-soft-drag');await page.waitForFunction(()=>document.querySelector('#stage').src.includes('soft-drag-study'));const frame=page.frames().find(f=>f.url().includes('/soft-drag-study-'));await frame.waitForFunction(()=>window.softDragStudy?.state.ready);const reports=[];
 for(const mode of ['head','body','frontLeft','frontRight','hindLeft','hindRight']){
  const r=await frame.evaluate(mode=>{
   const s=softDragStudy;s.state.paused=true;s.reset();s.begin(mode,s.restPoint(mode));for(let i=0;i<240;i++){s.move([600,s.state.start[1]+(150-s.state.start[1])*Math.min(1,i/180)]);s.step(1/120);if(i%2===0)s.render();}
   const d=s.poses[s.state.rendered.at(-1).id],sc=s.headSize/d.headWidth;let bendMin=Infinity,bendMax=-Infinity;let previous=null,deltaPrev=null,maxJerk=0,maxStep=0,minInk=Infinity,maxInk=0,maxResidual=0,faceError=0,heldError=0,lastProbeAngle=null,unwrappedAngle=0,minAngle=Infinity,maxAngle=-Infinity,folds=0,nearFrozen=0;const movement=[];
   const sample=(e,p)=>{const gx=p[0]/d.size[0]*28,gy=p[1]/d.size[1]*40,x=Math.max(0,Math.min(27,Math.floor(gx))),y=Math.max(0,Math.min(39,Math.floor(gy))),u=gx-x,v=gy-y,a=y*29+x,ids=u>=v?[a,a+1,a+30]:[a,a+30,a+29],w=u>=v?[1-u,u-v,v]:[1-v,u,v-u];return [0,1].map(axis=>ids.reduce((sum,k,j)=>sum+e.pts[k][axis]*w[j],0));};
   for(let i=0;i<360;i++){
    const x=i<180?600+Math.sin(i/16)*120:600+Math.sin(179/16)*120;s.move([x,150]);s.step(1/120);s.step(1/120);s.render();const e=s.state.rendered.at(-1),visible=new Set();
    for(let n=0;n<s.indices.length;n+=3){if(!e.visible[n/3])continue;const ids=s.indices.slice(n,n+3),[a,b,c]=ids.map(k=>e.pts[k]),u=ids.map(k=>[k%29/28*d.size[0]*sc,Math.floor(k/29)/40*d.size[1]*sc]),ux=u[1][0]-u[0][0],uy=u[1][1]-u[0][1],vx=u[2][0]-u[0][0],vy=u[2][1]-u[0][1],det0=ux*vy-uy*vx,dx=b[0]-a[0],dy=b[1]-a[1],ex=c[0]-a[0],ey=c[1]-a[1];if(dx*ey-dy*ex<=0)folds++;ids.forEach(k=>visible.add(k));if(e.ink[n/3]){const j00=(dx*vy-ex*uy)/det0,j01=(ex*ux-dx*vx)/det0,j10=(dy*vy-ey*uy)/det0,j11=(ey*ux-dy*vx)/det0,tr=j00*j00+j01*j01+j10*j10+j11*j11,det=j00*j11-j01*j10,disc=Math.sqrt(Math.max(0,tr*tr-4*det*det));minInk=Math.min(minInk,Math.sqrt(Math.max(0,(tr-disc)/2)));maxInk=Math.max(maxInk,Math.sqrt((tr+disc)/2));}}
    const a=sample(e,[d.head[0]-15,d.head[1]]),b=sample(e,[d.head[0]+15,d.head[1]]);faceError=Math.max(faceError,Math.abs(Math.hypot(a[0]-b[0],a[1]-b[1])/(30*sc)-1));const anchor=mode==='head'?(d.headGrab||d.head):d[mode],pin=sample(e,anchor);heldError=Math.max(heldError,Math.hypot(pin[0]-s.state.position[0],pin[1]-s.state.position[1]));const rig=s.ropes.get(e.id),sp=rig.guide,neck=sample(e,sp.neck),chest=sample(e,sp.chest),waist=sample(e,sp.waist),hip=sample(e,sp.hip),bend=Math.atan2(hip[1]-waist[1],hip[0]-waist[0])-Math.atan2(chest[1]-neck[1],chest[0]-neck[0]);if(i<180){const wrapped=Math.atan2(Math.sin(bend),Math.cos(bend));bendMin=Math.min(bendMin,wrapped);bendMax=Math.max(bendMax,wrapped);}const tip=sample(e,mode==='body'?d.head:d.body),angle=Math.atan2(tip[1]-pin[1],tip[0]-pin[0]);if(lastProbeAngle===null)unwrappedAngle=angle;else unwrappedAngle+=Math.atan2(Math.sin(angle-lastProbeAngle),Math.cos(angle-lastProbeAngle));lastProbeAngle=angle;if(i<180){minAngle=Math.min(minAngle,unwrappedAngle);maxAngle=Math.max(maxAngle,unwrappedAngle);}
    // Fit one rigid transform to visible vertices as a secondary check.
    // Torso angle variation above is the primary non-rigid motion criterion.
    const rc=[0,0],pc=[0,0],ids=[...visible];for(const k of ids){rc[0]+=(k%29)/28*d.size[0]*sc/ids.length;rc[1]+=Math.floor(k/29)/40*d.size[1]*sc/ids.length;pc[0]+=e.pts[k][0]/ids.length;pc[1]+=e.pts[k][1]/ids.length;}let dot=0,cross=0;for(const k of ids){const x=k%29/28*d.size[0]*sc-rc[0],y=Math.floor(k/29)/40*d.size[1]*sc-rc[1],u=e.pts[k][0]-pc[0],v=e.pts[k][1]-pc[1];dot+=x*u+y*v;cross+=x*v-y*u;}const theta=Math.atan2(cross,dot);let residual=0;for(const k of ids){const x=k%29/28*d.size[0]*sc-rc[0],y=Math.floor(k/29)/40*d.size[1]*sc-rc[1];residual+=Math.hypot(e.pts[k][0]-pc[0]-x*Math.cos(theta)+y*Math.sin(theta),e.pts[k][1]-pc[1]-x*Math.sin(theta)-y*Math.cos(theta));}maxResidual=Math.max(maxResidual,residual/ids.length);
    const local=e.pts.map(v=>[v[0]-s.state.position[0],v[1]-s.state.position[1]]);let step=0;if(previous){const deltas=local.map((p,k)=>[p[0]-previous[k][0],p[1]-previous[k][1]]);for(const k of visible){step=Math.max(step,Math.hypot(...deltas[k]));if(deltaPrev)maxJerk=Math.max(maxJerk,Math.hypot(deltas[k][0]-deltaPrev[k][0],deltas[k][1]-deltaPrev[k][1]));}deltaPrev=deltas;}if(i>0&&i<180&&step<.05)nearFrozen++;maxStep=Math.max(maxStep,step);movement.push(step);previous=local;
   }
   return {mode,bendingDegrees:(bendMax-bendMin)*180/Math.PI,swingDegrees:(maxAngle-minAngle)*180/Math.PI,maxResidual,minInk,maxInk,folds,faceError,heldError,maxStep,maxJerk,nearFrozen,stopLate:movement.slice(300).reduce((a,b)=>a+b,0)/60};
  },mode);reports.push(r);await frame.locator('.stage').screenshot({path:path.join(out,mode+'-settled.png')});
 }
 fs.writeFileSync(path.join(out,'verification.json'),JSON.stringify({reports,errors},null,2));console.log(JSON.stringify(reports));assert.deepEqual(errors,[]);

 // V8's whole-cat swing metric passed a rigid pendulum. Require changes in
 // the angle BETWEEN torso sections, measured on the rendered triangles.
 for(const r of reports){
  assert.ok(r.bendingDegrees>10,JSON.stringify(r));
  assert.ok(r.maxResidual>2.5,JSON.stringify(r));
  assert.ok(r.nearFrozen<20,JSON.stringify(r));
  assert.ok(r.minInk>=.649&&r.maxInk<=1.451,JSON.stringify(r));
  assert.equal(r.folds,0,JSON.stringify(r));
  assert.ok(r.faceError<.01,JSON.stringify(r));
  assert.ok(r.heldError<1.5,JSON.stringify(r));
  assert.ok(r.maxJerk<8,JSON.stringify(r));
  assert.ok(r.stopLate<.8,JSON.stringify(r));
 }
}finally{await browser.close();}})().catch(e=>{console.error(e);process.exitCode=1;});
