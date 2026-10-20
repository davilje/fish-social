import assert from 'node:assert/strict';

export async function compositeCatFrame(sharp, manifest, order, readLayer) {
  const scale = Object.hasOwn(manifest, 'bodyTextureScale') ? manifest.bodyTextureScale : 1;
  assert(Number.isInteger(scale) && [1, 2, 4].includes(scale), 'Invalid bodyTextureScale');
  assert.deepEqual(manifest.canvas, [640, 512]);
  const layers = await Promise.all(order.map(async layer => {
    const body = manifest.bodyLayers.includes(layer);
    let input = await readLayer(layer, body);
    const metadata = await sharp(input).metadata();
    const expectedScale = body ? scale : 1;
    assert.equal(metadata.width, 640 * expectedScale, layer + ': source width');
    assert.equal(metadata.height, 512 * expectedScale, layer + ': source height');
    if (!body && scale > 1)
      input = await sharp(input).resize(640 * scale, 512 * scale, { kernel: 'lanczos3' }).png().toBuffer();
    return { input, left: 0, top: 0 };
  }));
  return sharp({ create: { width: 640 * scale, height: 512 * scale, channels: 4, background: '#00000000' } })
    .composite(layers).png().toBuffer();
}
