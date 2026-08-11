/** 进程启动时刻（模块加载时） */
export const SERVER_STARTED_AT = Date.now();

export function getServerLifecycleInfo(): { startedAt: number; uptimeSec: number; pid: number } {
  return {
    startedAt: SERVER_STARTED_AT,
    uptimeSec: Math.floor((Date.now() - SERVER_STARTED_AT) / 1000),
    pid: process.pid,
  };
}
