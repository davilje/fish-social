import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const apply = process.argv.includes('--apply');
const unity = path.join(root, 'fish-social-unity/Assets');
const cozy = path.join(unity, 'Resources/Desktop/Theme/Cozy');
const overlay = path.join(root, 'desktop-overlay/OverlayResources');
const skins = ['orange', 'calico', 'silver', 'brown', 'cream', 'blue', 'black', 'white', 'cow'];
const retired = ['gray', 'siamese', 'tuxedo'];
const removed = [];
const retained = [];
const files = directory => fs.existsSync(directory) ? fs.readdirSync(directory, { withFileTypes: true })
  .flatMap(entry => entry.isDirectory() ? files(path.join(directory, entry.name)) : [path.join(directory, entry.name)]) : [];
const references = files(unity).filter(file => /\.(prefab|unity|asset)$/.test(file))
  .map(file => ({ file, text: fs.readFileSync(file, 'utf8') }));

function remove(file) {
  if (!fs.existsSync(file)) return;
  const meta = file + '.meta';
  if (!file.endsWith('.meta') && fs.existsSync(meta)) {
    const guid = fs.readFileSync(meta, 'utf8').match(/^guid: (\w+)/m)?.[1];
    const used = guid && references.filter(reference => reference.text.includes(guid));
    if (used?.length) {
      retained.push({ file: path.relative(root, file), references: used.map(reference => path.relative(root, reference.file)) });
      return;
    }
  }
  removed.push({ file: path.relative(root, file), bytes: fs.statSync(file).size });
  if (apply) fs.unlinkSync(file);
  if (!file.endsWith('.meta')) remove(meta);
}

for (const directory of [path.join(unity, 'StreamingAssets/Pet'), path.join(overlay, 'pets')]) {
  for (const file of files(directory).filter(file => !file.endsWith('.meta'))) remove(file);
}
for (const id of retired) {
  remove(path.join(root, `image/profile/cat_avatar_${id}.png`));
  remove(path.join(root, `image/fishing_photos/cat_fishing_${id}.png`));
  remove(path.join(cozy, `characters/avatars/${id}.png`));
}
for (const directory of [path.join(unity, 'StreamingAssets/Fishing'), path.join(overlay, 'fishing')]) {
  const expected = new Set();
  for (const skin of skins) {
    const manifest = JSON.parse(fs.readFileSync(path.join(directory, skin, 'manifest.json')));
    for (const [clip, definition] of Object.entries(manifest.clips)) {
      for (const [index] of definition.frames.entries()) {
        for (const layer of manifest.layers) {
          expected.add(path.join(directory, manifest.bodyLayers.includes(layer)
            ? `${skin}/body/${clip}/${index}/${layer}.png` : `shared/${clip}/${index}/${layer}.png`));
        }
      }
    }
  }
  for (const file of files(directory).filter(file => file.endsWith('.png'))) {
    if (!expected.has(file)) remove(file);
  }
  for (const file of expected) {
    if (!fs.existsSync(file)) throw new Error('Missing active layer: ' + file);
  }
  if (apply) {
    const filename = path.join(directory, 'inventory.json');
    const inventory = JSON.parse(fs.readFileSync(filename));
    const names = ['橘猫', '三花猫', '银渐层', '棕虎斑', '奶油猫', '英短蓝猫', '黑猫', '白猫', '奶牛猫'];
    inventory.skins = skins.map((id, index) => ({ id, name: names[index] }));
    inventory.note = 'Nine current layered skins. Orange baseline, calico v3, seven painted v4 packs. Retired flat art is not loaded.';
    fs.writeFileSync(filename, JSON.stringify(inventory, null, 2) + '\n');
  }
}
for (const directory of [cozy, path.join(overlay, 'hud/cozy')]) {
  const candidates = files(path.join(directory, 'characters')).filter(file =>
    file.endsWith('.png') && !file.includes('/avatars/') && !file.includes('/poses/'));
  for (const file of candidates) remove(file);
}

if (apply) {
  for (const directory of [cozy, path.join(overlay, 'hud/cozy')]) {
    fs.mkdirSync(path.join(directory, 'characters/avatars'), { recursive: true });
    for (const skin of skins) {
      fs.copyFileSync(path.join(root, `image/profile/cat_avatar_${skin}.png`),
        path.join(directory, 'characters/avatars', skin + '.png'));
    }
    const filename = path.join(directory, 'asset-manifest.json');
    const manifest = JSON.parse(fs.readFileSync(filename));
    for (const key of Object.keys(manifest.spriteAssets)) {
      if (!fs.existsSync(path.join(directory, key))) delete manifest.spriteAssets[key];
    }
    for (const skin of skins) {
      const key = `characters/avatars/${skin}.png`;
      const bytes = fs.readFileSync(path.join(directory, key));
      const meta = fs.readFileSync(path.join(cozy, key + '.meta'), 'utf8');
      manifest.spriteAssets[key] = { guid: meta.match(/^guid: (\w+)/m)[1], fileID: 21300000,
        size: [bytes.readUInt32BE(16), bytes.readUInt32BE(20)], border: [0, 0, 0, 0],
        resourcesPath: `Desktop/Theme/Cozy/characters/avatars/${skin}` };
    }
    for (const filename of files(path.join(directory, 'characters/poses')).filter(file => file.endsWith('.png'))) {
      const key = path.relative(directory, filename);
      const bytes = fs.readFileSync(filename);
      const meta = fs.readFileSync(filename + '.meta', 'utf8');
      manifest.spriteAssets[key] = { guid: meta.match(/^guid: (\w+)/m)[1], fileID: 21300000,
        size: [bytes.readUInt32BE(16), bytes.readUInt32BE(20)], border: [0, 0, 0, 0],
        resourcesPath: 'Desktop/Theme/Cozy/' + key.replace(/\.png$/, '') };
    }
    fs.writeFileSync(filename, JSON.stringify(manifest, null, 2) + '\n');
  }
  const contractPath = path.join(root, 'desktop-overlay/tests/cozy_source_contract.json');
  const contract = JSON.parse(fs.readFileSync(contractPath));
  for (const key of Object.keys(contract.png_sha256)) {
    if (!fs.existsSync(path.join(root, 'desktop-overlay', key))) delete contract.png_sha256[key];
  }
  for (const skin of skins) {
    const key = `OverlayResources/hud/cozy/characters/avatars/${skin}.png`;
    contract.png_sha256[key] = crypto.createHash('sha256').update(fs.readFileSync(path.join(root, 'desktop-overlay', key))).digest('hex');
  }
  fs.writeFileSync(contractPath, JSON.stringify(contract, null, 2) + '\n');
  for (const directory of [path.join(unity, 'StreamingAssets/Pet'), path.join(overlay, 'pets'),
    path.join(cozy, 'characters/portraits'), path.join(overlay, 'hud/cozy/characters/portraits')]) {
    const prune = folder => {
      for (const entry of fs.readdirSync(folder, { withFileTypes: true })) {
        if (entry.isDirectory()) prune(path.join(folder, entry.name));
      }
      if (fs.readdirSync(folder).length === 0) {
        fs.rmdirSync(folder);
        remove(folder + '.meta');
      }
    };
    if (fs.existsSync(directory)) prune(directory);
  }
}
const reportPath = path.join(root, 'docs/art/cat-skins/resource-cleanup.json');
const previous = apply && fs.existsSync(reportPath) ? JSON.parse(fs.readFileSync(reportPath)).removed : [];
const combined = [...new Map([...previous, ...removed].map(file => [file.file, file])).values()];
const report = { applied: apply, removed: combined, retained, bytesRemoved: combined.reduce((sum, file) => sum + file.bytes, 0) };
if (apply) fs.writeFileSync(reportPath, JSON.stringify(report, null, 2) + '\n');
console.log(JSON.stringify({ applied: apply, files: removed.length, bytes: report.bytesRemoved, retained }, null, 2));
