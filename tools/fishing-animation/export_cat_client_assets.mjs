import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';

const require = createRequire(import.meta.url);
const sharp = require(process.env.SHARP_MODULE || 'sharp');
const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const source = path.join(root, 'fish-social-unity/Assets/StreamingAssets/Fishing');
const output = path.join(root, 'image/cats');
const cozy = path.join(root, 'fish-social-unity/Assets/Resources/Desktop/Theme/Cozy');
const skins = JSON.parse(fs.readFileSync(path.join(source, 'inventory.json'))).skins.map(skin => skin.id);
const catalog = { version: 1, frameWidth: 320, frameHeight: 256, foot: [264, 450], skins, clips: {} };
fs.mkdirSync(output, { recursive: true });

async function composite(manifest, skin, clip, index) {
  const layers = manifest.clips[clip].frames[index].map(layer => ({
    input: path.join(source, manifest.bodyLayers.includes(layer)
      ? `${skin}/body/${clip}/${index}/${layer}.png` : `shared/${clip}/${index}/${layer}.png`),
    left: 0, top: 0,
  }));
  return sharp({ create: { width: 640, height: 512, channels: 4, background: '#00000000' } })
    .composite(layers).png().toBuffer();
}

for (const skin of skins) {
  const manifest = JSON.parse(fs.readFileSync(path.join(source, skin, 'manifest.json')));
  fs.mkdirSync(path.join(output, skin), { recursive: true });
  for (const [clip, definition] of Object.entries(manifest.clips)) {
    const frames = [];
    for (const [index] of definition.frames.entries()) {
      const png = await composite(manifest, skin, clip, index);
      frames.push({ input: await sharp(png).resize(320, 256).png().toBuffer(), left: index * 320, top: 0 });
    }
    await sharp({ create: { width: frames.length * 320, height: 256, channels: 4, background: '#00000000' } })
      .composite(frames).png().toFile(path.join(output, skin, clip + '.png'));
    catalog.clips[clip] = { count: frames.length, playback: definition.playback,
      durationsMs: definition.durationsMs, loopStart: definition.loopStart || 0, next: definition.nextClip || null };
  }
  if (skin === 'orange') {
    const head = path.join(source, 'orange/body/ready/0/head.png');
    const { data, info } = await sharp(head).ensureAlpha().raw().toBuffer({ resolveWithObject: true });
    let left = info.width, top = info.height, right = -1, bottom = -1;
    for (let row = 0; row < info.height; row++) {
      for (let column = 0; column < info.width; column++) {
        if (data[(row * info.width + column) * 4 + 3] < 128) continue;
        left = Math.min(left, column); right = Math.max(right, column);
        top = Math.min(top, row); bottom = Math.max(bottom, row);
      }
    }
    const avatar = await sharp(head).extract({ left, top, width: right - left + 1, height: bottom - top + 1 })
      .resize(240, 240, { fit: 'contain', background: '#00000000' })
      .extend({ top: 8, bottom: 8, left: 8, right: 8, background: '#00000000' }).png().toBuffer();
    fs.writeFileSync(path.join(root, 'image/profile/cat_avatar_orange.png'), avatar);
    fs.writeFileSync(path.join(cozy, 'characters/avatars/orange.png'), avatar);
    const ready = await composite(manifest, skin, 'ready', 0);
    const poses = path.join(cozy, 'characters/poses');
    fs.mkdirSync(poses, { recursive: true });
    fs.writeFileSync(path.join(poses, 'orange-ready.png'), ready);
    const oldMeta = fs.readFileSync(path.join(cozy, 'characters/avatars/orange.png.meta'), 'utf8');
    if (!fs.existsSync(path.join(poses, 'orange-ready.png.meta')))
      fs.writeFileSync(path.join(poses, 'orange-ready.png.meta'), oldMeta.replace(/^guid: .*$/m,
        'guid: ' + crypto.createHash('md5').update('cat-client/orange-ready').digest('hex')));
    fs.writeFileSync(path.join(root, 'image/fishing_photos/cat_fishing_orange.png'),
      await composite(manifest, skin, 'catch', 4));
  }
  console.log(skin + ': ' + Object.keys(manifest.clips).length + ' clips exported');
}
const serialized = JSON.stringify(catalog, null, 2) + '\n';
fs.writeFileSync(path.join(output, 'catalog.json'), serialized);
fs.mkdirSync(path.join(root, 'mobile/assets/cats'), { recursive: true });
fs.writeFileSync(path.join(root, 'mobile/assets/cats/catalog.json'), serialized);
