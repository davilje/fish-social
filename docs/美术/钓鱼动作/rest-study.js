// Preview-only motion study. All coordinates are logical 640 x 512 artwork units.
// Apply one continuous field to every character layer to preserve shared seams.
window.restStudy = (() => {
  const duration = 6400;
  const keys = [
    [0, 0], [900, .08], [1900, .48], [2700, 1],
    [3300, 1], [3900, .18], [4400, .32], [5200, 0], [6400, 0]
  ];
  const clamp = n => Math.max(0, Math.min(1, n));
  const ease = n => n*n*(3-2*n);
  function nod(time) {
    const phase = ((time % duration) + duration) % duration;
    const next = keys.findIndex(key => key[0] > phase);
    const a = keys[next-1], b = keys[next];
    return a[1] + (b[1]-a[1])*ease((phase-a[0])/(b[0]-a[0]));
  }
  function point(x, y, amount) {
    const weight = ease(clamp((410-y)/180));
    const angle = amount*8*Math.PI/180, c = Math.cos(angle), s = Math.sin(angle);
    const dx=x-282, dy=y-337;
    return [x+weight*(c*dx-s*dy-dx+3*amount),
            y+weight*(s*dx+c*dy-dy+11*amount)];
  }
  function rod(amount) {
    const grip=[346,348.5], target=point(...grip,amount);
    const angle=amount*1.5*Math.PI/180,c=Math.cos(angle),s=Math.sin(angle);
    return [c,s,-s,c,target[0]-c*grip[0]+s*grip[1],target[1]-s*grip[0]-c*grip[1]];
  }
  function transform(m,p) {return [m[0]*p[0]+m[2]*p[1]+m[4],m[1]*p[0]+m[3]*p[1]+m[5]];}
  function multiply(a,b) {
    return [a[0]*b[0]+a[2]*b[1],a[1]*b[0]+a[3]*b[1],a[0]*b[2]+a[2]*b[3],a[1]*b[2]+a[3]*b[3],
      a[0]*b[4]+a[2]*b[5]+a[4],a[1]*b[4]+a[3]*b[5]+a[5]];
  }
  // Each triangle shares exactly the same vertex mapping across all four layers.
  function triangle(ctx,asset,src,dst) {
    const [p,q,r]=src,[u,v,w]=dst;
    const a=q[0]-p[0],b=q[1]-p[1],c=r[0]-p[0],d=r[1]-p[1],det=a*d-b*c;
    const x=v[0]-u[0],y=v[1]-u[1],z=w[0]-u[0],h=w[1]-u[1];
    const m=[(x*d-z*b)/det,(y*d-h*b)/det,(z*a-x*c)/det,(h*a-y*c)/det];
    ctx.save();ctx.beginPath();ctx.moveTo(...u);ctx.lineTo(...v);ctx.lineTo(...w);ctx.closePath();ctx.clip();
    ctx.transform(...m,u[0]-m[0]*p[0]-m[2]*p[1],u[1]-m[1]*p[0]-m[3]*p[1]);
    ctx.drawImage(asset,0,0,640,512);ctx.restore();
  }
  function draw(ctx,asset,amount) {
    if(amount<.0001){ctx.drawImage(asset,0,0,640,512);return;}
    // Tight enough around the neck to keep the head/body transition continuous.
    for(let y=0;y<512;y+=32)for(let x=0;x<640;x+=64){
      const p=[x,y],q=[x+64,y],r=[x+64,y+32],s=[x,y+32];
      const mapped=[p,q,r,s].map(p=>point(...p,amount));
      triangle(ctx,asset,[p,q,r],mapped.slice(0,3));
      triangle(ctx,asset,[p,r,s],[mapped[0],mapped[2],mapped[3]]);
    }
  }
  return {duration,keys,nod,point,rod,transform,multiply,draw};
})();
