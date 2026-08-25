# -*- coding: utf-8 -*-
"""FEAT-SPOT-02: spot tag catalog, clue library, pond_spot_tags generator."""
from __future__ import annotations

import hashlib
from typing import Iterable

# ---------------------------------------------------------------------------
# Tag catalog — 6 categories, 22 tags (each spot carries 4–6 tags across categories)
# Columns: tagId, tagCategory, nameZh, descriptionZh
# ---------------------------------------------------------------------------
SPOT_TAG_DEFS: list[tuple[str, str, str, str]] = [
    # terrain 地形结构
    ("weed_grass", "terrain", "草缘", "岸边水草、芦苇根丛伸入水中，草线清晰。"),
    ("structure", "terrain", "结构位", "坎位、铧尖、桥墩、木桩等突出或转折结构。"),
    ("inlet_eddy", "terrain", "进出水", "进水口、洄湾、缓流区或明显转向水域。"),
    ("open_flat", "terrain", "亮水开阔", "无遮挡的大片水面，视野开阔。"),
    ("depth_break", "terrain", "深浅交界", "坡坎、台地边缘，深浅过渡明显。"),
    ("channel", "terrain", "收窄水道", "两湾之间收窄、流速略快的水道。"),
    # water 水质水色
    ("water_clear", "water", "清水", "可见底或近底，透光好。"),
    ("water_muddy", "water", "浑水", "泥浆色、悬浮颗粒多，透光差。"),
    ("water_green", "water", "泛绿水", "藻类染色，水色发绿。"),
    ("water_tea", "water", "茶色水", "腐殖质染色，呈茶褐或酱油色。"),
    # light 光照
    ("shade_tree", "light", "树影", "树冠投影覆盖水面。"),
    ("shade_cliff", "light", "坝崖影", "坝体、崖壁等高结构投影。"),
    ("sun_open", "light", "无遮强光", "直射日光，水面反光强。"),
    # wind 风况
    ("wind_lee", "wind", "背风", "岸线或地形遮挡，水面纹弱。"),
    ("wind_fetch", "wind", "下风/迎风", "波纹明显，漂物向一侧聚集。"),
    # depth 水深体感
    ("depth_shallow", "depth", "偏浅", "近岸延伸，体感水浅。"),
    ("depth_mid", "depth", "中等深", "常规岸钓深度，不深不浅。"),
    ("depth_deep", "depth", "偏深", "离岸远或陡坎外侧，体感较深。"),
    # shore 岸貌
    ("shore_reed", "shore", "芦苇岸", "芦苇、蒲草为主的软质岸线。"),
    ("shore_rock", "shore", "石岸", "乱石、卵石或砌石岸线。"),
    ("shore_mud", "shore", "泥岸", "淤泥、软泥质岸线。"),
    ("shore_hard", "shore", "硬岸", "水泥、条石等硬化岸线。"),
]

TAG_BY_CATEGORY: dict[str, list[str]] = {}
for _tid, _cat, _n, _d in SPOT_TAG_DEFS:
    TAG_BY_CATEGORY.setdefault(_cat, []).append(_tid)

TERRAIN_TAGS = TAG_BY_CATEGORY["terrain"]
DEPTH_TAGS = TAG_BY_CATEGORY["depth"]
OPTIONAL_TAGS = (
    TAG_BY_CATEGORY["water"]
    + TAG_BY_CATEGORY["light"]
    + TAG_BY_CATEGORY["wind"]
    + TAG_BY_CATEGORY["shore"]
)

# activitySignal: habitat | active_high | active_mid | active_low | inactive | disturbed
# habitat 仅描述环境；activity 只描述可观察征象，不出现鱼种/鱼情结论性用语。

def _hab(tag: str, cid: str, text: str, weight: int = 1) -> tuple:
    return (cid, "habitat", text, weight, 0, 0, "", tag, "habitat", True)


def _act(tag: str, cid: str, text: str, signal: str, weight: int = 1) -> tuple:
    return (cid, "activity", text, weight, 0, 0, "", tag, signal, True)


# Per-tag clue seeds: (habitat×3, active_high×2, active_mid×1, active_low×1, inactive×2, disturbed×1)
_TAG_CLUE_SEEDS: dict[str, list[tuple]] = {
    "weed_grass": [
        _hab("weed_grass", "h-wg-01", "岸边密集水草伸入水中，草缘与外侧亮水分界清晰。"),
        _hab("weed_grass", "h-wg-02", "芦苇根丛贴岸生长，茎秆在水中随轻波轻摆。"),
        _hab("weed_grass", "h-wg-03", "草缝间可见窄窄水色带，比外围略深一线。"),
        _act("weed_grass", "a-wg-01", "草缘处不时冒出细泡，位置随波纹漂移。", "active_high"),
        _act("weed_grass", "a-wg-02", "水草叶缘轻颤，节奏与风向不完全一致。", "active_high"),
        _act("weed_grass", "a-wg-03", "草边偶见小涟漪散开，间隔数分钟一次。", "active_mid"),
        _act("weed_grass", "a-wg-04", "草下传来极轻的水声，像细枝被拨动。", "active_low"),
        _act("weed_grass", "a-wg-05", "草缘水面长时间平静，未见新泡或叶动。", "inactive"),
        _act("weed_grass", "a-wg-06", "水草整齐垂立，近岸无漂物扰动。", "inactive"),
        _act("weed_grass", "a-wg-07", "草边突然成片晃动后迅速平息，水面留细浪。", "disturbed"),
    ],
    "structure": [
        _hab("structure", "h-st-01", "坎位边缘水色突变，深浅在此处转折。"),
        _hab("structure", "h-st-02", "木桩、桥墩周围水纹与外围流向不同。"),
        _hab("structure", "h-st-03", "铧尖延伸进大水面，两侧形成明显夹角。"),
        _act("structure", "a-st-01", "结构位下游偶起环状涟漪，范围不大。", "active_high"),
        _act("structure", "a-st-02", "桩旁水面间歇冒出成串气泡，大小不一。", "active_high"),
        _act("structure", "a-st-03", "坎边偶尔有细沫随流漂移。", "active_mid"),
        _act("structure", "a-st-04", "结构阴影区水面纹丝不动。", "active_low"),
        _act("structure", "a-st-05", "结构位长时间无泡、无漂物经过。", "inactive"),
        _act("structure", "a-st-06", "水面镜像般平整，结构倒影清晰完整。", "inactive"),
        _act("structure", "a-st-07", "投影掠过水面后，结构附近波纹骤密又骤停。", "disturbed"),
    ],
    "inlet_eddy": [
        _hab("inlet_eddy", "h-in-01", "水流在此转向，水面漂物沿弧线缓慢移动。"),
        _hab("inlet_eddy", "h-in-02", "进水一侧水色略浊，与主体水域有细线分界。"),
        _hab("inlet_eddy", "h-in-03", "洄湾内侧波纹比外侧更弱，像被兜住。"),
        _act("inlet_eddy", "a-in-01", "缓流区漂物打着转，偶尔带出小涡。", "active_high"),
        _act("inlet_eddy", "a-in-02", "进水口附近气泡成带分布，随流下行。", "active_high"),
        _act("inlet_eddy", "a-in-03", "湾内偶起细鳞状涟漪，方向不固定。", "active_mid"),
        _act("inlet_eddy", "a-in-04", "水面仅有极弱流向，几乎看不出移动。", "active_low"),
        _act("inlet_eddy", "a-in-05", "缓流区长时间无漂物、无新泡。", "inactive"),
        _act("inlet_eddy", "a-in-06", "进水一侧静得像停滞，只有远端微波。", "inactive"),
        _act("inlet_eddy", "a-in-07", "水面突然翻起一片浑色又很快散开。", "disturbed"),
    ],
    "open_flat": [
        _hab("open_flat", "h-op-01", "视野内大片亮水，对岸轮廓清晰可见。"),
        _hab("open_flat", "h-op-02", "中央水域颜色均匀，缺少草线或结构转折。"),
        _hab("open_flat", "h-op-03", "风过处整片水面同步起浪，没有局部遮挡。"),
        _act("open_flat", "a-op-01", "亮水区远处偶见长条涟漪快速划过。", "active_high"),
        _act("open_flat", "a-op-02", "中央水面间歇冒出单点气泡，随即消失。", "active_high"),
        _act("open_flat", "a-op-03", "细浪规律拍岸，中间偶有异常断纹。", "active_mid"),
        _act("open_flat", "a-op-04", "亮水区只有均匀微波，无局部扰动。", "active_low"),
        _act("open_flat", "a-op-05", "长时间不见漂物移动或新泡。", "inactive"),
        _act("open_flat", "a-op-06", "水面如镜，反光稳定不变。", "inactive"),
        _act("open_flat", "a-op-07", "一片区域突然起密浪后恢复平静。", "disturbed"),
    ],
    "depth_break": [
        _hab("depth_break", "h-db-01", "坡坎下方水色明显变深，过渡带窄而清晰。"),
        _hab("depth_break", "h-db-02", "深浅交界处偶尔有落叶在此处打旋。"),
        _hab("depth_break", "h-db-03", "坎边水温体感略低，近岸与远水颜色分层。"),
        _act("depth_break", "a-db-01", "坎边不时翻出细沫，范围沿坡延伸。", "active_high"),
        _act("depth_break", "a-db-02", "深浅线附近气泡断续出现，位置固定。", "active_high"),
        _act("depth_break", "a-db-03", "坡下偶见短促涟漪，像有物体擦过。", "active_mid"),
        _act("depth_break", "a-db-04", "坎边只有缓慢流向，无额外扰动。", "active_low"),
        _act("depth_break", "a-db-05", "深浅线长时间无任何泡或纹。", "inactive"),
        _act("depth_break", "a-db-06", "坡坎倒影稳定，水面无局部异常。", "inactive"),
        _act("depth_break", "a-db-07", "坎边浑色突然上翻，数秒后沉下去。", "disturbed"),
    ],
    "channel": [
        _hab("channel", "h-ch-01", "两湾之间的收窄段，流向比两侧更直。"),
        _hab("channel", "h-ch-02", "水道宽度仅数米，对岸距离明显变近。"),
        _hab("channel", "h-ch-03", "收窄处水色略深，波纹更密。"),
        _act("channel", "a-ch-01", "水道内漂物速度比外围快，偶尔打旋。", "active_high"),
        _act("channel", "a-ch-02", "收窄段水面起连续细泡，随流排成线。", "active_high"),
        _act("channel", "a-ch-03", "水道中段偶见短波纹逆向扩散。", "active_mid"),
        _act("channel", "a-ch-04", "流向稳定但无额外水面征象。", "active_low"),
        _act("channel", "a-ch-05", "水道内长时间无泡、无漂物加速。", "inactive"),
        _act("channel", "a-ch-06", "收窄段平静，只有均匀流向纹。", "inactive"),
        _act("channel", "a-ch-07", "水面突然乱流一阵，漂物四散。", "disturbed"),
    ],
    "water_clear": [
        _hab("water_clear", "h-wc-01", "近岸可辨底质颜色，透光良好。"),
        _hab("water_clear", "h-wc-02", "水色清亮，远处仍能看到岸线倒影。"),
        _hab("water_clear", "h-wc-03", "浅处与深处颜色渐变柔和，无浑带。"),
        _act("water_clear", "a-wc-01", "清水底偶见细尘扬起，范围很小。", "active_high"),
        _act("water_clear", "a-wc-02", "水面有清晰单点气泡，破后不留浑圈。", "active_high"),
        _act("water_clear", "a-wc-03", "近岸偶见细线状涟漪，很快消失。", "active_mid"),
        _act("water_clear", "a-wc-04", "清水区只有微风细纹，无局部浑色。", "active_low"),
        _act("water_clear", "a-wc-05", "长时间底质颜色不变，无新扰动。", "inactive"),
        _act("water_clear", "a-wc-06", "水面透明稳定，无泡无漂物。", "inactive"),
        _act("water_clear", "a-wc-07", "一片清水突然变浊又慢慢澄清。", "disturbed"),
    ],
    "water_muddy": [
        _hab("water_muddy", "h-wm-01", "水色泥浆样，近岸看不清底。"),
        _hab("water_muddy", "h-wm-02", "悬浮颗粒多，光线下水体发灰。"),
        _hab("water_muddy", "h-wm-03", "浑水带与清水区之间有锯齿状分界。"),
        _act("water_muddy", "a-wm-01", "浑水区大片气泡翻起，夹着细泥色。", "active_high"),
        _act("water_muddy", "a-wm-02", "水面间歇冒出浑圈，扩散慢而宽。", "active_high"),
        _act("water_muddy", "a-wm-03", "泥色区偶见短促波纹，像底被擦过。", "active_mid"),
        _act("water_muddy", "a-wm-04", "浑水稳定，只有缓慢漂物移动。", "active_low"),
        _act("water_muddy", "a-wm-05", "泥色长时间不变，无新泡无翻泥。", "inactive"),
        _act("water_muddy", "a-wm-06", "水面死灰一片，缺少动态征象。", "inactive"),
        _act("water_muddy", "a-wm-07", "浑色突然上涌又沉下，留一圈浅痕。", "disturbed"),
    ],
    "water_green": [
        _hab("water_green", "h-wg2-01", "水色发绿，近岸可见藻类悬浮。"),
        _hab("water_green", "h-wg2-02", "绿水中反光偏暗，不像清水那样透亮。"),
        _hab("water_green", "h-wg2-03", "下风口绿膜略厚，颜色更深一线。"),
        _act("water_green", "a-wg2-01", "绿水中偶见气泡，破后带浅绿沫。", "active_high"),
        _act("water_green", "a-wg2-02", "水面有细鳞状纹，与藻类分布重合。", "active_high"),
        _act("water_green", "a-wg2-03", "绿膜区偶尔起小涡，很快平复。", "active_mid"),
        _act("water_green", "a-wg2-04", "绿水稳定，只有均匀微波。", "active_low"),
        _act("water_green", "a-wg2-05", "绿膜长时间无新泡、无局部翻动。", "inactive"),
        _act("water_green", "a-wg2-06", "水面颜色均匀静止，缺少动态点。", "inactive"),
        _act("water_green", "a-wg2-07", "一片绿膜被搅散又慢慢合拢。", "disturbed"),
    ],
    "water_tea": [
        _hab("water_tea", "h-wt-01", "水呈茶褐或酱油色，透光弱。"),
        _hab("water_tea", "h-wt-02", "茶色区有淡淡腐殖质气味。"),
        _hab("water_tea", "h-wt-03", "近岸落叶在水色中呈深褐轮廓。"),
        _act("water_tea", "a-wt-01", "茶色水面偶起细泡，破后留深色圈。", "active_high"),
        _act("water_tea", "a-wt-02", "水面间歇有轻响，像远处物体拍水。", "active_high"),
        _act("water_tea", "a-wt-03", "茶色区漂物偶尔加速又减速。", "active_mid"),
        _act("water_tea", "a-wt-04", "水体颜色稳定，无额外波纹。", "active_low"),
        _act("water_tea", "a-wt-05", "茶色水面长时间无泡、无拍水声。", "inactive"),
        _act("water_tea", "a-wt-06", "近岸静滞，只有极弱流向。", "inactive"),
        _act("water_tea", "a-wt-07", "水面突然翻起褐沫又散开。", "disturbed"),
    ],
    "shade_tree": [
        _hab("shade_tree", "h-st2-01", "树冠投影覆盖水面，亮暗交界清晰。"),
        _hab("shade_tree", "h-st2-02", "树荫下反光弱，颜色比亮水区深。"),
        _hab("shade_tree", "h-st2-03", "树影随日移缓慢漂移，边界柔和。"),
        _act("shade_tree", "a-st2-01", "树荫边缘偶见气泡，多在明暗线附近。", "active_high"),
        _act("shade_tree", "a-st2-02", "影区内水面轻颤，与亮区波纹不同步。", "active_high"),
        _act("shade_tree", "a-st2-03", "树影下偶起细涟漪，范围不大。", "active_mid"),
        _act("shade_tree", "a-st2-04", "影区平静，只有树影移动。", "active_low"),
        _act("shade_tree", "a-st2-05", "树荫区长时间无泡、无局部纹。", "inactive"),
        _act("shade_tree", "a-st2-06", "影区水面像静止，缺少扰动点。", "inactive"),
        _act("shade_tree", "a-st2-07", "影区突然起密波，亮区仍平静。", "disturbed"),
    ],
    "shade_cliff": [
        _hab("shade_cliff", "h-sc-01", "坝体或崖壁投影压在水面，条带状暗区。"),
        _hab("shade_cliff", "h-sc-02", "高结构遮挡下，风纹明显减弱。"),
        _hab("shade_cliff", "h-sc-03", "坝影边缘水色更深，与亮区对比强。"),
        _act("shade_cliff", "a-sc-01", "坝影边偶见成串气泡沿暗带分布。", "active_high"),
        _act("shade_cliff", "a-sc-02", "高影区水面间歇轻响，像远处拍击。", "active_high"),
        _act("shade_cliff", "a-sc-03", "影区偶见短波纹逆向扩散。", "active_mid"),
        _act("shade_cliff", "a-sc-04", "坝影下只有弱流向，无额外征象。", "active_low"),
        _act("shade_cliff", "a-sc-05", "影区长时间无泡、无拍水声。", "inactive"),
        _act("shade_cliff", "a-sc-06", "高影区水面稳定，缺少动态点。", "inactive"),
        _act("shade_cliff", "a-sc-07", "影区水面骤起乱纹又迅速平息。", "disturbed"),
    ],
    "sun_open": [
        _hab("sun_open", "h-so-01", "直射日光下，水面反光强烈刺眼。"),
        _hab("sun_open", "h-so-02", "无遮区颜色偏亮，与邻近影区对比明显。"),
        _hab("sun_open", "h-so-03", "强光下近岸底质可见度高于远水。"),
        _act("sun_open", "a-so-01", "亮区偶见单点气泡，反光中仍清晰。", "active_high"),
        _act("sun_open", "a-so-02", "强光区水面有异常断纹，与风向不完全一致。", "active_high"),
        _act("sun_open", "a-so-03", "亮区偶起短涟漪，很快融入反光。", "active_mid"),
        _act("sun_open", "a-so-04", "强光下只有均匀细浪，无局部扰动。", "active_low"),
        _act("sun_open", "a-so-05", "亮区长时间无泡、无异常纹。", "inactive"),
        _act("sun_open", "a-so-06", "水面反光稳定，像静止镜面。", "inactive"),
        _act("sun_open", "a-so-07", "亮区突然起一片碎浪又恢复。", "disturbed"),
    ],
    "wind_lee": [
        _hab("wind_lee", "h-wl-01", "岸线或地形遮挡后，水面纹弱而细。"),
        _hab("wind_lee", "h-wl-02", "背风侧漂物移动慢，几乎贴水漂移。"),
        _hab("wind_lee", "h-wl-03", "背风区颜色略深，像被护住。"),
        _act("wind_lee", "a-wl-01", "背风区偶见细泡，位置相对固定。", "active_high"),
        _act("wind_lee", "a-wl-02", "弱纹区水面轻颤，与迎风侧不同步。", "active_high"),
        _act("wind_lee", "a-wl-03", "背风侧偶起小涟漪，范围局限。", "active_mid"),
        _act("wind_lee", "a-wl-04", "背风区只有极弱微波。", "active_low"),
        _act("wind_lee", "a-wl-05", "背风侧长时间无泡、无漂物加速。", "inactive"),
        _act("wind_lee", "a-wl-06", "弱纹区平静，缺少动态征象。", "inactive"),
        _act("wind_lee", "a-wl-07", "背风区突然起密纹，像受远端风扰。", "disturbed"),
    ],
    "wind_fetch": [
        _hab("wind_fetch", "h-wf-01", "波纹顺向排列，水面有定向细浪。"),
        _hab("wind_fetch", "h-wf-02", "下风口漂物聚集，颜色略深。"),
        _hab("wind_fetch", "h-wf-03", "迎风侧水声略大，拍岸节奏更密。"),
        _act("wind_fetch", "a-wf-01", "下风口漂物打着转，偶尔带出涡纹。", "active_high"),
        _act("wind_fetch", "a-wf-02", "定向浪中夹杂异常断纹，方向不一。", "active_high"),
        _act("wind_fetch", "a-wf-03", "下风口偶见气泡随浪漂移。", "active_mid"),
        _act("wind_fetch", "a-wf-04", "定向浪稳定，无额外局部扰动。", "active_low"),
        _act("wind_fetch", "a-wf-05", "下风口长时间无新泡、无异常断纹。", "inactive"),
        _act("wind_fetch", "a-wf-06", "定向浪均匀，缺少动态点。", "inactive"),
        _act("wind_fetch", "a-wf-07", "下风口漂物突然四散又聚拢。", "disturbed"),
    ],
    "depth_shallow": [
        _hab("depth_shallow", "h-ds-01", "近岸延伸远，底质颜色清晰可见。"),
        _hab("depth_shallow", "h-ds-02", "浅水区水色偏亮，与远水对比明显。"),
        _hab("depth_shallow", "h-ds-03", "浅滩边常有细浪拍岸，节奏均匀。"),
        _act("depth_shallow", "a-ds-01", "浅水区底质偶被搅起，范围很小。", "active_high"),
        _act("depth_shallow", "a-ds-02", "浅处水面有细密气泡，破后不留浑圈。", "active_high"),
        _act("depth_shallow", "a-ds-03", "浅滩偶见短涟漪，很快消失。", "active_mid"),
        _act("depth_shallow", "a-ds-04", "浅水区只有拍岸纹，无额外扰动。", "active_low"),
        _act("depth_shallow", "a-ds-05", "浅处长时间无底质扰动、无新泡。", "inactive"),
        _act("depth_shallow", "a-ds-06", "浅滩平静，底质颜色稳定。", "inactive"),
        _act("depth_shallow", "a-ds-07", "浅区突然浑一片又慢慢澄清。", "disturbed"),
    ],
    "depth_mid": [
        _hab("depth_mid", "h-dm-01", "常见抛投落点深度，近岸看不清底但颜色均匀。"),
        _hab("depth_mid", "h-dm-02", "中等深度区水色稳定，无明显分层带。"),
        _hab("depth_mid", "h-dm-03", "抛投落点处颜色与近岸接近，过渡柔和。"),
        _act("depth_mid", "a-dm-01", "中等深度区偶见单点气泡，位置不固定。", "active_high"),
        _act("depth_mid", "a-dm-02", "水面间歇有细鳞状纹，范围中等。", "active_high"),
        _act("depth_mid", "a-dm-03", "中等深度区偶起短波纹。", "active_mid"),
        _act("depth_mid", "a-dm-04", "水体稳定，只有均匀微波。", "active_low"),
        _act("depth_mid", "a-dm-05", "中等深度区长时间无泡、无异常纹。", "inactive"),
        _act("depth_mid", "a-dm-06", "水面颜色均匀静止。", "inactive"),
        _act("depth_mid", "a-dm-07", "一片区域突然起密浪又恢复。", "disturbed"),
    ],
    "depth_deep": [
        _hab("depth_deep", "h-dd-01", "离岸远侧水色明显变深，近岸仍偏亮。"),
        _hab("depth_deep", "h-dd-02", "深侧缺少底质可见度，像一块深色布。"),
        _hab("depth_deep", "h-dd-03", "陡坎外侧颜色分层清晰，过渡窄。"),
        _act("depth_deep", "a-dd-01", "深侧水面偶见长条涟漪快速划过。", "active_high"),
        _act("depth_deep", "a-dd-02", "深色区间歇冒出大气泡，破后留宽圈。", "active_high"),
        _act("depth_deep", "a-dd-03", "深侧偶见短促波纹，方向不固定。", "active_mid"),
        _act("depth_deep", "a-dd-04", "深区只有弱流向，无额外征象。", "active_low"),
        _act("depth_deep", "a-dd-05", "深侧长时间无泡、无长条涟漪。", "inactive"),
        _act("depth_deep", "a-dd-06", "深色区平静，缺少动态点。", "inactive"),
        _act("depth_deep", "a-dd-07", "深区突然翻起浑色又沉下。", "disturbed"),
    ],
    "shore_reed": [
        _hab("shore_reed", "h-sr-01", "岸线以芦苇、蒲草为主，茎秆密集。"),
        _hab("shore_reed", "h-sr-02", "芦苇根附近水色略深，有细碎漂浮物。"),
        _hab("shore_reed", "h-sr-03", "软质岸脚被水浸润，颜色发暗。"),
        _act("shore_reed", "a-sr-01", "芦苇根际偶见细泡，沿草线分布。", "active_high"),
        _act("shore_reed", "a-sr-02", "草茎轻颤，节奏与风向不完全一致。", "active_high"),
        _act("shore_reed", "a-sr-03", "岸脚偶见小涟漪向草丛扩散。", "active_mid"),
        _act("shore_reed", "a-sr-04", "芦苇岸只有轻波拍草，无额外扰动。", "active_low"),
        _act("shore_reed", "a-sr-05", "草岸长时间无新泡、无茎秆异常摇。", "inactive"),
        _act("shore_reed", "a-sr-06", "软质岸脚平静，漂物贴岸慢移。", "inactive"),
        _act("shore_reed", "a-sr-07", "草岸突然成片晃动又平息。", "disturbed"),
    ],
    "shore_rock": [
        _hab("shore_rock", "h-srk-01", "岸线为乱石或卵石，岸脚参差不齐。"),
        _hab("shore_rock", "h-srk-02", "石缝间常卡漂物，颜色与主体水不同。"),
        _hab("shore_rock", "h-srk-03", "石岸拍浪声更脆，节奏比泥岸密。"),
        _act("shore_rock", "a-srk-01", "石缝间偶见气泡冒出，位置固定。", "active_high"),
        _act("shore_rock", "a-srk-02", "石岸脚水面间歇起细沫。", "active_high"),
        _act("shore_rock", "a-srk-03", "乱石区偶见短波纹沿石缘扩散。", "active_mid"),
        _act("shore_rock", "a-srk-04", "石岸只有拍浪纹，无额外征象。", "active_low"),
        _act("shore_rock", "a-srk-05", "石缝长时间无泡、无新沫。", "inactive"),
        _act("shore_rock", "a-srk-06", "石岸平静，漂物卡在缝间不动。", "inactive"),
        _act("shore_rock", "a-srk-07", "石岸脚突然翻起浑沫又散开。", "disturbed"),
    ],
    "shore_mud": [
        _hab("shore_mud", "h-sm-01", "岸脚为软泥，颜色深褐，近水有浸润带。"),
        _hab("shore_mud", "h-sm-02", "泥岸附近水色略浑，细颗粒悬浮。"),
        _hab("shore_mud", "h-sm-03", "泥岸拍浪声闷，波纹扩散慢。"),
        _act("shore_mud", "a-sm-01", "泥岸脚偶见浑圈气泡，扩散宽而慢。", "active_high"),
        _act("shore_mud", "a-sm-02", "软泥区水面间歇翻起细泥色。", "active_high"),
        _act("shore_mud", "a-sm-03", "泥岸偶见短波纹，像底被擦过。", "active_mid"),
        _act("shore_mud", "a-sm-04", "泥岸只有闷声拍浪，无额外扰动。", "active_low"),
        _act("shore_mud", "a-sm-05", "泥岸长时间无浑圈、无翻泥。", "inactive"),
        _act("shore_mud", "a-sm-06", "软泥区平静，水色稳定。", "inactive"),
        _act("shore_mud", "a-sm-07", "泥岸脚突然浑一片又慢慢沉清。", "disturbed"),
    ],
    "shore_hard": [
        _hab("shore_hard", "h-sh-01", "岸线为水泥或条石，岸脚整齐。"),
        _hab("shore_hard", "h-sh-02", "硬岸反光强，近岸水色偏亮。"),
        _hab("shore_hard", "h-sh-03", "硬化岸脚无草线，与水分界笔直。"),
        _act("shore_hard", "a-sh-01", "硬岸脚偶见单点气泡，破后不留痕。", "active_high"),
        _act("shore_hard", "a-sh-02", "条石缝间间歇有细沫随浪进出。", "active_high"),
        _act("shore_hard", "a-sh-03", "硬岸偶见短涟漪，沿岸线传播。", "active_mid"),
        _act("shore_hard", "a-sh-04", "硬化岸只有均匀拍浪，无局部扰动。", "active_low"),
        _act("shore_hard", "a-sh-05", "硬岸长时间无泡、无条石缝沫。", "inactive"),
        _act("shore_hard", "a-sh-06", "岸脚平静，反光稳定。", "inactive"),
        _act("shore_hard", "a-sh-07", "硬岸脚突然起密浪又恢复。", "disturbed"),
    ],
}

SPOT_CLUE_TEXTS: list[tuple] = []
for _tag, _rows in _TAG_CLUE_SEEDS.items():
    SPOT_CLUE_TEXTS.extend(_rows)

# ---------------------------------------------------------------------------
# Pond / spot IDs (mirror shared/pondCatalog + pond-novice)
# ---------------------------------------------------------------------------
POND_SPOT_PREFIXES: list[tuple[str, str]] = [
    ("pond-calm", "calm"),
    ("pond-mist", "mist"),
    ("pond-sunset", "sunset"),
    ("pond-bamboo", "bamboo"),
    ("pond-reed", "reed"),
    ("pond-crystal", "crystal"),
    ("pond-lotus", "lotus"),
    ("pond-mirror", "mirror"),
    ("pond-willow", "willow"),
    ("pond-stone", "stone"),
    ("pond-spring", "spring"),
    ("pond-dusk", "dusk"),
    ("pond-pine", "pine"),
    ("pond-coral", "coral"),
    ("pond-moon", "moon"),
    ("pond-fern", "fern"),
    ("pond-ridge", "ridge"),
    ("pond-harbor", "harbor"),
    ("pond-orchid", "orchid"),
    ("pond-frost", "frost"),
    ("pond-novice", "novice"),
]

SPOTS_PER_POND = 20


def _hash_seed(key: str) -> int:
    return int(hashlib.sha256(key.encode("utf-8")).hexdigest()[:12], 16)


def tags_for_spot(pond_id: str, spot_index: int) -> list[str]:
    """Stable pseudo-random multi-category tags per spot (v0 seed; manual tune later)."""
    seed = f"{pond_id}:spot-{spot_index}"
    h = _hash_seed(seed)
    tags: set[str] = set()
    tags.add(TERRAIN_TAGS[h % len(TERRAIN_TAGS)])
    tags.add(DEPTH_TAGS[(h >> 4) % len(DEPTH_TAGS)])
    extra_count = 2 + ((h >> 8) % 3)  # 2–4 optional tags
    used_optional: set[str] = set()
    for i in range(extra_count):
        pool = [t for t in OPTIONAL_TAGS if t not in used_optional]
        if not pool:
            break
        pick = pool[(h >> (12 + i * 5)) % len(pool)]
        used_optional.add(pick)
        tags.add(pick)
    return sorted(tags)


def build_pond_spot_tag_rows() -> list[tuple[str, str, str]]:
    """pondId, spotId, tags (comma-separated)."""
    rows: list[tuple[str, str, str]] = []
    for pond_id, prefix in POND_SPOT_PREFIXES:
        for i in range(1, SPOTS_PER_POND + 1):
            spot_id = f"{prefix}-spot-{i}"
            tag_list = tags_for_spot(pond_id, i)
            rows.append((pond_id, spot_id, ",".join(tag_list)))
    return rows


def iter_all_spot_ids() -> Iterable[tuple[str, str]]:
    for pond_id, prefix in POND_SPOT_PREFIXES:
        for i in range(1, SPOTS_PER_POND + 1):
            yield pond_id, f"{prefix}-spot-{i}"
