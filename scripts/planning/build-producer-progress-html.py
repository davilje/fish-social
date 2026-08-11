#!/usr/bin/env python3
"""
策划进度看板 HTML — 从 项目开发需求计划表.xlsx 自动生成。

权威数据：xlsx「开发计划」sheet。
输出：仓库根目录 策划进度看板.html（并同步 docs/planning/ 副本）

与 npm run planning:master-xlsx 绑定：改表后必须重生本页，
保证策划打开网页即看到「离正式上线 / 千人运营」与「Unity 版本」双页签进度。
"""
from __future__ import annotations

import html
import os
import shutil
from collections import Counter, defaultdict
from datetime import date

import openpyxl

BASE = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
XLSX = os.path.join(BASE, "项目开发需求计划表.xlsx")
OUT = os.path.join(BASE, "策划进度看板.html")
OUT_DOCS_COPY = os.path.join(BASE, "docs", "planning", "策划进度看板.html")
XLSX_DOCS_FALLBACK = os.path.join(BASE, "docs", "planning", "项目开发需求计划表.xlsx")

DONE = frozenset({"已实现", "已实现（MVP）"})
OPEN = frozenset({"已确认", "未开始", "待开发"})
SKIP = frozenset({"已废弃", "已定稿", "已文档化"})  # 不计入「待做进度」分母时单独说明


def is_done_status(status: str) -> bool:
    """认准完成态：精确「已实现」、带阶段前缀（Phase 0 / DP-A 已实现）、MVP 标注。"""
    s = (status or "").strip()
    if not s or s in SKIP:
        return False
    if s in DONE:
        return True
    # 「Phase 0 已实现」「DP-A 已实现」等历史写法
    if s.endswith("已实现") and "未实现" not in s and "部分实现" not in s:
        return True
    return False

# 千人运营阶梯：每阶依赖的计划编号；完成比例 = 已实现 / 所列编号
CAPACITY_STAGES = [
    {
        "id": "S1",
        "title": "正规单店 · 内测可用",
        "capacity": "约 80 人同时在塘 / ~200 连接",
        "plain": "能玩、能查账、能上云单机。够小规模内测。",
        "ids": [
            "D-L1-01", "D-L1-03", "D-L1-05", "D-L1-08",
            "D-L2-01", "D-L2-02", "D-L2-03", "D-L2-11", "D-L3-01",
            "D-L1-04", "D-L1-06", "D-L1-07", "D-L2-05", "D-L2-06", "D-L2-07",
            "D-L2-12", "D-L2-13", "D-L1-11",
            "ARC-06", "ARC-07", "BUG-11",
            "D-L1-10", "D-L1-12", "D-L2-09",
        ],
    },
    {
        "id": "S2",
        "title": "安全加固 · 可小规模商用",
        "capacity": "仍约 80～200 人，但更稳、更安全",
        "plain": "加限流/连接上限、自动测试；店不会被一个人刷垮。",
        "ids": ["ARC-08", "ARC-09", "ARC-10", "ARC-11", "BUG-08", "D-L2-10", "D-L2-14"],
    },
    {
        "id": "S3",
        "title": "强单机 · 运营日报齐全",
        "capacity": "单机仍为主；运营能看日报、对照模拟",
        "plain": "数据库更强（PG）、有日报/BI；为扩容打地基，还不是千人。",
        "ids": [
            "D-L2-04",
            "D-L3-02", "D-L3-03", "D-L3-04", "D-L3-05",
            "D-L3-07", "D-L3-08", "D-L3-09", "D-L3-06", "D-L3-10",
        ],
    },
    {
        "id": "S4",
        "title": "多机扩容 · 千人以上",
        "capacity": "目标：1000+ 同时在线",
        "plain": "需要多台服务器 + Redis + 业务库全面 PG。专项见 BE-OPT-E；完成 S1～S3 后排期。",
        "ids": ["BE-OPT-E"],
        "future": True,
        "future_progress": 0,
    },
]

# Unity 客户端版本阶梯（与千人运营正交）
UNITY_STAGES = [
    {
        "id": "U0",
        "title": "P0 · 决策与契约冻结",
        "capacity": "切线确认 · 契约清单 v0",
        "plain": "Unity+Node、monorepo unity/、mobile 仅紧急修复、protocolVersion 约定。",
        "ids": ["UNITY-P0"],
    },
    {
        "id": "U1",
        "title": "P1 · 契约工程化",
        "capacity": "OpenAPI · Socket 目录 · C# DTO",
        "plain": "shared 拆 contracts/rules；Unity 空工程可编译引用同一套 DTO。",
        "ids": ["UNITY-P1"],
    },
    {
        "id": "U2",
        "title": "P2 · 网络薄客户端",
        "capacity": "连服钓一条鱼入库",
        "plain": "Socket 进塘闭环；可用临时几何体，无需正式美术。",
        "ids": ["UNITY-P2"],
    },
    {
        "id": "U3",
        "title": "P3 · 等距场景核心",
        "capacity": "Tile · 相机 · 序列帧",
        "plain": "承接 REF-SCENE-1；斜 45° 大场景可拖拽；与 P2 网络联通。",
        "ids": ["UNITY-P3"],
    },
    {
        "id": "U4",
        "title": "P4 · 壳层功能迁入",
        "capacity": "地图 · 背包商店 · 社交 · 排行榜",
        "plain": "主循环不依赖 Expo；Admin 仍留浏览器。",
        "ids": ["UNITY-P4"],
    },
    {
        "id": "U5",
        "title": "P5 · 发布与运维对齐",
        "capacity": "商店构建 · client-logs · 退役 RN",
        "plain": "可提交商店的最小可靠包 + 协议兼容回滚方案。",
        "ids": ["UNITY-P5"],
    },
]

UNITY_ID_PREFIX = "UNITY-"


def load_rows():
    xlsx_path = XLSX if os.path.exists(XLSX) else XLSX_DOCS_FALLBACK
    if not os.path.exists(xlsx_path):
        raise FileNotFoundError(f"缺少计划表: {XLSX}，请先 npm run planning:master-xlsx")
    wb = openpyxl.load_workbook(xlsx_path, read_only=True, data_only=True)
    ws = wb["开发计划"]
    rows = []
    for r in ws.iter_rows(min_row=2, values_only=True):
        if not r or not r[0]:
            continue
        rows.append(
            {
                "status": str(r[0]),
                "id": str(r[1] or ""),
                "type": str(r[2] or ""),
                "name": str(r[3] or ""),
                "layer": str(r[4] or ""),
                "phase": str(r[5] or ""),
                "priority": str(r[6] or ""),
                "note": str(r[7] or ""),
                "design": str(r[9] or "") if len(r) > 9 else "",
                "done": str(r[10] or "") if len(r) > 10 else "",
            }
        )
    wb.close()
    return rows


def pct(done: int, total: int) -> int:
    if total <= 0:
        return 0
    return int(round(100.0 * done / total))


def stage_progress(by_id: dict, stage: dict) -> tuple[int, int, int]:
    ids = stage.get("ids") or []
    if stage.get("future") and not ids:
        p = int(stage.get("future_progress") or 0)
        return p, 0, 0  # percent, done, total — empty future uses percent only
    known = [i for i in ids if i in by_id]
    if not known:
        if stage.get("future"):
            return int(stage.get("future_progress") or 0), 0, 0
        return 0, 0, 0
    done = sum(1 for i in known if is_done_status(by_id[i]["status"]))
    return pct(done, len(known)), done, len(known)


def bar_html(percent: int, tone: str = "ok") -> str:
    percent = max(0, min(100, percent))
    return (
        f'<div class="bar" role="progressbar" aria-valuenow="{percent}" '
        f'aria-valuemin="0" aria-valuemax="100">'
        f'<div class="bar-fill tone-{html.escape(tone)}" style="width:{percent}%"></div>'
        f'<span class="bar-label">{percent}%</span></div>'
    )


def type_missing_html(items: list[dict]) -> str:
    """Render the unfinished tasks hidden behind each type progress card."""
    missing = [item for item in items if not is_done_status(item["status"])]
    missing.sort(key=lambda item: (item["status"], item["priority"], item["id"]))
    if not missing:
        return '<p class="type-complete">该类型全部完成</p>'

    rows = []
    for item in missing:
        rows.append(
            "<li>"
            f'<code>{html.escape(item["id"])}</code> '
            f'{html.escape(item["name"])} '
            f'<em class="tag">{html.escape(item["status"])}</em>'
            f'<span class="task-priority">· {html.escape(item["priority"])}</span>'
            "</li>"
        )
    return (
        '<details class="type-missing">'
        f'<summary>还差 {len(missing)} 项未完成（点击展开）</summary>'
        f'<ul>{"".join(rows)}</ul>'
        "</details>"
    )


def render_stage_blocks(by_id: dict, stages: list[dict], label_prefix: str = "阶段") -> list[str]:
    stage_blocks = []
    for i, st in enumerate(stages):
        p, d, n = stage_progress(by_id, st)
        if st.get("future") and not st.get("ids"):
            status_txt = "未立项（计划表外）"
            tone = "future"
            detail = "完成前三阶后，需单独做「多机扩容」需求设计"
        elif st.get("future") and st.get("ids"):
            status_txt = "规划中（未排期）"
            tone = "future"
            detail = f"专项已登记；进度 {d}/{n} 已实现" if n else "专项已登记，待排期"
        elif p >= 100:
            status_txt = "已达成"
            tone = "ok"
            detail = f"{d}/{n} 项已实现"
        elif p > 0:
            status_txt = "进行中"
            tone = "mid"
            detail = f"{d}/{n} 项已实现"
        else:
            status_txt = "未开始"
            tone = "low"
            detail = f"0/{n} 项已实现" if n else "待立项"

        missing = []
        for iid in st.get("ids") or []:
            row = by_id.get(iid)
            if not row:
                missing.append(
                    f'<li><code>{html.escape(iid)}</code> '
                    f'<em class="tag">计划表未见</em></li>'
                )
                continue
            if not is_done_status(row["status"]):
                missing.append(
                    f'<li><code>{html.escape(iid)}</code> '
                    f'{html.escape(row["name"])} '
                    f'<em class="tag">{html.escape(row["status"])}</em></li>'
                )

        miss_html = ""
        if missing:
            miss_html = (
                '<details class="miss"><summary>还差这些（点击展开）</summary>'
                f'<ul>{"".join(missing)}</ul></details>'
            )
        elif st.get("future") and not st.get("ids"):
            miss_html = (
                '<ul class="future-list">'
                "<li>业务数据全面迁 PostgreSQL</li>"
                "<li>Redis + Socket 跨机广播</li>"
                "<li>负载均衡 / 多副本部署</li>"
                "<li>正式压测报告（千人级）</li>"
                "</ul>"
            )
        elif st.get("future") and st.get("ids"):
            miss_html = (
                '<ul class="future-list">'
                "<li>业务数据全面迁 PostgreSQL</li>"
                "<li>Redis + Socket 跨机广播</li>"
                "<li>负载均衡 / 多副本部署</li>"
                "<li>正式压测报告（千人级）</li>"
                "<li>排期并确认 BE-OPT-E 子项</li>"
                "</ul>"
            )

        stage_blocks.append(
            f'<section class="stage tone-{tone}">'
            f'<div class="stage-top">'
            f'<span class="stage-num">{html.escape(label_prefix)} {i + 1}</span>'
            f'<span class="pill">{html.escape(status_txt)}</span>'
            f"</div>"
            f'<h3>{html.escape(st["title"])}</h3>'
            f'<p class="cap">{html.escape(st["capacity"])}</p>'
            f'<p class="plain">{html.escape(st["plain"])}</p>'
            f'{bar_html(p, tone)}'
            f'<p class="muted small">{html.escape(detail)}</p>'
            f"{miss_html}"
            f"</section>"
        )
    return stage_blocks


def open_table_html(open_rows: list[dict]) -> str:
    open_trs = []
    for r in open_rows:
        open_trs.append(
            "<tr>"
            f'<td><code>{html.escape(r["id"])}</code></td>'
            f'<td>{html.escape(r["name"])}</td>'
            f'<td>{html.escape(r["type"])}</td>'
            f'<td>{html.escape(r["status"])}</td>'
            f'<td>{html.escape(r["priority"])}</td>'
            f'<td>{html.escape(r["design"] if r["design"] != "None" else "")}</td>'
            "</tr>"
        )
    body = "".join(open_trs) if open_trs else "<tr><td colspan=6>暂无开放项</td></tr>"
    return f"""<table class="data">
    <thead>
      <tr><th>编号</th><th>名称</th><th>类型</th><th>状态</th><th>优先级</th><th>设计时间</th></tr>
    </thead>
    <tbody>
      {body}
    </tbody>
  </table>"""


def build_html(rows: list[dict]) -> str:
    today = date.today().isoformat()
    by_id = {r["id"]: r for r in rows if r["id"]}
    status_c = Counter(r["status"] for r in rows)
    total = len(rows)
    done_n = sum(1 for r in rows if is_done_status(r["status"]))
    conf_n = status_c.get("已确认", 0)
    open_n = sum(status_c.get(s, 0) for s in ("已确认", "未开始", "待开发"))
    overall = pct(done_n, total - status_c.get("已废弃", 0))

    # 按类型
    by_type: dict[str, list] = defaultdict(list)
    for r in rows:
        if r["status"] == "已废弃":
            continue
        by_type[r["type"] or "其他"].append(r)

    type_blocks = []
    for t in sorted(by_type.keys(), key=lambda x: (-len(by_type[x]), x)):
        items = by_type[t]
        d = sum(1 for x in items if is_done_status(x["status"]))
        p = pct(d, len(items))
        tone = "ok" if p >= 80 else ("mid" if p >= 40 else "low")
        type_blocks.append(
            f'<div class="card">'
            f'<div class="card-h"><strong>{html.escape(t)}</strong>'
            f'<span class="muted">{d}/{len(items)}</span></div>'
            f'{bar_html(p, tone)}'
            f'{type_missing_html(items)}</div>'
        )

    capacity_blocks = render_stage_blocks(by_id, CAPACITY_STAGES, "阶段")
    unity_blocks = render_stage_blocks(by_id, UNITY_STAGES, "Phase")

    unity_ids = [st["ids"][0] for st in UNITY_STAGES if st.get("ids")]
    unity_known = [i for i in unity_ids if i in by_id]
    unity_done = sum(1 for i in unity_known if is_done_status(by_id[i]["status"]))
    unity_pct = pct(unity_done, len(unity_known)) if unity_known else 0

    # 开放项：千人页排除 UNITY；Unity 页只看 UNITY
    open_all = [r for r in rows if r["status"] in OPEN]
    open_capacity = [r for r in open_all if not r["id"].startswith(UNITY_ID_PREFIX)]
    open_unity = [r for r in open_all if r["id"].startswith(UNITY_ID_PREFIX)]
    # 已定稿但未实现的 Unity 阶段也列入 Unity 页「排队」
    unity_queued = [
        r for r in rows
        if r["id"].startswith(UNITY_ID_PREFIX)
        and r["status"] in ("已定稿", "已确认", "未开始", "待开发")
    ]
    unity_queued.sort(key=lambda x: x["id"])
    open_capacity.sort(key=lambda x: (x["type"], x["id"]))

    legend = """
    <div class="legend">
      <h2>用店做比喻（给策划）</h2>
      <ol>
        <li><strong>最早</strong>：小作坊柜台 — 能钓鱼，出事靠盯黑窗口。</li>
        <li><strong>现在（阶段1）</strong>：有监控室和台账的正规单店 — 能内测、能上云单机。</li>
        <li><strong>阶段2</strong>：装好防盗门和自动质检 — 限流、测试，适合小规模对外。</li>
        <li><strong>阶段3</strong>：仓库升级 + 日报齐全 — 还是一家店，但账记得更清楚。</li>
        <li><strong>阶段4 · 千人</strong>：开连锁店 — 多台服务器，计划表里还要新开专项。</li>
      </ol>
      <p class="warn">重要：计划表全部做完 ≠ 自动支持千人同时在线。千人属于阶段4。与 Unity 换端正交。</p>
    </div>
    """

    ops = """
    <div class="ops">
      <h2>出问题了我从哪看？</h2>
      <table>
        <tr><th>你要做什么</th><th>去哪</th></tr>
        <tr><td>店还开着吗？</td><td>浏览器打开 <code>/health</code>（本地一般是 localhost:3001/health）</td></tr>
        <tr><td>最近报错、鱼塘人数、健康趋势</td><td>游戏里的 <strong>Admin 管理页</strong>（找开发要 Admin 密钥）</td></tr>
        <tr><td>开发自检功能对不对</td><td>终端跑 <code>npm run verify:server-boot</code> 等（不是压测）</td></tr>
        <tr><td>压力测试结果</td><td><strong>目前还没有固定报告页</strong>；做完后由开发给你一页纸结论</td></tr>
      </table>
    </div>
    """

    unity_legend = """
    <div class="legend">
      <h2>Unity 版本原则（给策划）</h2>
      <ol>
        <li><strong>权威在 Node</strong>：咬钩 / 占位 / 库存写不进 Unity。</li>
        <li><strong>协议先冻</strong>：事件名与主 DTO 默认不变（见契约清单 v0）。</li>
        <li><strong>场景重做</strong>：不平移 RN View；斜 45° Tile 在 P3。</li>
        <li><strong>RN</strong>：主开发切 Unity 后仅紧急修复；退役在 P5。</li>
      </ol>
      <p class="warn">千人多机（阶段4 / BE-OPT-E）与 Unity 换端分开推进，互不阻塞。</p>
    </div>
    """

    return f"""<!DOCTYPE html>
<html lang="zh-CN">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>Fish Social · 策划进度看板</title>
<style>
:root {{
  --bg: #f6f3ee;
  --ink: #1c1917;
  --muted: #78716c;
  --card: #fffcf7;
  --line: #e7e0d5;
  --ok: #0f766e;
  --ok-bg: #ccfbf1;
  --mid: #b45309;
  --mid-bg: #ffedd5;
  --low: #b91c1c;
  --low-bg: #fee2e2;
  --future: #57534e;
  --accent: #0e7490;
  --hero: #134e4a;
  --tab: #0f766e;
}}
* {{ box-sizing: border-box; margin: 0; padding: 0; }}
body {{
  font-family: "Segoe UI", "PingFang SC", "Microsoft YaHei", sans-serif;
  background: var(--bg);
  color: var(--ink);
  line-height: 1.55;
  padding: 1.5rem;
}}
.wrap {{ max-width: 980px; margin: 0 auto; }}
header {{
  background: var(--hero);
  color: #ecfdf5;
  border-radius: 16px;
  padding: 1.5rem 1.75rem;
  margin-bottom: 1.25rem;
}}
header h1 {{ font-size: 1.55rem; font-weight: 700; letter-spacing: -0.02em; }}
header .sub {{ opacity: 0.85; margin-top: 0.4rem; font-size: 0.95rem; }}
.stats {{
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 0.75rem;
  margin: 1.25rem 0;
}}
@media (max-width: 720px) {{
  .stats {{ grid-template-columns: repeat(2, 1fr); }}
}}
.stat {{
  background: var(--card);
  border: 1px solid var(--line);
  border-radius: 12px;
  padding: 1rem;
}}
.stat .n {{ font-size: 1.75rem; font-weight: 700; color: var(--accent); }}
.stat .l {{ font-size: 0.8rem; color: var(--muted); margin-top: 0.2rem; }}
.section {{ margin: 1.75rem 0; }}
.section h2 {{
  font-size: 1.15rem;
  margin-bottom: 0.85rem;
  padding-bottom: 0.4rem;
  border-bottom: 2px solid var(--line);
}}
.grid2 {{
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 0.75rem;
}}
@media (max-width: 720px) {{ .grid2 {{ grid-template-columns: 1fr; }} }}
.card {{
  background: var(--card);
  border: 1px solid var(--line);
  border-radius: 12px;
  padding: 0.9rem 1rem;
}}
.card-h {{
  display: flex;
  justify-content: space-between;
  align-items: baseline;
  margin-bottom: 0.5rem;
}}
.muted {{ color: var(--muted); }}
.small {{ font-size: 0.85rem; }}
.bar {{
  position: relative;
  height: 1.35rem;
  background: #ebe5dc;
  border-radius: 999px;
  overflow: hidden;
  margin: 0.45rem 0;
}}
.bar-fill {{
  height: 100%;
  border-radius: 999px;
  transition: width 0.3s ease;
}}
.tone-ok {{ background: var(--ok); }}
.tone-mid {{ background: var(--mid); }}
.tone-low {{ background: var(--low); }}
.tone-future {{ background: var(--future); }}
.bar-label {{
  position: absolute;
  inset: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 0.75rem;
  font-weight: 600;
  color: var(--ink);
}}
.stages {{ display: flex; flex-direction: column; gap: 0.85rem; }}
.stage {{
  background: var(--card);
  border: 1px solid var(--line);
  border-left: 5px solid var(--muted);
  border-radius: 12px;
  padding: 1rem 1.1rem;
}}
.stage.tone-ok {{ border-left-color: var(--ok); background: #f0fdfa; }}
.stage.tone-mid {{ border-left-color: var(--mid); background: #fffbeb; }}
.stage.tone-low {{ border-left-color: var(--low); }}
.stage.tone-future {{ border-left-color: var(--future); }}
.stage-top {{
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 0.35rem;
}}
.stage-num {{ font-size: 0.8rem; color: var(--muted); font-weight: 600; }}
.pill {{
  font-size: 0.75rem;
  padding: 0.15rem 0.55rem;
  border-radius: 999px;
  background: #e7e5e4;
  font-weight: 600;
}}
.stage.tone-ok .pill {{ background: var(--ok-bg); color: var(--ok); }}
.stage.tone-mid .pill {{ background: var(--mid-bg); color: var(--mid); }}
.stage.tone-low .pill {{ background: var(--low-bg); color: var(--low); }}
.cap {{ font-weight: 600; color: var(--accent); margin: 0.25rem 0; }}
.plain {{ color: var(--muted); font-size: 0.92rem; margin-bottom: 0.35rem; }}
.miss, .future-list {{ margin-top: 0.6rem; font-size: 0.88rem; }}
.miss summary {{ cursor: pointer; color: var(--mid); font-weight: 600; }}
.miss ul, .future-list {{ margin: 0.4rem 0 0 1.1rem; }}
.type-missing {{ margin-top: 0.65rem; font-size: 0.86rem; }}
.type-missing summary {{ cursor: pointer; color: var(--accent); font-weight: 600; }}
.type-missing ul {{ margin: 0.4rem 0 0 1.1rem; }}
.type-missing li {{ margin: 0.2rem 0; }}
.type-complete {{ color: var(--ok); font-size: 0.86rem; margin-top: 0.6rem; }}
.task-priority {{ color: var(--muted); font-size: 0.8rem; }}
.tag {{ font-style: normal; color: var(--muted); font-size: 0.8rem; }}
table.data {{
  width: 100%;
  border-collapse: collapse;
  font-size: 0.88rem;
  background: var(--card);
  border-radius: 12px;
  overflow: hidden;
  border: 1px solid var(--line);
}}
table.data th, table.data td {{
  text-align: left;
  padding: 0.55rem 0.65rem;
  border-bottom: 1px solid var(--line);
}}
table.data th {{ background: #efeae2; font-size: 0.8rem; }}
code {{
  font-family: ui-monospace, Consolas, monospace;
  font-size: 0.85em;
  background: #efeae2;
  padding: 0.05rem 0.3rem;
  border-radius: 4px;
}}
.legend, .ops {{
  background: var(--card);
  border: 1px solid var(--line);
  border-radius: 12px;
  padding: 1rem 1.2rem;
  margin-bottom: 1rem;
}}
.legend ol {{ margin: 0.5rem 0 0.75rem 1.2rem; }}
.warn {{
  background: var(--mid-bg);
  color: #7c2d12;
  padding: 0.65rem 0.8rem;
  border-radius: 8px;
  font-size: 0.9rem;
}}
.ops table {{ width: 100%; border-collapse: collapse; margin-top: 0.5rem; font-size: 0.9rem; }}
.ops th, .ops td {{ border-bottom: 1px solid var(--line); padding: 0.5rem; text-align: left; vertical-align: top; }}
footer {{
  margin-top: 2rem;
  padding-top: 1rem;
  border-top: 1px solid var(--line);
  color: var(--muted);
  font-size: 0.82rem;
  text-align: center;
}}
.overall-wrap {{
  background: var(--card);
  border: 1px solid var(--line);
  border-radius: 12px;
  padding: 1rem 1.2rem;
  margin-bottom: 0.5rem;
}}
.tabs {{
  display: flex;
  gap: 0.5rem;
  margin: 1.25rem 0 0;
  flex-wrap: wrap;
}}
.tab-btn {{
  appearance: none;
  border: 1px solid var(--line);
  background: var(--card);
  color: var(--ink);
  font: inherit;
  font-weight: 600;
  font-size: 0.95rem;
  padding: 0.7rem 1.1rem;
  border-radius: 999px;
  cursor: pointer;
}}
.tab-btn[aria-selected="true"] {{
  background: var(--tab);
  border-color: var(--tab);
  color: #ecfdf5;
}}
.tab-btn:focus-visible {{
  outline: 2px solid var(--accent);
  outline-offset: 2px;
}}
.tab-panel {{ display: none; margin-top: 1rem; }}
.tab-panel.active {{ display: block; }}
.doc-links {{ font-size: 0.88rem; margin-top: 0.5rem; }}
.doc-links a {{ color: var(--accent); }}
</style>
</head>
<body>
<div class="wrap">
<header>
  <h1>Fish Social · 策划进度看板</h1>
  <p class="sub">自动生成于 {html.escape(today)} · 数据来自「项目开发需求计划表」· 双页签：千人运营 / Unity 版本</p>
</header>

<div class="overall-wrap">
  <div class="card-h"><strong>计划表总进度</strong><span class="muted">{done_n} 已实现 / {total} 项</span></div>
  {bar_html(overall, "ok" if overall >= 70 else "mid")}
  <p class="muted small">不含「已废弃」时的完成度约 {overall}%。「已确认」= 设计好了、还在等开发。「已定稿」= 排队未开工。</p>
</div>

<div class="stats">
  <div class="stat"><div class="n">{done_n}</div><div class="l">已实现</div></div>
  <div class="stat"><div class="n">{conf_n}</div><div class="l">已确认（待开发）</div></div>
  <div class="stat"><div class="n">{open_n}</div><div class="l">开放待办合计</div></div>
  <div class="stat"><div class="n">{unity_pct}%</div><div class="l">Unity 阶梯（{unity_done}/{len(unity_known)}）</div></div>
</div>

<nav class="tabs" role="tablist" aria-label="进度主题">
  <button type="button" class="tab-btn" role="tab" id="tab-capacity" aria-controls="panel-capacity" aria-selected="true" data-tab="capacity">完成千人运营目标</button>
  <button type="button" class="tab-btn" role="tab" id="tab-unity" aria-controls="panel-unity" aria-selected="false" data-tab="unity">Unity 版本开发</button>
</nav>

<div id="panel-capacity" class="tab-panel active" role="tabpanel" aria-labelledby="tab-capacity">
{legend}

<section class="section">
  <h2>离「千人以上运营」还有多远？</h2>
  <p class="muted small" style="margin-bottom:0.75rem">下面四根进度条是<strong>容量阶梯</strong>，不是玩法清单。阶段 4 = 千人；与 Unity 换端分开看。</p>
  <div class="stages">
    {"".join(capacity_blocks)}
  </div>
</section>

<section class="section">
  <h2>按类型：后端/数据/玩法分别做到哪</h2>
  <div class="grid2">
    {"".join(type_blocks)}
  </div>
</section>

{ops}

<section class="section">
  <h2>当前还没做完的事项（非 Unity · {len(open_capacity)}）</h2>
  {open_table_html(open_capacity)}
</section>
</div>

<div id="panel-unity" class="tab-panel" role="tabpanel" aria-labelledby="tab-unity" hidden>
{unity_legend}

<div class="overall-wrap">
  <div class="card-h"><strong>Unity 移植阶梯</strong><span class="muted">{unity_done} / {len(unity_known)} Phase 已实现</span></div>
  {bar_html(unity_pct, "ok" if unity_pct >= 70 else ("mid" if unity_pct > 0 else "low"))}
  <p class="doc-links muted">
    决策：<a href="docs/planning/architecture/Unity迁移决策记录.md">Unity迁移决策记录</a> ·
    契约：<a href="docs/planning/architecture/Unity契约冻结清单-v0.md">契约冻结清单 v0</a> ·
    总表：<a href="docs/planning/specs/Unity移植-分阶段需求清单.md">分阶段需求清单</a>
  </p>
</div>

<section class="section">
  <h2>Unity 版本开发进度（P0～P5）</h2>
  <p class="muted small" style="margin-bottom:0.75rem">顺序：P0→P1→P2→P3；P4 可与 P3 错峰；P5 在主循环出口后。</p>
  <div class="stages">
    {"".join(unity_blocks)}
  </div>
</section>

<section class="section">
  <h2>Unity 排队 / 待办（{len(unity_queued)}）</h2>
  {open_table_html(unity_queued)}
</section>
</div>

<footer>
  本文件由 <code>scripts/planning/build-producer-progress-html.py</code> 生成。<br>
  开发验收后请运行 <code>npm run planning:master-xlsx</code>（会同时更新本页）。请勿手改 HTML，下次会覆盖。<br>
  权威计划表：项目开发需求计划表.xlsx（仓库根目录）
</footer>
</div>
<script>
(function () {{
  var tabs = document.querySelectorAll(".tab-btn");
  var panels = {{
    capacity: document.getElementById("panel-capacity"),
    unity: document.getElementById("panel-unity")
  }};
  function activate(name) {{
    tabs.forEach(function (btn) {{
      var on = btn.getAttribute("data-tab") === name;
      btn.setAttribute("aria-selected", on ? "true" : "false");
    }});
    Object.keys(panels).forEach(function (key) {{
      var el = panels[key];
      var on = key === name;
      el.classList.toggle("active", on);
      if (on) el.removeAttribute("hidden");
      else el.setAttribute("hidden", "");
    }});
    try {{ history.replaceState(null, "", "#" + name); }} catch (e) {{}}
  }}
  tabs.forEach(function (btn) {{
    btn.addEventListener("click", function () {{
      activate(btn.getAttribute("data-tab"));
    }});
  }});
  var hash = (location.hash || "").replace(/^#/, "");
  if (hash === "unity" || hash === "capacity") activate(hash);
}})();
</script>
</body>
</html>
"""


def main():
    rows = load_rows()
    html_out = build_html(rows)
    with open(OUT, "w", encoding="utf-8") as f:
        f.write(html_out)
    print(f"Written: {OUT}")
    try:
        os.makedirs(os.path.dirname(OUT_DOCS_COPY), exist_ok=True)
        shutil.copy2(OUT, OUT_DOCS_COPY)
        print(f"Synced copy: {OUT_DOCS_COPY}")
    except OSError as e:
        print(f"WARN: could not sync docs copy: {e}")
    status_c = Counter(r["status"] for r in rows)
    print(f"Items: {len(rows)} · {dict(status_c)}")


if __name__ == "__main__":
    main()
