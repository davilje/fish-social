/** Mirror shared/pondCatalog.ts display names (admin-web has no shared workspace dep). */

const POND_NAMES: Record<string, string> = {
  'pond-calm': '静心湖',
  'pond-mist': '云雾塘',
  'pond-sunset': '夕阳湾',
  'pond-bamboo': '竹林池',
  'pond-reed': '芦苇荡',
  'pond-crystal': '晶石潭',
  'pond-lotus': '荷香池',
  'pond-mirror': '镜面湖',
  'pond-willow': '柳荫湾',
  'pond-stone': '叠石矶',
  'pond-spring': '清泉眼',
  'pond-dusk': '暮色泊',
  'pond-pine': '松风潭',
  'pond-coral': '珊瑚浅',
  'pond-moon': '月影池',
  'pond-fern': '蕨影泽',
  'pond-ridge': '岭下塘',
  'pond-harbor': '渔港湾',
  'pond-orchid': '兰汀',
  'pond-frost': '霜华淀',
};

export const ADMIN_PONDS = Object.entries(POND_NAMES).map(([id, name]) => ({ id, name }));

export function formatPondName(pondId: string | null | undefined): string {
  if (!pondId) return '—';
  return POND_NAMES[pondId] ?? pondId;
}

export function formatSpotName(
  spotId: string | null | undefined,
  pondId?: string | null,
): string {
  if (!spotId) return '—';
  let short = pondId ? POND_NAMES[pondId] : undefined;
  if (!short) {
    for (const [pid, name] of Object.entries(POND_NAMES)) {
      const prefix = pid.replace(/^pond-/, '');
      if (spotId.startsWith(`${prefix}-spot-`) || spotId.includes(prefix)) {
        short = name;
        break;
      }
    }
  }
  short = short ?? '钓位';
  const m = /(?:^|-)spot-(\d+)$/i.exec(spotId) ?? /-(\d+)$/.exec(spotId);
  if (m) return `${short}·${Number(m[1])}号位`;
  return `${short}·${spotId}`;
}
