import fs from 'node:fs';
import path from 'node:path';
import { createRequire } from 'node:module';

const require = createRequire(import.meta.url);
const sharp = require('/Users/red/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const repoRoot = path.resolve(new URL('.', import.meta.url).pathname, '../..');
const studyRoot = path.join(repoRoot, 'docs/art/cat-skins');
const unityFishingRoot = path.join(repoRoot, 'fish-social-unity/Assets/StreamingAssets/Fishing');
const overlayFishingRoot = path.join(repoRoot, 'desktop-overlay/OverlayResources/fishing');
const sourceManifest = JSON.parse(fs.readFileSync(path.join(unityFishingRoot, 'orange/manifest.json'), 'utf8'));
const poseManifest = JSON.parse(fs.readFileSync(path.join(studyRoot, 'full-pose-manifest.json'), 'utf8'));
const poseByKey = new Map(poseManifest.frames.map(frame => [`${frame.clip}:${frame.index}`, frame]));
const bodyLayers = ['body', 'head', 'far_hand', 'hands'];

function clamp(value, minimum, maximum) {
  return Math.max(minimum, Math.min(maximum, value));
}

function smoothstep(edge0, edge1, value) {
  const amount = clamp((value - edge0) / (edge1 - edge0), 0, 1);
  return amount * amount * (3 - 2 * amount);
}

function patternValue(x, y, seed) {
  return Math.sin(x * 17.3 + y * 9.1 + seed * 0.37) * 0.5 +
    Math.sin(x * 6.7 - y * 13.2 + seed * 0.19) * 0.5;
}

function furColor(original, patch, lightness) {
  if (patch === 'orange') {
    return [clamp(245 + lightness, 220, 255), clamp(143 + lightness, 105, 190), clamp(30 + lightness, 15, 75)];
  }
  if (patch === 'black') {
    return [clamp(66 + lightness, 42, 105), clamp(62 + lightness, 38, 98), clamp(69 + lightness, 42, 108)];
  }
  return [clamp(250 + lightness, 225, 255), clamp(246 + lightness, 220, 255), clamp(226 + lightness, 202, 245)];
}

function paintCalico(raw, width, height, seed) {
  const pixels = Buffer.from(raw.data);
  let minimumX = width;
  let minimumY = height;
  let maximumX = 0;
  let maximumY = 0;
  for (let offset = 0; offset < pixels.length; offset += 4) {
    if (pixels[offset + 3] > 12) {
      const pixel = offset / 4;
      const x = pixel % width;
      const y = Math.floor(pixel / width);
      minimumX = Math.min(minimumX, x);
      minimumY = Math.min(minimumY, y);
      maximumX = Math.max(maximumX, x);
      maximumY = Math.max(maximumY, y);
    }
  }
  const boxWidth = Math.max(1, maximumX - minimumX);
  const boxHeight = Math.max(1, maximumY - minimumY);
  for (let offset = 0; offset < pixels.length; offset += 4) {
    const alpha = pixels[offset + 3];
    if (alpha < 12) continue;
    const red = pixels[offset];
    const green = pixels[offset + 1];
    const blue = pixels[offset + 2];
    const darkest = Math.max(red, green, blue) < 82;
    const warmFur = red > 105 && red > green * 1.12 && green > blue * 1.28 && !darkest;
    if (!warmFur) continue;
    const pixel = offset / 4;
    const x = pixel % width;
    const y = Math.floor(pixel / width);
    const normalizedX = (x - minimumX) / boxWidth;
    const normalizedY = (y - minimumY) / boxHeight;
    const irregular = patternValue(normalizedX, normalizedY, seed) * 0.035;
    let patch = 'white';
    if (normalizedY < 0.43) {
      const orangeEdge = 0.42 + 0.055 * Math.sin(normalizedY * 8.0 + seed * 0.01) + irregular;
      const blackEdge = 0.61 + 0.075 * Math.sin(normalizedY * 6.0 + 0.8) + irregular;
      if (normalizedX < orangeEdge) patch = 'orange';
      else if (normalizedX > blackEdge) patch = 'black';
    } else if (normalizedY < 0.83) {
      const orangeEdge = 0.34 + 0.11 * Math.sin(normalizedY * 7.0 + 0.4) + irregular;
      if (normalizedX < orangeEdge) patch = 'orange';
      else if (normalizedX > 0.43 + 0.08 * Math.sin(normalizedY * 5.0 + 1.1) + irregular &&
        normalizedX < 0.64 + 0.05 * Math.sin(normalizedY * 4.0) && normalizedY > 0.52) patch = 'black';
    } else if (normalizedX < 0.34 + irregular) {
      patch = 'orange';
    }
    const originalLightness = Math.round((red + green + blue) / 3 - 155) * 0.14;
    const color = furColor([red, green, blue], patch, originalLightness);
    pixels[offset] = color[0];
    pixels[offset + 1] = color[1];
    pixels[offset + 2] = color[2];
  }
  return pixels;
}

async function readRaw(filename) {
  return sharp(filename).ensureAlpha().raw().toBuffer({ resolveWithObject: true });
}

async function writeLayer(filename, sourcePixels, maskPixels, width, height) {
  const pixels = Buffer.from(sourcePixels);
  for (let offset = 0; offset < pixels.length; offset += 4) {
    pixels[offset + 3] = Math.min(pixels[offset + 3], maskPixels[offset + 3]);
  }
  await sharp(pixels, { raw: { width, height, channels: 4 } }).png().toFile(filename);
}

async function buildPose(frame, destinationRoot) {
  const source = path.join(studyRoot, frame.sourceCharacter);
  const original = await readRaw(source);
  const calico = paintCalico(original, original.info.width, original.info.height, frame.atlasIndex);
  const fullDestination = path.join(studyRoot, 'calico-full', `${frame.id}.png`);
  fs.mkdirSync(path.dirname(fullDestination), { recursive: true });
  await sharp(calico, { raw: original.info }).png().toFile(fullDestination);
  const masks = {};
  for (const layer of bodyLayers) {
    masks[layer] = await readRaw(path.join(studyRoot, frame.layers[layer]));
  }
  const runtimeFrames = [];
  for (const [runtimeClip, clip] of Object.entries(sourceManifest.clips)) {
    clip.sourceFrames.forEach((sourceFrame, runtimeIndex) => {
      if (sourceFrame.clip === frame.clip && sourceFrame.index === frame.index) {
        runtimeFrames.push([runtimeClip, runtimeIndex]);
      }
    });
  }
  for (const [runtimeClip, runtimeIndex] of runtimeFrames) {
    for (const layer of bodyLayers) {
      const destination = path.join(destinationRoot, 'calico', 'body', runtimeClip, String(runtimeIndex), `${layer}.png`);
      fs.mkdirSync(path.dirname(destination), { recursive: true });
      await writeLayer(destination, calico, masks[layer].data, original.info.width, original.info.height);
    }
  }
}

async function writeCharacterPreview(frame, destinationRoot) {
  const runtimeLocation = [];
  for (const [clip, definition] of Object.entries(sourceManifest.clips)) {
    definition.sourceFrames.forEach((sourceFrame, index) => {
      if (sourceFrame.clip === frame.clip && sourceFrame.index === frame.index) {
        runtimeLocation.push([clip, index]);
      }
    });
  }
  const [clip, index] = runtimeLocation[0];
  const composites = [];
  for (const layer of frame.renderLayerOrder) {
    const filename = bodyLayers.includes(layer)
      ? path.join(destinationRoot, 'calico', 'body', clip, String(index), `${layer}.png`)
      : path.join(destinationRoot, 'shared', clip, String(index), `${layer}.png`);
    composites.push({ input: filename });
  }
  const output = path.join(studyRoot, 'calico-full', `${frame.id}.png`);
  fs.mkdirSync(path.dirname(output), { recursive: true });
  await sharp({
    create: { width: 640, height: 512, channels: 4, background: { r: 0, g: 0, b: 0, alpha: 0 } },
  }).composite(composites).png().toFile(output);
}

async function buildRoot(destinationRoot) {
  for (const frame of poseManifest.frames) await buildPose(frame, destinationRoot);
  const manifest = { ...sourceManifest, skinId: 'calico', skinName: '三花猫', skinKind: 'whole-pose-painted-material-v1' };
  fs.mkdirSync(path.join(destinationRoot, 'calico'), { recursive: true });
  fs.writeFileSync(path.join(destinationRoot, 'calico/manifest.json'), `${JSON.stringify(manifest, null, 2)}\n`);
}

await buildRoot(unityFishingRoot);
await buildRoot(overlayFishingRoot);
for (const frame of poseManifest.frames) await writeCharacterPreview(frame, unityFishingRoot);
console.log(JSON.stringify({ sourceFrames: poseManifest.frames.length, runtimeFrames: sourceManifest.sourceFrameCount, skin: 'calico' }));
