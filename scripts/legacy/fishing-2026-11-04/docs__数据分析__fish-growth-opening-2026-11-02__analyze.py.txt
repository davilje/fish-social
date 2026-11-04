"""开塘出生分布与渐近成长候选方案。离线分析，不修改业务配置。"""
import json
import math
from pathlib import Path
from html import escape
import numpy as np
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
from matplotlib.font_manager import FontProperties

OUT = Path(__file__).resolve().parent
ROOT = OUT.parents[2]
DATA = ROOT / 'shared/generated/game-data'
read = lambda name: json.loads((DATA / name).read_text())
stats = read('fish_quality_stats.json')
ponds = read('ponds.json')
weight_rows = read('pond_category_quality_weights.json')
species = {r['speciesId']: r for r in read('fish_species.json')}
pool = read('pond_fish_pool.json')
qids = [r['quality'] for r in stats]
names = [r['displayName'] for r in stats]
caps = np.array([r['sizeCapM'] for r in stats])
t80 = np.array([1, 2, 4, 5, 7, 10, 14], dtype=float)
DAILY_TARGET = 100
LOW_WATERMARK = 40
LOW_REFILL_TARGET = 80
colors = ['#818994', '#409d71', '#4b83bf', '#9c6db6', '#cf6664', '#df953c', '#bd9a28']
seed = 20261102
rng = np.random.default_rng(seed)

def fmt(x):
    mm = int(math.floor(float(x) * 1000 + .5))
    if mm < 1000:
        return f'{mm / 10:.1f}cm'
    if mm < 9995:
        return f'{mm / 1000:.2f}m'
    return f'{mm / 1000:.1f}m'

def rate(cap, birth, days80):
    return np.log((1 - birth / cap) / .2) / days80

def length(age, cap, birth, days80):
    return cap - (cap - birth) * np.exp(-rate(cap, birth, days80) * np.asarray(age))

def inverse(size, cap, birth, days80):
    return -np.log((cap - size) / (cap - birth)) / rate(cap, birth, days80)

def initial(cap, birth, days80, u):
    upper = length(14, cap, birth, days80)
    size = birth + u * (upper - birth)
    return size, inverse(size, cap, birth, days80)

def supplement_count(current_count, daily=False):
    # 日结与低水位检查重合时，优先补足100；以事务中的真实库存计算缺口。
    if daily:
        return max(0, DAILY_TARGET - current_count)
    if current_count <= LOW_WATERMARK:
        return max(0, LOW_REFILL_TARGET - current_count)
    return 0

assert [supplement_count(n) for n in [0, 40, 41, 80, 100]] == [80, 40, 0, 0, 0]
assert [supplement_count(n, daily=True) for n in [0, 40, 41, 80, 100]] == [100, 60, 59, 20, 0]

def table(heads, rows):
    return '| ' + ' | '.join(heads) + ' |\n| ' + ' | '.join(['---'] * len(heads)) + ' |\n' + '\n'.join('| ' + ' | '.join(map(str, row)) + ' |' for row in rows)

categories = list(dict.fromkeys(p['pondCategory'] for p in ponds))
catnames = {r['pondCategory']: r['pondCategoryName'] for r in weight_rows}
weights = {}
for cat in categories:
    w = np.array([next((r['spawnWeight'] for r in weight_rows if r['pondCategory'] == cat and r['quality'] == q), 0) for q in qids], dtype=float)
    weights[cat] = w / w.sum()

age_points = [0, 1, 3, 7, 14, 28, 42]
cohort_days = [0, 3, 7, 14, 28]
ref_rows, open_rows, retained_rows, milestone_rows = [], [], [], []
reference = []
for i, name in enumerate(names):
    cap, b, d = caps[i], .04, t80[i]
    k = float(rate(cap, b, d))
    ls = length(np.array(age_points), cap, b, d)
    upper = float(length(14, cap, b, d))
    quantiles = b + np.array([.1, .5, .9]) * (upper - b)
    median_age = float(inverse(quantiles[1], cap, b, d))
    mean_size = (b + upper) / 2
    # 初始化长度均匀，因此均值的后续变化可精确计算。
    retained = [float(cap - (cap - mean_size) * math.exp(-k * t)) for t in cohort_days]
    ref_rows.append([name, fmt(cap), f'{d:g}天'] + [fmt(x) for x in ls[1:]])
    open_rows.append([name, f'{fmt(b)}～{fmt(upper)}', *[fmt(x) for x in quantiles], f'{median_age:.2f}天'])
    retained_rows.append([name] + [fmt(x) for x in retained])
    milestone_rows.append([name] + [f'{float(inverse(cap * p, cap, b, d)):.1f}天' for p in [.8, .95, .99]])
    reference.append(dict(quality=qids[i], name=name, cap_m=float(cap), birth_m=b, t80_days=float(d), age_points=age_points,
                          lengths_m=ls.tolist(), initial_p10_p50_p90_m=quantiles.tolist(), initial_median_age_days=median_age,
                          retained_mean_m=retained))

# 按原品质设计概率计算体长。统一出生4cm，隔离鱼种出生体长的影响。
N = 100000
bins = np.array([0, .2, .5, 1, 10, np.inf])
bin_names = ['<20cm', '20～<50cm', '50cm～<1m', '1～<10m', '≥10m']
stages = np.array([0, .3, .6, .8, .95, 1.00000001])
stage_names = ['<30%上限', '30～60%', '60～80%', '80～95%', '≥95%上限']
stage_colors = ['#c7e3da', '#84c6b4', '#459d94', '#34717b', '#35475e']
pond_results = []
advanced_stages = None
category_samples = {}
for pond in ponds:
    cat = pond['pondCategory']
    if cat in category_samples:
        result = dict(category_samples[cat], pond_id=pond['pondId'], name=pond['name'], initial_population=pond['initialPopulation'])
        result['initial_length_bin_expected_counts'] = (np.array(result['initial_length_bin_shares']) * pond['initialPopulation']).tolist()
        pond_results.append(result)
        continue
    qi = rng.choice(7, N, p=weights[cat])
    cap = caps[qi] * rng.triangular(.85, 1, 1.15, N)
    birth = np.full(N, .04)
    size, age = initial(cap, birth, t80[qi], rng.random(N))
    k = rate(cap, birth, t80[qi])
    shares = np.histogram(size, bins)[0] / N
    stages_by_day = []
    for delta in cohort_days:
        future = length(age + delta, cap, birth, t80[qi])
        stages_by_day.append((np.histogram(future / cap, stages)[0] / N).tolist())
        assert np.all(future >= size - 1e-10)
        assert np.all(future <= cap + 1e-10)
    assert np.all(age >= 0) and np.all(age <= 14 + 1e-9)
    assert np.allclose(length(t80[qi], cap, birth, t80[qi]), .8 * cap)
    assert np.allclose(length(age, cap, birth, t80[qi]), size)
    assert abs(shares.sum() - 1) < 1e-9
    result = dict(pond_id=pond['pondId'], name=pond['name'], category=cat, initial_population=pond['initialPopulation'],
                  quality_probabilities=weights[cat].tolist(), initial_length_bin_shares=shares.tolist(),
                  initial_length_bin_expected_counts=(shares * pond['initialPopulation']).tolist(),
                  initial_median_age_days=float(np.median(age)), stages_by_day=stages_by_day)
    pond_results.append(result)
    category_samples[cat] = result
    if pond['pondId'] == 'pond-calm':
        advanced_stages = np.array(stages_by_day)

weight_table = table(['鱼塘类别', *names], [[catnames[c], *[f'{w * 100:.2f}%' for w in weights[c]]] for c in categories])
growth_table = table(['品质', '上限基准', '80%时间', '1天', '3天', '7天', '14天', '28天', '42天'], ref_rows)
opening_table = table(['品质', '初始可取范围', 'P10', '中位体长', 'P90', '鱼龄中位数'], open_rows)
retained_table = table(['品质', '开塘', '开塘后3天', '7天', '14天', '28天'], retained_rows)
milestones_table = table(['品质', '80%上限', '95%上限', '99%上限'], milestone_rows)
pond_table = table(['鱼塘', '初始条数', *bin_names], [[r['name'], r['initial_population'], *[f'{x:.1f}' for x in r['initial_length_bin_expected_counts']]] for r in pond_results])
gray_examples = []
for cap in [.192, .226]:
    gray_examples.append([fmt(cap)] + [fmt(x) for x in length(np.array([1, 3, 7, 14]), cap, .04, t80[0])])
individual_table = table(['个体上限', '1天', '3天', '7天', '14天'], gray_examples)

# 出生日期均匀和倒推日期两种初始化的对照，均用普通鱼 4cm→20cm。
cap, b, d = .2, .04, t80[0]
k = float(rate(cap, b, d))
threshold_age = float(inverse(cap * .95, cap, b, d))
uniform_age_near_cap = max(0, 14 - threshold_age) / 14
upper = float(length(14, cap, b, d))
inverse_age_near_cap = (upper - .95 * cap) / (upper - b)

font = FontProperties(fname='/System/Library/Fonts/Hiragino Sans GB.ttc')
plt.rcParams.update({'font.family': font.get_name(), 'axes.unicode_minus': False, 'font.size': 11,
                     'axes.spines.top': False, 'axes.spines.right': False, 'axes.facecolor': '#fbfcfc', 'figure.facecolor': 'white'})
fig, axs = plt.subplots(2, 2, figsize=(14, 10), layout='constrained')
ages = np.linspace(0, 42, 600)
ax = axs[0, 0]
for i in range(7):
    ax.plot(ages, length(ages, caps[i], .04, t80[i]) / caps[i] * 100, color=colors[i], label=names[i], lw=2)
ax.axvline(14, color='#777', ls=':', lw=1)
ax.scatter([14], [80], c=[colors[-1]], s=45, zorder=4)
ax.set(title='单条鱼：14天以后仍会继续长大', xlabel='实际鱼龄（天）', ylabel='长度 / 个体上限（%）', ylim=(0, 104))
ax.legend(ncol=4, fontsize=9, loc='lower right')
ax.grid(alpha=.14)
ax = axs[0, 1]
x = np.arange(7)
q10 = np.array([r['initial_p10_p50_p90_m'][0] / r['cap_m'] * 100 for r in reference])
q50 = np.array([r['initial_p10_p50_p90_m'][1] / r['cap_m'] * 100 for r in reference])
q90 = np.array([r['initial_p10_p50_p90_m'][2] / r['cap_m'] * 100 for r in reference])
ax.errorbar(x, q50, yerr=[q50 - q10, q90 - q50], fmt='none', ecolor='#73968d', lw=12, alpha=.55, capsize=6)
ax.scatter(x, q50, c=colors, s=48, zorder=3)
ax.set(title='开塘：中间80%的个体体长范围（P10～P90）', xticks=x, xticklabels=names, ylabel='长度 / 个体上限（%）', ylim=(0, 104))
ax.text(.03, .05, '点为中位数；出生4cm、上限取品质基准', transform=ax.transAxes, fontsize=10, color='#52635f')
ax.grid(axis='y', alpha=.14)
ax = axs[1, 0]
ages14 = np.linspace(0, 14, 500)
for i in [0, 3, 6]:
    k = float(rate(caps[i], .04, t80[i]))
    density = k * np.exp(-k * ages14) / (1 - math.exp(-k * 14))
    ax.plot(ages14, density, color=colors[i], label=names[i], lw=2)
ax.set(title='倒推出生日期：低品质更偏向近期出生', xlabel='开塘时鱼龄（天）', ylabel='概率密度（每天）')
ax.legend()
ax.grid(alpha=.14)
ax = axs[1, 1]
left = np.zeros(len(cohort_days))
for j in range(5):
    vals = advanced_stages[:, j] * 100
    bars = ax.barh(np.arange(len(cohort_days)), vals, left=left, color=stage_colors[j], label=stage_names[j], height=.62)
    for row, value in enumerate(vals):
        if value > 9:
            ax.text(left[row] + value / 2, row, f'{value:.0f}%', ha='center', va='center', fontsize=10,
                    color='white' if j >= 2 else '#294b43')
    left += vals
ax.set(title='千岛湖原有鱼群：无人捕捞时会逐渐变老', yticks=np.arange(len(cohort_days)),
       yticklabels=['开塘' if v == 0 else f'+{v}天' for v in cohort_days], xlabel='塘内存量比例（%）', xlim=(0, 100))
ax.invert_yaxis()
ax.legend(ncol=3, fontsize=9, loc='upper center', bbox_to_anchor=(.5, -.14))
fig.suptitle('开塘出生范围 -14～0天 · 品质上限 + 先快后慢的成长', fontsize=19, fontweight='bold')
fig.savefig(OUT / 'growth-opening.png', dpi=160)
fig.savefig(OUT / 'growth-opening.svg')
plt.close(fig)

report = f'''# 开塘鱼群体长与成长变化报告

2026-11-03更新 · 方案参数与离线推演，尚未替换业务算法。

## 结论

保留每个鱼塘原有品质概率和鱼种池；每条鱼的出生时间限制在开塘前14～0天。先铺开每条鱼的初始体长，再反推出生日期。至尊鱼自然成长14天，达到自身上限的80%；之后继续变大，越接近上限越慢。

“前14天”是初始化鱼龄范围，不是鱼的寿命或停止成长时间。

## 本轮确认的补鱼与成长调整

- 普通鱼自然达到个体上限80%的时间改为1天；优良鱼改为2天。
- 低库存每小时检查一次：剩余≤40条时补到80条；41条及以上不触发本轮低水位补鱼。
- 每日固定结算仍补足100条。日结与低水位检查在同一时刻发生时，直接按日结目标补足100条，合并处理，避免重复补鱼。
- 补鱼按实际缺口计算。例如低水位检查时剩40条补40条；日结时剩40条补60条，剩80条补20条。
- 自然成长按真实鱼龄计算，未额外加速的普通鱼出生满24小时达到80%。每天同一时刻补入的普通幼鱼，到次日同一时刻均达到80%或以上；日间临时补鱼需按实际鱼龄判断，跨过零点不代表已经过了24小时。
- 此处的低库存规则已经更新，前半部分图表只模拟自然成长；后续捕捞章节是固定成功次数的条件推演，尚非完整会话验证。70%幼鱼、25%成长鱼、5%较成熟鱼，以及每日营养增益上限6小时，仍作为待评估建议，尚未作为已确认参数实施。

## 1. 哪些已明确，哪些是本次参考参数

- 已明确：原品质分布；品质上限基准20cm、35cm、50cm、80cm、3m、15m、40m；每条鱼有固定的个体上限波动；出生于前14～0天；普通1天、优良2天、至尊14天达到80%；成长先快后慢。
- 当前对比采用各品质80%时间1、2、4、5、7、10、14天。普通、优良已按本轮要求更新，至尊14天沿用既定约束，其他天数保留前次参考参数。这里的80%指“实际长度/该鱼个体上限”，不是完成剩余生长空间的80%。
- 为量化个体波动，鱼塘样本暂用上限基准×三角分布(0.85,1.00,1.15)，以基准附近最常见。±15%并非此前已定案的最终范围。
- 本报告所有样本统一出生4cm，隔离鱼种出生体长对结果的影响；它不表示正式设计中所有鱼种都从4cm出生。单品质对比表上限取品质基准；鱼塘统计表才叠加候选的个体上限波动。接入鱼种出生体长时，必须确保出生体长低于个体上限80%，建议最多为个体上限25%，再重新生成统计表。
- 本轮各品质使用固定的80%时间，鱼种影响出生体长，个体上限影响实际每小时增长多少。额外的鱼种速度系数与随机速度未叠加进本次推演；若保留，需重新校准，保证最慢至尊个体也在14天内到80%。
- 前半部分采用纯自然成长情景：跟踪同一批存活鱼，不包含捕捞、补鱼、自然死亡、回鱼、打窝或脱钩加成。附加的“一天捕捞与补鱼后分布”章节另列条件捕获模型与假设。

## 2. 原有品质分布

{weight_table}

以上是原设计配置权重归一化后的抽取概率，不是每塘固定配额，也不是成功钓获比例。巨物塘原权重之和119，按119归一化。普通鱼塘初始100条；新手塘仍为原配置1条，不扩充为100条。

核对代码时还发现：千岛湖、太湖、洪泽湖、万绿湖、北江的现有鱼种池只覆盖到第6品质，至尊权重抽中后会触发回退。本报告按你要求保留原设计品质概率，不把该回退结果当作目标分布；正式落实新塘初始化前，需要处理这个既有配置冲突。

## 3. 成长曲线

令C为个体上限、B为出生体长、t为实际鱼龄（天）、T为该品质80%时间：

```
k = ln((C-B)/(0.2×C)) / T
L(t) = C - (C-B) × exp(-k×t)
```

因此L(0)=B，L(T)=0.8C。增长速度k(C-B)exp(-kt)一直下降；14天后仍增长。数学上渐近上限，存储或显示四舍五入后仍可能显示成上限。

| 品质 | 80%上限时间 | 95%上限时间 | 99%上限时间 |
| --- | --- | --- | --- |
''' + '\n'.join('| ' + ' | '.join(row) + ' |' for row in milestone_rows) + f'''

参考单条鱼的实际鱼龄变化（均从4.0cm出生）：

{growth_table}

同品质不同个体的差异示例：

{individual_table}

这两条普通鱼都在1天达到各自上限的80%，但长度不同；它们初始出生日期也可不同，因此同一时刻的长度差异还会进一步变化。

## 4. 出生日期如何分布

对每条鱼先计算它14天时的长度L14，再抽取U∈[0,1]：

```
开塘长度 = B + U × (L14-B)
开塘鱼龄 = -ln((C-开塘长度)/(C-B)) / k
出生时间 = 开塘时间 - 开塘鱼龄
```

这样每条鱼的初始体长在B～L14之间均匀分布，出生日期自动落在前14～0天。可用分层抽样让同品质的多个个体均匀覆盖体长区间，区间内部保留随机；稀有品质只有一条时照常随机，不保证它是大鱼。

倒推后的鱼龄服从0～14天内的截断指数分布：f(t)=k×exp(-kt)/(1-exp(-14k))。它不是额外抽一次年龄，而是同一个体长分布的另一种表达。

这么选的原因：若普通鱼出生日期在14天内均匀分布，已有{uniform_age_near_cap*100:.1f}%超过自身上限的95%；改成先铺体长再倒推，降至{inverse_age_near_cap*100:.1f}%。普通鱼不会开塘就大面积挤在极限附近。至尊最多14天鱼龄，因此开塘不会自然出现超过个体上限80%的至尊鱼。

每个品质的初始化分布如下（4cm出生、基准上限）：

{opening_table}

P10～P90是跨大量初始化个体的中间80%范围，不是一个只有1条至尊鱼的鱼塘内实际分布。均匀长度抽样使中位体长也等于平均体长。

## 5. 初始鱼塘会是什么样

以下按各类鱼塘原设计品质概率，每类别抽100,000个统计样本，固定种子20261102。统一出生4cm，并应用上述候选上限波动。同类鱼塘使用同一组估计值，方便比较；未将鱼种出生体长差异混入这次成长曲线对比。

表中为折算到原配置初始条数的“期望条数”，不是新增10万条鱼，也不是每塘强制配额。实际100条样本可能没有至尊鱼，也可能有多条；小数仅用于显示期望，逐格取整可能不等于总条数。

{pond_table}

## 6. 开塘以后，原来的鱼会怎么变

以下只跟踪原有存活鱼：以开塘鱼龄为起点继续成长，不能在开塘时把鱼龄清零。平均体长如下（4cm出生、基准上限）：

{retained_table}

“开塘后14天”和“鱼龄14天”不同：原始鱼在开塘后14天的实际鱼龄是14～28天，所以至尊鱼群此时平均长度已经超过32m。

![成长、初始分布、出生日期与存量变化](growth-opening.png)

## 7. 这套设计能解决什么，还剩什么

开塘时的低品质鱼不再因年龄均匀而大部分接近上限；品质、个体上限、出生体长、出生日期共同决定玩家钓上来的长度。自然成长不再14天停止。

但同一批鱼若长期无人捕捞、也无自然更新，最终仍会接近各自上限；个体上限差异只会让它们显示不同长度。反过来，20人持续捕捞、只补新生鱼，仍可能以幼鱼为主。本报告明确不把初始分布当作长期生态已经稳定的证明。

现行脱钩按当前体长增加2%的机制、打窝的额外长度、回鱼增长，都可能改变这里的自然成长轨迹。落实时须把它们一起接入、限制不超过个体上限，并分别记录自然鱼龄与额外增长。本次没有将这些加成伪造成出生更早；初始化也不编造吃饵、咬钩或脱钩历史。

## 数据与复现

- 品质与上限：shared/generated/game-data/fish_quality_stats.json。
- 鱼塘品质权重：shared/generated/game-data/pond_category_quality_weights.json。
- 初始数量、鱼种池：ponds.json、pond_fish_pool.json、fish_species.json。
- 原选种规则：shared/gameData.client.ts 的 pickSpawnFish / pickPondSpeciesForQuality。
- 本报告只新增离线分析文件、图表和预览入口，没有修改成长、补鱼或捕捞业务配置。
- 原始汇总：results.json；复现脚本：analyze.py（numpy、matplotlib）。校验了出生鱼龄范围、公式反解、14天80%标定、体长单调性、上限约束及分布总和。
'''
harvest_report = OUT / 'harvest-report.md'
if harvest_report.exists():
    # 将专项推演同步进入稳定总预览，不覆盖此前的自然成长对比。
    harvest_md = harvest_report.read_text()
    harvest_md = '\n'.join('#' + line if line.startswith('#') else line for line in harvest_md.splitlines())
    report += '\n\n' + harvest_md + '\n'
full_pond_report = OUT / 'full-pond-report.md'
if full_pond_report.exists():
    full_md = full_pond_report.read_text()
    full_md = '\n'.join('#' + line if line.startswith('#') else line for line in full_md.splitlines())
    report += '\n\n' + full_md + '\n'
mixed_report = OUT / 'restock-50-40-10-report.md'
if mixed_report.exists():
    mixed_md = mixed_report.read_text()
    mixed_md = '\n'.join('#' + line if line.startswith('#') else line for line in mixed_md.splitlines())
    report += '\n\n' + mixed_md + '\n'
cycles_report = OUT / 'occupancy-cycles-report.md'
if cycles_report.exists():
    cycles_md = cycles_report.read_text()
    cycles_md = '\n'.join('#' + line if line.startswith('#') else line for line in cycles_md.splitlines())
    report += '\n\n' + cycles_md + '\n'
checklist_file = OUT / '完整修改清单.md'
if checklist_file.exists():
    checklist_md = checklist_file.read_text()
    checklist_md = '\n'.join('#' + line if line.startswith('#') else line for line in checklist_md.splitlines())
    report += '\n\n' + checklist_md + '\n'
options_file = OUT / '额外成长与旧鱼兼容选项.md'
if options_file.exists():
    options_md = options_file.read_text()
    options_md = '\n'.join('#' + line if line.startswith('#') else line for line in options_md.splitlines())
    report += '\n\n' + options_md + '\n'
(OUT / '历史报告-出生口径.md').write_text(report)
result = dict(revised_at='2026-11-03', seed=seed, samples_per_category=N, quality_names=names, quality_caps_m=caps.tolist(), t80_days=t80.tolist(),
              replenishment_policy=dict(check_interval_hours=1, low_watermark=LOW_WATERMARK,
                                        low_refill_target=LOW_REFILL_TARGET, daily_target=DAILY_TARGET,
                                        daily_takes_priority=True, simulated_with_catches=False),
              reference=reference, length_bins=bin_names, cohort_days=cohort_days, relative_stages=stage_names,
              ponds=pond_results, gray_initial_over95_uniform_age=uniform_age_near_cap,
              gray_initial_over95_inverse_age=inverse_age_near_cap,
              assumptions=dict(cap_factor='Triangular(0.85,1,1.15), candidate only', birth='0.04m reference for all samples; species birth variation not simulated',
                               initial_length='Uniform(birth,lengthAtAge14)', speed='fixed T80 by quality', events='none'))
(OUT / 'results.json').write_text(json.dumps(result, ensure_ascii=False, indent=2))

# 简单的报告展示页，不依赖外部库，支持手机横向滚动表格。
def md_to_html(md):
    lines, output, i = md.splitlines(), [], 0
    while i < len(lines):
        line = lines[i]
        if line.startswith('```'):
            code = []
            i += 1
            while i < len(lines) and not lines[i].startswith('```'):
                code.append(lines[i]); i += 1
            output.append('<pre>' + escape('\n'.join(code)) + '</pre>')
        elif line.startswith('|'):
            rows = []
            while i < len(lines) and lines[i].startswith('|'):
                rows.append([s.strip() for s in lines[i].strip('|').split('|')]); i += 1
            header = '<tr>' + ''.join('<th>' + escape(x) + '</th>' for x in rows[0]) + '</tr>'
            body = ''.join('<tr>' + ''.join('<td>' + escape(x) + '</td>' for x in row) + '</tr>' for row in rows[2:])
            output.append('<div class="table"><table><thead>' + header + '</thead><tbody>' + body + '</tbody></table></div>')
            continue
        elif line.startswith('#'):
            level = min(6, len(line) - len(line.lstrip('#')))
            output.append(f'<h{level}>' + escape(line.lstrip('#').strip()) + f'</h{level}>')
        elif line.startswith('!['):
            alt, source = line[2:].split('](', 1)
            output.append('<img src="' + escape(source.rsplit(')', 1)[0], quote=True) + '" alt="' + escape(alt, quote=True) + '">')
        elif line.startswith('- '): output.append('<p class="item">• ' + escape(line[2:]) + '</p>')
        elif line.strip(): output.append('<p>' + escape(line) + '</p>')
        i += 1
    return '\n'.join(output)

html = '''<!doctype html><html lang="zh-CN"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>开塘鱼群 · 14天80%成长方案</title><style>
:root{color-scheme:light}*{box-sizing:border-box}body{margin:0;background:#f2f5f1;color:#253b34;font:16px/1.8 system-ui,sans-serif}main{max-width:1150px;margin:30px auto;padding:30px 38px;background:#fff;border-radius:16px}h1{font-size:29px;line-height:1.4}h2{font-size:22px;margin-top:34px;border-bottom:1px solid #dce7e0;padding-bottom:8px}p{margin:12px 0;overflow-wrap:anywhere}.item{margin:5px 0}.table{overflow:auto;margin:18px 0}table{border-collapse:collapse;white-space:nowrap;font-size:14px;width:100%}td,th{text-align:left;border-bottom:1px solid #e4eae7;padding:9px 12px}th{background:#e9f1ec}pre{overflow:auto;background:#eef4f0;padding:16px;border-radius:8px;line-height:1.65;font-size:14px}img{display:block;width:100%;height:auto;margin:22px 0}@media(max-width:600px){main{margin:0;padding:20px 15px;border-radius:0}h1{font-size:24px}h2{font-size:20px}}
</style></head><body><main>''' + md_to_html(report) + '</main></body></html>'
(OUT / '历史推演.html').write_text(html)
print(json.dumps(dict(reference=reference, gray_compare=[uniform_age_near_cap,inverse_age_near_cap],
                     category_reference_ponds=[r for r in pond_results if next(p['pondId'] for p in ponds if p['pondCategory']==r['category'])==r['pond_id']]), ensure_ascii=False, indent=2))
