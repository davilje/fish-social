import fs from 'node:fs';
import path from 'node:path';
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';
import { skinParts } from '../geometry.mjs';

const sharp = createRequire(import.meta.url)(process.env.SHARP_MODULE || 'sharp');
const output = path.dirname(fileURLToPath(import.meta.url));
const folder = path.dirname(output);
const registrations = JSON.parse(fs.readFileSync(path.join(folder, 'registration.json')));
const plan = JSON.parse(fs.readFileSync(path.join(folder, 'plan.json')));
const imports = JSON.parse(fs.readFileSync(path.join(folder, 'drawing-imports.json'))).drawings;
const readJson = file => JSON.parse(fs.readFileSync(file));
const measurements = [];
for (const registration of registrations) {
  const stages = {};
  for (const stage of ['painted', 'registered']) {
    const { data, info } = await sharp(path.join(folder, stage, registration.id + '.png'))
      .ensureAlpha().raw().toBuffer({ resolveWithObject: true });
    const face = skinParts(data, registration.id).face;
    let left = info.width, top = info.height, right = -1, bottom = -1;
    for (let row = 0; row < info.height; row++) {
      for (let column = 0; column < info.width; column++) {
        if (data[(row * info.width + column) * 4 + 3] < 128) continue;
        left = Math.min(left, column); right = Math.max(right, column);
        top = Math.min(top, row); bottom = Math.max(bottom, row);
      }
    }
    stages[stage] = { canvas: [info.width, info.height], opaqueBounds: [left, top, right, bottom],
      faceSkinBounds: face.box, faceSkinWidth: face.box[2] - face.box[0] + 1,
      faceSkinHeight: face.box[3] - face.box[1] + 1, faceSkinArea: face.area };
  }
  const imported = imports.find(entry => entry.id === registration.id);
  const source = imported ? path.join(folder, imported.sheet) :
    path.resolve(folder, '../../whale-maid/template-study-v1/generated', registration.reused + '.png');
  const metadata = await sharp(source).metadata();
  measurements.push({ id: registration.id, source: path.relative(output, source),
    sourcePixels: [metadata.width, metadata.height], panels: imported ? 2 : 1,
    scale: registration.scale, offset: registration.offset, retainedKeyPose: registration.reused,
    nearHandError: registration.nearHandError ?? null, farHandError: registration.farHandError ?? null, ...stages });
}
const byId = new Map(measurements.map(entry => [entry.id, entry]));
const range = values => ({ min: Math.min(...values), max: Math.max(...values), maxOverMin: Math.max(...values) / Math.min(...values) });
const transitions = [];
for (const [index, frame] of plan.frames.entries()) {
  const previous = plan.frames[index - 1];
  if (!previous || previous.clip !== frame.clip || previous.drawing === frame.drawing) continue;
  const before = byId.get(previous.drawing), after = byId.get(frame.drawing);
  transitions.push({ from: previous.id, to: frame.id, fromDrawing: previous.drawing, toDrawing: frame.drawing,
    scaleBefore: before.scale, scaleAfter: after.scale,
    faceWidthBefore: before.registered.faceSkinWidth, faceWidthAfter: after.registered.faceSkinWidth,
    faceWidthChangePercent: 100 * (after.registered.faceSkinWidth / before.registered.faceSkinWidth - 1) });
}
const runtime = readJson(path.join(folder, 'pack/dafeiyu/manifest.json'));
const summary = {
  date: '2026-10-20', purpose: 'Diagnosis only; production artwork is unchanged',
  drawings: measurements.length, newDrawings: measurements.filter(entry => !entry.retainedKeyPose).length,
  registrationScale: range(measurements.map(entry => entry.scale)),
  paintedFaceSkinWidth: range(measurements.map(entry => entry.painted.faceSkinWidth)),
  registeredFaceSkinWidth: range(measurements.map(entry => entry.registered.faceSkinWidth)),
  sourceSheetSizes: [...new Set(measurements.filter(entry => !entry.retainedKeyPose).map(entry => entry.sourcePixels.join('x')))],
  runtimeCanvas: runtime.canvas, atlasFrame: [320, 256],
  caveat: 'Face skin detection is a diagnostic proxy, not skull size. Turning, hair occlusion and expression affect it. Scale values are exact build parameters.',
  measurements, transitions,
};
fs.writeFileSync(path.join(output, 'measurements.json'), JSON.stringify(summary, null, 2) + '\n');
const examples = ['catch-03', 'catch-04', 'catch-05', 'catch-06', 'reset_catch-01', 'reset_catch-02', 'sit-03'];
const number = value => Number(value.toFixed(3));
const cell = (entry, stage) => `<div class="frame"><img src="../${stage}/${entry.id}.png" alt="${entry.id} ${stage}"><span class="floor"></span></div>`;
const cards = examples.map(id => {
  const entry = byId.get(id), source = plan.frames.find(frame => frame.drawing === id);
  return `<section><h3>${id}${entry.retainedKeyPose ? ' · 保留关键姿势' : ''}</h3><div class="compare">
  <div><p>原猫咪动作模板</p><div class="frame"><img src="../../../cat-skins/${source.sourceCharacter}" alt="原猫咪"><span class="floor"></span></div></div>
  <div><p>导入稿 · 脸部肤色宽 ${entry.painted.faceSkinWidth}px</p>${cell(entry, 'painted')}</div>
  <div><p>当前最终稿 · 缩放 ${number(entry.scale)} · 脸部肤色宽 ${entry.registered.faceSkinWidth}px</p>${cell(entry, 'registered')}</div>
  </div></section>`;
}).join('');
const rows = measurements.map(entry => `<tr><td>${entry.id}</td><td>${entry.retainedKeyPose ? '旧关键姿势' : '新增稿'}</td><td>${number(entry.scale)}</td><td>${entry.painted.faceSkinWidth}</td><td>${entry.registered.faceSkinWidth}</td><td>${entry.sourcePixels.join(' × ')} / ${entry.panels} 角色</td><td><a href="${entry.source}">原始生成图</a></td></tr>`).join('');
fs.writeFileSync(path.join(output, 'index.html'), `<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><title>大肥鱼 · 尺寸与清晰度排查</title><style>
body{margin:0;background:#edf1f5;color:#20374b;font:15px/1.65 system-ui,sans-serif}main{max-width:1400px;margin:auto;padding:28px}h1{margin:0;font-size:28px}h2{font-size:21px}h3{font-size:17px;margin:12px 0}a{color:#22618c}section,.note{background:white;border:1px solid #cbd6df;border-radius:12px;padding:18px;margin:20px 0}.compare{display:grid;grid-template-columns:repeat(3,1fr);gap:12px}.compare p{min-height:48px;margin:0}.frame{position:relative;background:repeating-conic-gradient(#e8edf1 0% 25%,#f7f9fb 0% 50%) 50%/24px 24px;aspect-ratio:640/512}.frame img{display:block;width:100%;height:100%;object-fit:contain}.floor{position:absolute;left:0;right:0;top:87.89%;border-top:1px dashed #c16655}table{border-collapse:collapse;width:100%;background:white}th,td{text-align:left;padding:8px 12px;border-bottom:1px solid #d5e0e9}th{background:#dce7f0}code{background:#e8eef4;padding:1px 5px}.table{overflow:auto}.warning{color:#9f4937}@media(max-width:800px){.compare{grid-template-columns:1fr}main{padding:16px}}</style><main>
<h1>大肥鱼：尺寸跳变与清晰度诊断</h1><p>2026-10-20 · 44 张姿势稿 / 63 个源帧 / 16 个运行片段。此页为诊断对照，未替换生产素材。</p>
<div class="note"><strong>主要原因：原稿比例不统一，加上逐帧以握点和脚底反算整个人物缩放。</strong><p>实际缩放系数为 ${number(summary.registrationScale.min)}～${number(summary.registrationScale.max)}。六张旧姿势按 1.0 原样通过，新增稿采用另一套规则。以手臂动作来反推整身体型，会使举手、放手动作带动头和躯干一起变大变小。</p><p>脸部肤色连通区域宽度：导入稿 ${summary.paintedFaceSkinWidth.min}～${summary.paintedFaceSkinWidth.max}px；最终稿 ${summary.registeredFaceSkinWidth.min}～${summary.registeredFaceSkinWidth.max}px。这是排查指标，受表情、转头、刘海遮挡影响，不能直接等同于头骨尺寸。下方同底线、同画布对照能直观看到体型变化。</p></div>
<div class="note"><strong>清晰度问题：有效像素不足，并存在多次重采样。</strong><p>新增双角色原图实际为 1983×793，先放大成 2560×1024，再逐格缩小到 640×512，之后又按 0.67～1.06 重采样。脸部最终只有约 69～107 像素宽。网页播放直接加载 640×512 分层图；320×256 条带是另一条客户端输出链路，不是当前网页模糊的直接来源。Unity 还会按显示尺寸进行双线性重采样。</p><p>应统一角色头颅、躯干和落座尺度，在该尺度内重绘手臂与姿势；保留高分辨率主稿，最终目标分辨率只重采样一次。简单将现有 PNG 放大或锐化无法恢复已丢失的眼睛、发丝与衣饰细节。</p></div>
<h2>重点帧对照</h2><p>各栏统一画布比例；虚线是逻辑坐标 y=450。原猫咪只用于对照动作和握点，不用于规定人形角色的头身比。</p>${cards}
<h2>全部 44 张稿的测量</h2><p>脸部宽度列单位为像素；图像框始终为 640×512。<a href="measurements.json">完整数据与相邻帧变化</a> · <a href="../index.html">返回动画播放页</a></p><div class="table"><table><thead><tr><th>姿势</th><th>来源</th><th>整图缩放</th><th>导入脸宽</th><th>最终脸宽</th><th>原图尺寸 / 内容</th><th>原图</th></tr></thead><tbody>${rows}</tbody></table></div></main></html>`);
console.log(JSON.stringify({ drawings: summary.drawings, scale: summary.registrationScale, paintedFace: summary.paintedFaceSkinWidth,
  finalFace: summary.registeredFaceSkinWidth, sourceSheetSizes: summary.sourceSheetSizes }, null, 2));
