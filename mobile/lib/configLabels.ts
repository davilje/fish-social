import { FISH_QUALITIES, FISH_SPECIES } from '@fish-social/shared';

export interface ConfigKeyMeta {
  label: string;
  hint: string;
}

const STATIC_KEYS: Record<string, ConfigKeyMeta> = {
  FISH_BITE_CHECK_MS: {
    label: '咬钩判定间隔',
    hint: '毫秒；默认 60000（每 1 分钟判定一次）',
  },
  BITE_LAMBDA: {
    label: '咬钩指数 λ（旧）',
    hint: 'A0 指数模型遗留；v2 乘法咬钩不再使用',
  },
  C3_SINK_ENABLED: {
    label: '经济回收开关',
    hint: 'true / false；控制金币回收类玩法',
  },
  C4_GENETICS_ENABLED: {
    label: '繁殖遗传开关',
    hint: 'true / false；v0.3.1 起塘内繁殖已移除，本项不再生效',
  },
  C6_SKIP_CASTING_ON_REBATE: {
    label: '返饵跳过抛竿',
    hint: 'true / false；C6 状态机相关',
  },
  BOT_CATCH_SHARE_CAP: {
    label: 'Bot 钓获占比上限',
    hint: '0~1；限制机器人占全塘钓获比例',
  },
};

const qualityByKey = new Map(
  FISH_QUALITIES.map((q) => [`QUALITY_ESCAPE_BONUS_${q.id.toUpperCase()}`, q.name]),
);

const speciesById = new Map(FISH_SPECIES.map((s) => [s.id, s.name]));

export function describeConfigKey(key: string): ConfigKeyMeta {
  const known = STATIC_KEYS[key];
  if (known) return known;

  const qName = qualityByKey.get(key);
  if (qName) {
    return {
      label: `${qName}品质脱钩加成`,
      hint: '加到该品质基础脱钩率上（0~1）',
    };
  }

  const biteMatch = key.match(/^SPECIES_BITE_WEIGHT_(.+)$/);
  if (biteMatch) {
    const sp = speciesById.get(biteMatch[1]) ?? biteMatch[1];
    return {
      label: `${sp} · 基础咬钩权重`,
      hint: 'v2 中 biteRatePerTick 的兜底来源（0~1）',
    };
  }

  const escMatch = key.match(/^SPECIES_ESCAPE_RATE_(.+)$/);
  if (escMatch) {
    const sp = speciesById.get(escMatch[1]) ?? escMatch[1];
    return {
      label: `${sp} · 基础脱钩率`,
      hint: '上钩后跑鱼概率（0~1）',
    };
  }

  return { label: key, hint: '运行时配置键' };
}
