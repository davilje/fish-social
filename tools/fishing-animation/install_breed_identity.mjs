import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';

const require = createRequire(import.meta.url);
const sharp = require(process.env.SHARP_MODULE || 'sharp');
const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const skin = process.argv[2];
const breeds = JSON.parse(fs.readFileSync(path.join(root, 'docs/art/cat-skins/breed-definitions.json'))).breeds;
if (skin !== 'calico' && !breeds.some(breed => breed.id === skin)) throw new Error('Unknown painted breed');
const fishing = path.join(root, 'fish-social-unity/Assets/StreamingAssets/Fishing', skin);
const manifest = JSON.parse(fs.readFileSync(path.join(fishing, 'manifest.json')));
if (manifest.skinKind !== (skin === 'calico' ? 'painted-pose-assets-v3' : 'painted-pose-assets-v4')) throw new Error('Install complete painted pack before identity assets');
const input = path.join(fishing, 'body/ready/0/head.png');
const { data, info } = await sharp(input).ensureAlpha().raw().toBuffer({ resolveWithObject: true });
let left = info.width, top = info.height, right = -1, bottom = -1;
for (let row = 0; row < info.height; row++) {
  for (let column = 0; column < info.width; column++) {
    if (data[(row * info.width + column) * 4 + 3] < 128) continue;
    left = Math.min(left, column); right = Math.max(right, column);
    top = Math.min(top, row); bottom = Math.max(bottom, row);
  }
}
if (right < left || bottom < top) throw new Error('Empty portrait head');
const portrait = await sharp(input).extract({ left, top, width: right - left + 1, height: bottom - top + 1 })
  .resize(240, 240, { fit: 'contain', background: '#00000000' })
  .extend({ top: 8, bottom: 8, left: 8, right: 8, background: '#00000000' }).png().toBuffer();
const avatars = path.join(root, 'fish-social-unity/Assets/Resources/Desktop/Theme/Cozy/characters/avatars');
fs.writeFileSync(path.join(root, 'image/profile', `cat_avatar_${skin}.png`), portrait);
fs.writeFileSync(path.join(avatars, skin + '.png'), portrait);
const overlayAvatars = path.join(root, 'desktop-overlay/OverlayResources/hud/cozy/characters/avatars');
fs.mkdirSync(overlayAvatars, { recursive: true });
fs.writeFileSync(path.join(overlayAvatars, skin + '.png'), portrait);
const meta = path.join(avatars, skin + '.png.meta');
if (!fs.existsSync(meta)) {
  const template = fs.readFileSync(path.join(avatars, 'orange.png.meta'), 'utf8');
  fs.writeFileSync(meta, template.replace(/^guid: .*$/m, 'guid: ' + crypto.randomBytes(16).toString('hex')));
}
fs.copyFileSync(path.join(root, `docs/art/cat-skins/${skin}-full/catch-04.png`),
  path.join(root, `image/fishing_photos/cat_fishing_${skin}.png`));
console.log(JSON.stringify({ skin, portraitSource: 'ready/0/head.png', avatarSize: 256 }));
