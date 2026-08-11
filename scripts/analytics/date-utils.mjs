/**
 * Asia/Shanghai 自然日边界工具
 */
const SHANGHAI_TZ = 'Asia/Shanghai';

/** @returns { dateKey: string, dayStartMs: number, dayEndMs: number } */
export function shanghaiDayBounds(dateKey) {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(dateKey)) {
    throw new Error(`Invalid dateKey: ${dateKey}`);
  }
  const dayStartMs = Date.parse(`${dateKey}T00:00:00+08:00`);
  if (!Number.isFinite(dayStartMs)) {
    throw new Error(`Cannot parse Shanghai day bounds for ${dateKey}`);
  }
  const dayEndMs = dayStartMs + 86_400_000;
  return { dateKey, dayStartMs, dayEndMs };
}

export function getShanghaiDateKey(date = new Date()) {
  return new Intl.DateTimeFormat('en-CA', { timeZone: SHANGHAI_TZ }).format(date);
}

export function yesterdayDateKeyShanghai(now = new Date()) {
  const todayKey = getShanghaiDateKey(now);
  const { dayStartMs } = shanghaiDayBounds(todayKey);
  return getShanghaiDateKey(new Date(dayStartMs - 86_400_000));
}

/** @param {string[]} [argv] */
export function parseDateArg(argv = process.argv.slice(2)) {
  const dateArg = argv.find((a) => a.startsWith('--date='));
  if (dateArg) {
    const key = dateArg.split('=')[1];
    shanghaiDayBounds(key);
    return key;
  }
  return yesterdayDateKeyShanghai();
}

/** 前 N 个自然日 dateKey（含当日），升序 */
export function previousDateKeys(dateKey, count) {
  const keys = [];
  let { dayStartMs } = shanghaiDayBounds(dateKey);
  for (let i = count - 1; i >= 0; i--) {
    const ms = dayStartMs - i * 86_400_000;
    keys.push(getShanghaiDateKey(new Date(ms)));
  }
  return keys;
}

/** dateKey + n 个上海自然日 */
export function addShanghaiDays(dateKey, days) {
  const { dayStartMs } = shanghaiDayBounds(dateKey);
  return getShanghaiDateKey(new Date(dayStartMs + days * 86_400_000));
}
