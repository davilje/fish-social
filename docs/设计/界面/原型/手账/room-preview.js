'use strict';

// The existing cat becomes the single room companion; page rendering stays in app.js.
(() => {
  const stage = document.getElementById('stage');
  const background = document.getElementById('bg');
  const indoor = document.getElementById('room-bg-indoor');
  const outdoor = document.getElementById('room-bg-outdoor');
  const actor = document.getElementById('corner-cat');
  const art = actor.querySelector('.room-cat-art');
  const bubble = document.getElementById('room-cat-bubble');
  const floor = document.getElementById('room-floor');
  const tackle = document.getElementById('room-tackle');
  const aquarium = document.getElementById('room-aquarium');
  const scene = document.getElementById('room-scene');
  const layerToggle = document.getElementById('room-layers-toggle');

  // Individual alpha PNGs and placements are shared with the exported asset manifest.
  window.roomAssetsReady = fetch('assets/room-runtime/manifest.json').then(response => {
    if (!response.ok) throw new Error(`Room asset manifest: ${response.status}`);
    return response.json();
  }).then(async manifest => {
    const images = manifest.placements.map(item => {
      const img = document.createElement('img');
      img.className = 'room-prop';
      img.dataset.component = item.asset;
      img.dataset.instance = item.id;
      img.alt = '';
      img.src = `assets/room-runtime/${manifest.assets[item.asset].file}`;
      Object.assign(img.style, {
        left: `${item.x}px`, top: `${item.y}px`, width: `${item.width}px`,
        height: `${item.height}px`, zIndex: String(item.layer),
        objectFit: item.fit || 'contain',
        transform: item.rotation ? `rotate(${item.rotation}deg)` : '',
        transformOrigin: '50% 100%',
      });
      scene.append(img);
      bindProp(img, item);
      return img;
    });
    await Promise.all(images.map(img => img.decode()));
    restoreFurniture();
    scene.dataset.ready = 'true';
    return manifest;
  }).catch(error => {
    scene.dataset.ready = 'error';
    console.error(error);
    caption('房间物件加载失败，请刷新预览。');
    throw error;
  });
  layerToggle.addEventListener('click', () => {
    scene.hidden = !scene.hidden;
    stage.classList.toggle('room-layers-hidden', scene.hidden);
    layerToggle.textContent = scene.hidden ? '物件：隐藏' : '物件：显示';
    layerToggle.setAttribute('aria-pressed', String(!scene.hidden));
  });
  let isIndoor = true, seated = false, motion = null, steps = null, drag = null;
  let point = { x: 835, y: 742 }, bubbleTimer, idleSince = performance.now();
  let nextStroll = performance.now() + 12000;
  let bedState = 'free', propDrag = null;
  const bedFront = document.createElement('div');
  bedFront.id = 'room-bed-front'; bedFront.className = 'room-only'; bedFront.hidden = true;
  actor.after(bedFront);
  const furnitureBounds = {
    'cat-bed': [170, 675, 370, 785], 'cat-climber': [180, 600, 350, 665], 'food-bowls': [1430, 660, 1580, 785]
  };
  const prop = id => scene.querySelector(`[data-instance="${id}"]`);
  const propBox = img => ({ x: parseFloat(img.style.left), y: parseFloat(img.style.top), w: parseFloat(img.style.width), h: parseFloat(img.style.height) });
  const bedDestination = () => { const b = propBox(prop('cat-bed')); return { x: b.x + b.w / 2, y: b.y + b.h * .71 }; };
  const media = matchMedia('(prefers-reduced-motion: reduce)');
  const calm = () => reduced || media.matches;
  const active = () => isIndoor && !S.open && !document.hidden;
  const clamp = p => ({ x: Math.max(580, Math.min(1070, p.x)), y: Math.max(690, Math.min(775, p.y)) });
  const dimensions = () => {
    if (bedState === 'sleeping') { const w = propBox(prop('cat-bed')).w * .64; return { w, h: w * 175 / 293 }; }
    return seated ? { w: 176, h: 204 } : { w: 172, h: 246 };
  };

  function place(p, allowOutside = false) {
    point = allowOutside || bedState !== 'free' ? p : clamp(p);
    const { w, h } = dimensions();
    actor.style.left = `${point.x - w / 2}px`;
    actor.style.top = `${point.y - h}px`;
    actor.style.width = `${w}px`; actor.style.height = `${h}px`;
    if (bedState === 'sleeping') {
      const b = propBox(prop('cat-bed'));
      Object.assign(bedFront.style, { left: `${b.x}px`, top: `${b.y + b.h * .65}px`, width: `${b.w}px`, height: `${b.h * .35}px`, backgroundImage: `url(${prop('cat-bed').src})`, backgroundSize: `${b.w}px ${b.h}px` });
    }
    bedFront.hidden = bedState !== 'sleeping';
  }
  function setBedState(value) { bedState = value; actor.classList.toggle('is-sleeping', value === 'sleeping'); bedFront.hidden = value !== 'sleeping'; }
  function sleepInBed() {
    if (!active()) return;
    interact(); stop(); setBedState('going'); walkTo(bedDestination(), false, true);
    caption('点击猫窝：小橘去窝里休息；猫窝、猫碗和猫爬架可在各自区域拖放。');
  }
  function setFurniture(img, center) {
    const bounds = furnitureBounds[img.dataset.instance], b = propBox(img);
    const p = { x: Math.max(bounds[0], Math.min(bounds[2], center.x)), y: Math.max(bounds[1], Math.min(bounds[3], center.y)) };
    img.style.left = `${p.x-b.w/2}px`; img.style.top = `${p.y-b.h}px`;
    if (img.dataset.instance === 'cat-bed' && bedState !== 'free') { stop(); setBedState('sleeping'); place(bedDestination()); }
  }
  function saveFurniture() {
    const data = Object.keys(furnitureBounds).map(id => { const b = propBox(prop(id)); return { id, x: b.x+b.w/2, y: b.y+b.h }; });
    try { localStorage.setItem('fish-room-layout-v1', JSON.stringify(data)); } catch (_) { /* Private browsing may disable storage. */ }
  }
  function restoreFurniture() {
    try {
      const data = JSON.parse(localStorage.getItem('fish-room-layout-v1') || '[]');
      if (Array.isArray(data)) for (const p of data) if (furnitureBounds[p.id] && Number.isFinite(p.x) && Number.isFinite(p.y)) setFurniture(prop(p.id), p);
    } catch (_) { /* Invalid local layouts keep the authored positions. */ }
  }
  function cancelPropDrag() {
    if (!propDrag) return;
    const d = propDrag; propDrag = null; setFurniture(d.img, d.origin);
    if (d.img.hasPointerCapture(d.id)) d.img.releasePointerCapture(d.id);
  }
  function bindProp(img, item) {
    const movable = Boolean(furnitureBounds[item.id]), rod = item.asset === 'fishing-rod';
    if (!movable && !rod) return;
    img.draggable = false; img.tabIndex = 0; img.setAttribute('role', 'button');
    img.classList.add('is-interactive'); if (movable) img.classList.add('is-movable');
    img.alt = movable ? `${item.id === 'cat-bed' ? '猫窝：点击休息；' : ''}${item.id === 'food-bowls' ? '猫碗' : item.id === 'cat-climber' ? '猫爬架' : '猫窝'}，拖动或用方向键摆放` : '鱼竿：打开装备页';
    img.addEventListener('click', e => {
      if (!active() || img.dataset.dragged === 'true') { img.dataset.dragged = 'false'; return; }
      if (rod) go('equip'); else if (item.id === 'cat-bed') sleepInBed();
    });
    img.addEventListener('keydown', e => {
      if (!active()) return;
      if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); img.click(); }
      const step = { ArrowLeft: [-20, 0], ArrowRight: [20, 0], ArrowUp: [0, -15], ArrowDown: [0, 15] }[e.key];
      if (movable && step) { e.preventDefault(); const b = propBox(img); setFurniture(img, { x: b.x+b.w/2+step[0], y: b.y+b.h+step[1] }); saveFurniture(); }
    });
    if (!movable) return;
    img.addEventListener('pointerdown', e => {
      if (!active() || e.button !== 0) return;
      cancelPropDrag(); const p = stagePoint(e), b = propBox(img), origin = { x: b.x+b.w/2, y: b.y+b.h };
      propDrag = { img, id: e.pointerId, start: p, origin, offset: { x: origin.x-p.x, y: origin.y-p.y }, moved: false };
      img.dataset.dragged = 'false'; img.setPointerCapture(e.pointerId);
    });
    img.addEventListener('pointermove', e => {
      if (!propDrag || propDrag.img !== img || propDrag.id !== e.pointerId) return;
      const p = stagePoint(e), d = propDrag;
      if (Math.hypot(p.x-d.start.x, p.y-d.start.y) > 5) d.moved = true;
      if (d.moved) setFurniture(img, { x: p.x+d.offset.x, y: p.y+d.offset.y });
    });
    img.addEventListener('pointerup', e => {
      if (!propDrag || propDrag.id !== e.pointerId) return;
      const moved = propDrag.moved; propDrag = null; img.dataset.dragged = String(moved);
      if (img.hasPointerCapture(e.pointerId)) img.releasePointerCapture(e.pointerId);
      if (moved) saveFurniture();
    });
    img.addEventListener('pointercancel', cancelPropDrag);
  }
  function stop() {
    if (motion) {
      const cs = getComputedStyle(actor), { w, h } = dimensions();
      const current = { x: parseFloat(cs.left) + w / 2, y: parseFloat(cs.top) + h };
      motion.cancel(); motion = null;
      place(current);
    }
    steps?.cancel(); steps = null;
    actor.classList.remove('is-walking');
  }
  function pose(sit) {
    seated = sit;
    actor.classList.toggle('is-sitting', sit);
    actor.dataset.pose = sit ? 'sitting' : 'standing';
    place(point, true);
  }
  function say(message) {
    bubble.textContent = message;
    bubble.classList.add('visible');
    clearTimeout(bubbleTimer);
    bubbleTimer = setTimeout(() => bubble.classList.remove('visible'), 2400);
  }
  function interact() {
    wake(); idleSince = performance.now(); nextStroll = idleSince + 12000;
  }
  function rest() {
    stop(); pose(true);
    caption('小橘坐下休息了；点它、点地板或拖动即可继续活动。');
    say('在家歇一会儿～');
  }
  function stand() { stop(); setBedState('free'); pose(false); }
  function walkTo(target, manual = true, toBed = false) {
    if (!active()) return;
    if (manual) interact();
    stop(); pose(false);
    const start = { ...point }, dest = toBed ? target : clamp(target);
    const distance = Math.hypot(dest.x - start.x, dest.y - start.y);
    place(dest);
    if (distance < 2 || calm()) { if (toBed) { setBedState('sleeping'); pose(false); place(dest); } return; }
    const { w, h } = dimensions();
    const travel = actor.animate([
      { left: `${start.x - w / 2}px`, top: `${start.y - h}px` },
      { left: `${dest.x - w / 2}px`, top: `${dest.y - h}px` }
    ], { duration: Math.max(350, distance / 170 * 1000), easing: 'ease-in-out' });
    motion = travel;
    actor.classList.add('is-walking');
    steps = art.animate([{ transform: 'translateY(0) rotate(-1deg)' }, { transform: 'translateY(-3px) rotate(1deg)' }, { transform: 'translateY(0) rotate(-1deg)' }], { duration: 330, iterations: Infinity });
    travel.finished.then(() => {
      if (motion !== travel) return;
      motion = null; steps?.cancel(); steps = null; actor.classList.remove('is-walking');
      if (toBed) { setBedState('sleeping'); pose(false); place(dest); }
    }).catch(() => {});
  }
  function stagePoint(event) {
    const rect = stage.getBoundingClientRect();
    return { x: (event.clientX - rect.left) * 1672 / rect.width, y: (event.clientY - rect.top) * 941 / rect.height };
  }
  floor.addEventListener('click', e => {
    walkTo(e.detail === 0 ? { x: 835, y: 742 } : stagePoint(e));
    caption('点击地板：小橘移动到指定位置。也可以拖动小橘，或聚焦后用方向键移动、R 坐下。');
  });
  actor.addEventListener('pointerdown', e => {
    if (!active() || e.button !== 0) return;
    interact(); stop(); pose(false);
    const p = stagePoint(e);
    drag = { id: e.pointerId, start: p, offset: { x: point.x - p.x, y: point.y - p.y }, moved: false };
    actor.setPointerCapture(e.pointerId);
  });
  actor.addEventListener('pointermove', e => {
    if (!drag || drag.id !== e.pointerId) return;
    const p = stagePoint(e);
    if (Math.hypot(p.x - drag.start.x, p.y - drag.start.y) > 5) drag.moved = true;
    if (drag.moved) place({ x: p.x + drag.offset.x, y: p.y + drag.offset.y });
  });
  function finishDrag(e) {
    if (!drag || drag.id !== e.pointerId) return;
    const moved = drag.moved;
    if (actor.hasPointerCapture(e.pointerId)) actor.releasePointerCapture(e.pointerId);
    drag = null;
    actor.dataset.dragged = moved ? 'true' : 'false';
    if (moved) { interact(); caption('小橘的位置已调整；活动范围限定在中央地板，避开家具和底部导航。'); }
  }
  actor.addEventListener('pointerup', finishDrag);
  actor.addEventListener('pointercancel', e => { finishDrag(e); });
  actor.addEventListener('click', e => {
    if (!active()) return;
    if (actor.dataset.dragged === 'true' && e.detail !== 0) { actor.dataset.dragged = 'false'; return; }
    interact(); stop(); pose(false); catNod(); say('今天也一起钓鱼吧！');
  });
  actor.addEventListener('keydown', e => {
    if (!active()) return;
    const directions = { ArrowLeft: [-60, 0], ArrowRight: [60, 0], ArrowUp: [0, -30], ArrowDown: [0, 30] };
    if (directions[e.key]) {
      e.preventDefault(); stop();
      const [x, y] = directions[e.key]; walkTo({ x: point.x + x, y: point.y + y });
    } else if (e.key.toLowerCase() === 'r') { e.preventDefault(); interact(); rest(); }
    else if (e.key === 'Home') { e.preventDefault(); walkTo({ x: 835, y: 742 }); }
  });
  tackle.addEventListener('click', () => go('equip'));
  aquarium.addEventListener('click', () => go('profile', { tab: 1 }));

  function syncPage() {
    stage.classList.toggle('room-page-open', S.open);
    scene.inert = !active();
    for (const el of [actor, floor, tackle, aquarium]) el.inert = !active();
    if (!active()) { cancelPropDrag(); stop(); drag = null; bubble.classList.remove('visible'); if (bedState === 'going') { setBedState('sleeping'); pose(false); place(bedDestination()); } }
    else if (!seated) caption('小橘在房间中央活动：点地板移动，拖动调整位置，点小橘互动；方向键移动，R 坐下。');
    nextStroll = performance.now() + 12000;
  }
  function setRoomBackground(value) {
    cancelPropDrag(); stop(); setBedState('free'); isIndoor = value;
    // Clear outdoor-only persistent poses before switching the same actor indoors.
    zzzLoop.cancel(); actor._sleep?.cancel(); actor._lean?.cancel(); sleeping = false;
    background.src = value ? 'assets/room-runtime/room-base.jpg' : 'assets/home-sakura.jpg';
    background.alt = value ? '均匀光照的空房间底图，用品由独立透明切图叠放' : '原有樱花湖畔背景';
    layerToggle.disabled = !value;
    stage.classList.toggle('room-mode', value);
    indoor.classList.toggle('on', value); outdoor.classList.toggle('on', !value);
    indoor.setAttribute('aria-pressed', String(value)); outdoor.setAttribute('aria-pressed', String(!value));
    document.querySelector('[data-demo="sleep"]').textContent = value ? '小猫休息' : '小猫睡着';
    if (value) { pose(false); place(point); }
    else { actor.style.left = ''; actor.style.top = ''; actor.style.width = ''; actor.style.height = ''; }
    if (!S.open) startCatBreath();
    syncPage();
  }
  window.roomCompanion = { get active() { return isIndoor; }, rest, wake: stand };
  indoor.addEventListener('click', () => setRoomBackground(true));
  outdoor.addEventListener('click', () => setRoomBackground(false));
  document.getElementById('room-home').addEventListener('click', () => closePage());
  new MutationObserver(syncPage).observe(document.getElementById('page'), { attributes: true, attributeFilter: ['style'] });
  document.addEventListener('visibilitychange', syncPage);
  window.addEventListener('blur', cancelPropDrag);
  function reduceMotion() { if (calm()) stop(); }
  document.getElementById('reduce').addEventListener('click', () => queueMicrotask(reduceMotion));
  media.addEventListener('change', reduceMotion);
  setInterval(() => {
    if (!active() || calm() || seated || bedState !== 'free' || propDrag || drag || motion || performance.now() < nextStroll) return;
    nextStroll = performance.now() + 12000;
    walkTo({ x: 650 + Math.random() * 350, y: 710 + Math.random() * 50 }, false);
  }, 1000);
  setRoomBackground(true);
  const ready = new Promise(resolve => {
    if (document.readyState !== 'loading') resolve();
    else document.addEventListener('DOMContentLoaded', resolve, { once: true });
  });
  Promise.all([document.fonts.load('20px FishCozy').catch(() => {}), ready]).then(() => {
    if (new URLSearchParams(location.search).get('roomView') === 'home') requestAnimationFrame(() => closePage());
  });
})();
