export function skinParts(pixels, label = '') {
  const count = 640 * 512;
  const skin = new Uint8Array(count);
  for (let index = 0; index < count; index++) {
    const offset = index * 4;
    skin[index] = pixels[offset + 3] > 180 && pixels[offset] > 155 && pixels[offset] - pixels[offset + 1] > 12 && pixels[offset + 1] - pixels[offset + 2] > 8 ? 1 : 0;
  }
  const parts = [];
  for (let start = 0; start < count; start++) {
    if (!skin[start]) continue;
    skin[start] = 0;
    const queue = [start];
    let left = 640, top = 512, right = -1, bottom = -1, sumX = 0, sumY = 0;
    for (let cursor = 0; cursor < queue.length; cursor++) {
      const index = queue[cursor];
      const horizontal = index % 640, vertical = Math.floor(index / 640);
      left = Math.min(left, horizontal); right = Math.max(right, horizontal);
      top = Math.min(top, vertical); bottom = Math.max(bottom, vertical);
      sumX += horizontal; sumY += vertical;
      for (const neighbor of [horizontal > 0 ? index - 1 : -1, horizontal < 639 ? index + 1 : -1, index - 640, index + 640]) {
        if (neighbor < 0 || neighbor >= count || !skin[neighbor]) continue;
        skin[neighbor] = 0; queue.push(neighbor);
      }
    }
    if (queue.length >= 60) parts.push({ area: queue.length, box: [left, top, right, bottom], center: [sumX / queue.length, sumY / queue.length] });
  }
  parts.sort((first, second) => second.area - first.area);
  const face = parts[0];
  const hands = parts.slice(1).filter(part => part.center[1] > face.box[1] + 20)
    .slice(0, 2).sort((first, second) => first.center[0] - second.center[0]);
  if (!face || hands.length !== 2) throw new Error(label + ': could not identify face and two hands: ' + JSON.stringify(parts));
  return { face, near: hands[0], far: hands[1] };
}

export function inside(horizontal, vertical, polygon) {
  let result = false;
  for (let current = 0, previous = polygon.length - 1; current < polygon.length; previous = current++) {
    const [currentX, currentY] = polygon[current], [previousX, previousY] = polygon[previous];
    if ((currentY > vertical) !== (previousY > vertical) && horizontal < (previousX - currentX) * (vertical - currentY) / (previousY - currentY) + currentX) result = !result;
  }
  return result;
}

export function polygonsFor(parts) {
  const [left, top, right, bottom] = parts.face.box;
  const hand = (part, cuff) => {
    const [handLeft, handTop, handRight, handBottom] = part.box;
    return [[handLeft - cuff, handTop - 3], [handLeft - 2, handTop - 7], [handRight + 4, handTop - 4],
      [handRight + 7, handTop + 3], [handRight + 7, handBottom - 1], [handRight, handBottom + 7],
      [handLeft - cuff + 2, handBottom + 7], [handLeft - cuff - 3, handBottom - 1]];
  };
  return { head: [[left - 85, 0], [right + 55, 0], [right + 55, bottom - 30], [right + 20, bottom - 13],
    [right - 6, bottom + 4], [left + 25, bottom + 7], [left - 20, bottom - 9], [left - 85, bottom - 10]],
    far_hand: hand(parts.far, 8), hands: hand(parts.near, 15) };
}
