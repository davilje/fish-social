"""成功捕获数量给定时，推演新成长方案下的鱼群变化；不是完整会话模拟。"""
import json
import math
from pathlib import Path
import numpy as np
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
from matplotlib.font_manager import FontProperties

OUT = Path(__file__).resolve().parent
DATA = OUT.parents[2] / 'shared/generated/game-data'
stats = json.loads((DATA / 'fish_quality_stats.json').read_text())
qids = [r['quality'] for r in stats]
names = [r['displayName'] for r in stats]
caps_base = np.array([r['sizeCapM'] for r in stats])
base = np.array([r['biteBaseAtMaxSize'] for r in stats])
weights = np.array([38, 28, 18, 9, 4, 2, 1], dtype=float)
prob = weights / weights.sum()
t80 = np.array([1, 2, 4, 5, 7, 10, 14], dtype=float)
quality_fit = np.array([1.1, 1.05, 1, .7, .5, .4, .3])
N = 4000
DAYS = 30
TAKE = 20
SEED = 2026110302
bin_edges = np.array([0, .1, .2, .5, 1, 10, np.inf])
bin_names = ['<10cm', '10～<20cm', '20～<50cm', '50cm～<1m', '1～<10m', '≥10m']

def growth(q, cap, born, now):
    k = np.log((cap - .04) / (.2 * cap)) / t80[q]
    return cap - (cap - .04) * np.exp(-k * np.maximum(0, now - born))

def escape_rate(size):
    juvenile = .22 + (.08 - .22) * np.clip((size - .02) / .33, 0, 1)
    main = .985 * (1 - (1 - np.clip(size / 40, 0, 1)) ** 1.8)
    return np.where(size <= .35, juvenile, main)

def success_intensity(q, size, cap):
    # 在同一候选池内，按品质抽取的分母对所有鱼相同，条件成功分布可约去。
    return weights[q] * base[q] * .05 * (.35 + .65 * np.clip(size / cap, 0, 1)) * quality_fit[q] * (1 - escape_rate(size))

def table(head, rows):
    return '| ' + ' | '.join(head) + ' |\n| ' + ' | '.join(['---'] * len(head)) + ' |\n' + '\n'.join('| ' + ' | '.join(map(str, r)) + ' |' for r in rows)

def snapshot(q, size, alive, pond_replicates=N):
    qq, ss = q[alive], size[alive]
    return dict(quality_counts=(np.bincount(qq, minlength=7) / pond_replicates).tolist(),
                length_bin_counts=(np.histogram(ss, bin_edges)[0] / pond_replicates).tolist(),
                count=float(alive.sum() / pond_replicates))

def initial_state(rng):
    q = rng.choice(7, (N, 100), p=prob)
    cap = caps_base[q] * rng.triangular(.85, 1, 1.15, (N, 100))
    k = np.log((cap - .04) / (.2 * cap)) / t80[q]
    upper = cap - (cap - .04) * np.exp(-14 * k)
    size = .04 + rng.random((N, 100)) * (upper - .04)
    age = -np.log((cap - size) / (cap - .04)) / k
    born = -age
    alive = np.ones((N, 100), dtype=bool)
    return q, cap, born, alive, size

def deficit_restock(q, alive):
    """逐条复现现有 calcSupplementQualityWeights；仅比较日结品质。"""
    rng = np.random.default_rng(SEED + 101)
    counts = np.stack([np.sum(alive & (q == i), axis=1) for i in range(7)], axis=1)
    before = counts.copy()
    holes = 100 - counts.sum(axis=1)
    for step in range(int(holes.max())):
        rows = np.flatnonzero(holes > step)
        c = counts[rows]
        deficit = np.maximum(0, weights - c)
        w = deficit + weights * .1
        w[:, :2] *= np.where(deficit[:, :2] > 0, 2.25, 1)
        share = c[:, 2] / c.sum(axis=1)
        w[:, 2] *= np.where((c[:, 2] > 0) & (share >= prob[2] * .6), .15, 1)
        w[:, 3:] *= np.where(c[:, 3:] > 0, .02, 1)
        threshold = rng.random(len(rows)) * w.sum(axis=1)
        picked = np.sum(np.cumsum(w, axis=1) < threshold[:, None], axis=1)
        counts[rows, picked] += 1
    assert np.all(counts.sum(axis=1) == 100)
    return dict(added_quality_counts=((counts-before).sum(axis=0)/N).tolist(),
                after_quality_counts=(counts.sum(axis=0)/N).tolist())

def run_raw_day():
    """原始基础概率的等待态参考；半数塘2人、半数3人，均钓8小时。"""
    rng = np.random.default_rng(SEED)
    q, cap, born, alive, size = initial_state(rng)
    initial = snapshot(q, size, alive)
    seats = np.where(np.arange(N) % 2 == 0, 2, 3)
    taken_q, taken_size = [], []
    for minute in range(8 * 60):
        now = (8 + minute / 60) / 24
        for seat in range(3):
            rows = np.flatnonzero(seat < seats)
            picked = np.zeros(len(rows), dtype=int)
            pending = np.arange(len(rows))
            # 均匀提出候选，再按品质权重接受，等价于按现行权重选单条鱼。
            while len(pending):
                proposal = rng.integers(0, 100, len(pending))
                rr = rows[pending]
                accepted = alive[rr, proposal] & (rng.random(len(pending)) < weights[q[rr, proposal]] / weights.max())
                picked[pending[accepted]] = proposal[accepted]
                pending = pending[~accepted]
            cq, cc, cb = q[rows, picked], cap[rows, picked], born[rows, picked]
            cs = growth(cq, cc, cb, now)
            p_bite = 2.5 * base[cq] * .05 * (.35 + .65 * np.clip(cs / cc, 0, 1)) * quality_fit[cq]
            hooked = rng.random(len(rows)) < p_bite
            successful = hooked & (rng.random(len(rows)) >= escape_rate(cs))
            if np.any(successful):
                taken_q.append(cq[successful].copy())
                taken_size.append(cs[successful].copy())
                alive[rows[successful], picked[successful]] = False
    all_q = np.concatenate(taken_q)
    all_size = np.concatenate(taken_size)
    caught = snapshot(all_q, all_size, np.ones(len(all_q), dtype=bool))
    size = growth(q, cap, born, 1)
    before = snapshot(q, size, alive)
    deficit_comparison = deficit_restock(q, alive)
    replacement_mask = ~alive
    count = int(replacement_mask.sum())
    new_q = rng.choice(7, count, p=prob)
    new_cap = caps_base[new_q] * rng.triangular(.85, 1, 1.15, count)
    q[replacement_mask], cap[replacement_mask], born[replacement_mask] = new_q, new_cap, 1
    alive[:] = True
    after = snapshot(q, growth(q, cap, born, 1), alive)
    added = snapshot(new_q, np.full(count, .04), np.ones(count, dtype=bool))
    assert np.allclose(np.array(before['quality_counts']) + added['quality_counts'], after['quality_counts'])
    return dict(initial=initial, caught=caught, before_restock=before, added=added, after_restock=after,
                current_deficit_restock=deficit_comparison,
                player_hours_per_pond=20, always_waiting=True, hook_occupation_omitted=True,
                note='2或3位玩家08:00–16:00每分钟判定，钓点倍率2.5；忽略空间、收杆、睡眠、耐久；不是完整服务器预测')

def run(policy):
    rng = np.random.default_rng(SEED)
    q, cap, born, alive, size = initial_state(rng)
    initial = snapshot(q, size, alive)
    # 每分钟一次、钓点乘数取均值2.5，保持等待时的初始平均成功率近似。
    p_success = 2.5 * np.sum(success_intensity(q, size, cap), axis=1) / np.sum(weights[q], axis=1)
    always_waiting_success_per_hour = float(np.mean(p_success) * 60)
    row_idx = np.arange(N)
    daily = []
    day_one_catch = None
    for day in range(DAYS):
        taken_q, taken_size, taken_age, taken_ratio = [], [], [], []
        for event in range(TAKE):
            now = day + (event + .5) / TAKE
            size = growth(q, cap, born, now)
            intensity = np.where(alive, success_intensity(q, size, cap), 0)
            total = intensity.sum(axis=1)
            assert np.all(total > 0)
            threshold = rng.random(N) * total
            picked = np.sum(np.cumsum(intensity, axis=1) < threshold[:, None], axis=1)
            assert np.all(alive[row_idx, picked])
            taken_q.append(q[row_idx, picked].copy())
            taken_size.append(size[row_idx, picked].copy())
            taken_age.append((now - born[row_idx, picked]).copy())
            taken_ratio.append((size[row_idx, picked] / cap[row_idx, picked]).copy())
            alive[row_idx, picked] = False
        now = day + 1
        size = growth(q, cap, born, now)
        before = snapshot(q, size, alive)
        deficit_comparison = deficit_restock(q, alive) if day == 0 else None
        removed_q = np.concatenate(taken_q)
        removed_size = np.concatenate(taken_size)
        caught = snapshot(removed_q, removed_size, np.ones(len(removed_q), dtype=bool))
        replacement_mask = ~alive
        count = int(replacement_mask.sum())
        assert count == N * TAKE
        new_q = rng.choice(7, count, p=prob)
        new_cap = caps_base[new_q] * rng.triangular(.85, 1, 1.15, count)
        # 固定抽取随机数数量，令两个方案的首日捕获和补鱼品质样本完全一致。
        source_u = rng.random(count)
        age_u = rng.random(count)
        if policy == 'newborn':
            new_age = np.zeros(count)
        else:
            fraction = np.where(source_u < .7, age_u * .2,
                        np.where(source_u < .95, .2 + age_u * .6, .8 + age_u * .7))
            new_age = fraction * t80[new_q]
        q[replacement_mask] = new_q
        cap[replacement_mask] = new_cap
        born[replacement_mask] = now - new_age
        alive[:] = True
        size = growth(q, cap, born, now)
        after = snapshot(q, size, alive)
        added = snapshot(new_q, size[replacement_mask], np.ones(count, dtype=bool))
        assert abs(before['count'] - 80) < 1e-10 and abs(after['count'] - 100) < 1e-10
        assert np.allclose(np.array(before['quality_counts']) + added['quality_counts'], after['quality_counts'])
        daily.append(dict(day=now, caught=caught, before_restock=before, added=added, after_restock=after,
                          current_deficit_restock=deficit_comparison))
        if day == 0:
            day_one_catch = dict(age_quantiles_days=np.quantile(np.concatenate(taken_age), [.1, .5, .9]).tolist(),
                                 size_quantiles_m=np.quantile(removed_size, [.1, .5, .9]).tolist(),
                                 relative_size_quantiles=np.quantile(np.concatenate(taken_ratio), [.1, .5, .9]).tolist(),
                                 by_quality={names[i]:dict(mean_length_m=float(removed_size[removed_q == i].mean()) if np.any(removed_q == i) else None,
                                                          sample_count=int(np.sum(removed_q == i))) for i in range(7)})
    return dict(policy=policy, initial=initial, always_waiting_initial_success_per_hour=always_waiting_success_per_hour,
                day_one_catch=day_one_catch, daily=daily)

results = {p:run(p) for p in ['newborn', 'mixed']}
raw_day = run_raw_day()
a, b = results['newborn'], results['mixed']
assert a['daily'][0]['caught'] == b['daily'][0]['caught']
assert a['daily'][0]['added']['quality_counts'] == b['daily'][0]['added']['quality_counts']
result = dict(seed=SEED, replicated_ponds=N, days=DAYS, assumed_successful_take_per_pond_day=TAKE,
              quality_names=names, length_bins=bin_names, results=results, raw_probability_reference=raw_day,
              assumptions=dict(initial='100 fish; advanced category probabilities 38/28/18/9/4/2/1; length-uniform initialization, age 0–14d',
                               growth='new proposal T80 1/2/4/5/7/10/14 days; birth 4cm; individual cap factor triangular .85/1/1.15',
                               probability='current quality selection weights, bite base, size bite shape, size escape; bamboo quality fit; catch-group fit 1',
                               spatial='one well-mixed candidate pool; no spot isolation or migration simulation',
                               time='20 successful catches evenly spaced across 24h; no session waiting or hook occupation simulation',
                               replenishment='daily fill 100; stock never drops to 40 in this 20-catch scenario',
                               omitted='runtime overrides, species/rod group differences, rod damage, paid bait, groundbait, escapes as explicit events, sleep effects, nutrition, return fish'))
(OUT / 'harvest-results.json').write_text(json.dumps(result, ensure_ascii=False, indent=2))

d = a['daily'][0]
initial_q = a['initial']['quality_counts']
qc = d['caught']['quality_counts']
def count_text(value, observed=False):
    if observed and value == 0:
        return '本次未观察到'
    return f'{value:.4f}' if 0 < value < .01 else f'{value:.2f}'

quality_table = table(['品质', '开塘', '一天带走', '补鱼前', '沿用缺口算法补完', '按开塘比例补完'],
                      [[names[i], f'{initial_q[i]:.2f}', count_text(qc[i], True),
                        *[f'{v[i]:.2f}' for v in [d['before_restock']['quality_counts'],d['current_deficit_restock']['after_quality_counts'],d['after_restock']['quality_counts']]]] for i in range(7)])
raw_table = table(['品质', '开塘', '原始概率参考带走', '补鱼前', '日结补完'],
                  [[names[i], f'{initial_q[i]:.2f}', count_text(raw_day['caught']['quality_counts'][i], True),
                    f"{raw_day['before_restock']['quality_counts'][i]:.2f}", f"{raw_day['current_deficit_restock']['after_quality_counts'][i]:.2f}"] for i in range(7)])
size_table = table(['体长', '一天带走', '补鱼前存量', '全新幼鱼补完', '混龄方案补完'],
                   [[bin_names[i], *[f'{v[i]:.2f}' for v in [d['caught']['length_bin_counts'],d['before_restock']['length_bin_counts'],d['after_restock']['length_bin_counts'],b['daily'][0]['after_restock']['length_bin_counts']]]] for i in range(len(bin_names))])
long_table = table(['品质', '开塘', '第1天补完', '第7天补完', '第14天补完', '第30天补完'],
                   [[names[i], f'{initial_q[i]:.2f}', *[f"{a['daily'][day-1]['after_restock']['quality_counts'][i]:.2f}" for day in [1,7,14,30]]] for i in range(7)])
mean_success = a['always_waiting_initial_success_per_hour']
report = f'''# 一天捕捞与补鱼后的分布推演

2026-11-03 · 新成长方案与现行概率结构的条件推演，未修改业务代码。

## 计算口径

以七品质齐全的参考高级塘为例：初始100条，品质按38/28/18/9/4/2/1随机生成。采用新成长时间1、2、4、5、7、10、14天；出生4cm，上限围绕品质基准按候选三角分布±15%波动。初始体长均匀铺开，再反推出前14～0天内的出生时间。

按每线100名日参与用户、每人目标8条、40个塘平均分配，给定每塘一天成功带走20条。模拟4000个独立参考塘，固定种子2026110302，表中为平均条数；实际单塘是整数并有较大随机波动。每条鱼真实独立，成功带走后不再参加当天后续抽取。

采用现行的按品质抽鱼权重、品质咬钩基数、尺寸咬钩系数、尺寸脱钩曲线，并使用竹竿的品质适配。基础饵，无打窝、无额外营养。为适配新个体上限，尺寸归一化采用该鱼自己的上限；鱼种适配统一为1。没有模拟20个钓位的局部隔离、收杆占用、睡眠强制脱钩、竿损坏、回鱼、线上配置覆盖或脱钩后的增长。因此这是捕获20条的条件分布，不是现有服务器一天必然产出20条的预测，也不是线上实测。

在同一候选池内，某条鱼成功被带走的相对倾向为：

```
该鱼品质抽取权重 × 品质咬钩基数 × 尺寸咬钩系数 × 鱼竿品质适配 × (1−脱钩率)
```

同一池的公共选鱼分母与公共倍率可在成功条件分布中约去。模拟中的20次成功捕获均匀铺在24小时内，每次重新计算所有存活鱼的体长与相对倾向。

## 原始基础概率下的参考日

另做一次未强制捕获数的参考：半数塘安排2位玩家，半数3位，均在08:00～16:00连续8小时每分钟判定一次，即每塘平均20个玩家小时。按原始品质选择、咬钩与脱钩概率抽取结果，钓点倍率取均值2.5。

该参考日平均带走{raw_day['caught']['count']:.2f}条/塘，剩{raw_day['before_restock']['count']:.2f}条，日结平均补{raw_day['added']['count']:.2f}条回到100。如果40个塘都采用同一参考配置，折算全线约{raw_day['caught']['count']*40:.0f}条；实际不同类别塘与装备不适用这个直接倍乘。

{raw_table}

这仍是“始终处于可判定等待态”的参考：没有扣除收杆占用，也没有钓位隔离、睡眠、耐久和线上覆盖配置。它用于展示基础饵概率与目标的差距，不能当作实服精确产量。下文则明确将成功数给定为20条，查看达到目标以后生态怎么变化。

## 目标情景：首日成功带走20条的品质变化

{quality_table}

数量关系是：补完后的该品质条数＝开塘条数−当天带走条数＋当日补入条数。主比较沿用现有缺口补鱼算法，逐条重算品质权重：普通、优良有缺口时加权，蓝色相对充足时压低，紫色及以上仍有存量时乘0.02。它偏向恢复低品质，不是精确凑齐每种品质的开塘条数。另列“完全按开塘品质比例抽补鱼”作为对照；这不是现有补鱼算法。两者都采用你最新的日结补足100条规则。

当前权重使普通、优良在成功捕获中占比明显高于它们的存量占比。现有缺口算法会优先把它们补回来，因此比按开塘比例随机补更能维持品质结构。若改成只按开塘比例补鱼，被带走少的高品质会逐渐积累；开塘品质概率不会自动成为每日结算后的固定存量比例。

首日4000塘合计80000条成功捕获样本中，神话观察到3条，至尊未观察到。未观察到或表格四舍五入显示0.00都不代表数学概率为零，极稀有品质的频率仍需要更大样本和完整会话模型验证。

## 首日体长变化

{size_table}

“全新幼鱼补完”将补入的20条全部设为4cm、鱼龄0天。“混龄方案补完”采用尚未确认的70%幼鱼、25%成长鱼、5%较成熟鱼，鱼龄分别为该品质T80的0～20%、20～80%、80～150%；迁入时间都是当日结算时刻，出生时间根据鱼龄确定，不编造此前经历。

本场景每天仅带走20条，补鱼前剩80条，全天不触发“≤40补到80”的低水位规则；日结补20条回到100。补鱼后的幼鱼增加属于新个体加入，原有存活鱼没有缩小。不同补鱼来源会改变体长分布；首日补入鱼的品质分布一致。

## 连续多日的品质变化

下表只演示对照方案：每日固定成功捕获20条、按开塘比例补鱼、全部补新生幼鱼。它不使用现有缺口补鱼权重，不能当作沿用现有算法的长期预测：

{long_table}

后面的20条仍是外部给定目标。随着低品质减少，真实未调整概率下凑齐20条所需等待时间可能增加，不能据本表承诺未来每天仍能稳定带走20条。

## 原始概率与每小时1条目标的差距

使用均值钓点倍率2.5、每分钟判定一次，并假设玩家一直处于可判定的等待阶段，开塘时近似成功强度为每人每小时{mean_success:.3f}条，8小时约{mean_success*8:.2f}条。该数值仍未扣除收杆占用、空钓位等影响，也未加入付费饵的加成。运行中的配置和装备组合可能明显改变结果。

因此“每人8小时8条、全线800条”在本报告里是要达到的目标情景，原始基础饵概率尚不能据此视为已经达到。鱼群存量分布、成功钓获分布、补鱼分布需要分别看。

![品质、体长与长期存量变化](harvest.png)

原始数据：harvest-results.json。复现：simulate_harvest.py，需要numpy和matplotlib。所有数值均为离线推演；未执行真实捕获、补鱼或数据库写入。
'''
(OUT / 'harvest-report.md').write_text(report)

font=FontProperties(fname='/System/Library/Fonts/Hiragino Sans GB.ttc')
plt.rcParams.update({'font.family':font.get_name(),'axes.unicode_minus':False,'font.size':10,'axes.spines.top':False,'axes.spines.right':False})
fig,axs=plt.subplots(2,2,figsize=(14,10),layout='constrained')
qcolors=['#818994','#409d71','#4b83bf','#9c6db6','#cf6664','#df953c','#bd9a28']
x=np.arange(7)
ax=axs[0,0]
ax.bar(x-.2,np.array(initial_q),width=.4,color='#b7c9c1',label='开塘存量（100条）')
ax.bar(x+.2,np.array(qc)*5,width=.4,color='#367c74',label='成功带走的构成（20条折算为%）')
ax.set(title='普通、优良在钓获中更集中',xticks=x,xticklabels=names,ylabel='占比（%）')
ax.legend(fontsize=9)
ax=axs[0,1]
for offset,v,label,color in [(-.3,initial_q,'开塘','#b7c9c1'),(-.1,d['before_restock']['quality_counts'],'带走20条后','#dbb683'),(.1,d['current_deficit_restock']['after_quality_counts'],'现有缺口算法补完','#367c74'),(.3,d['after_restock']['quality_counts'],'按开塘比例补完','#8e9dc4')]:
    ax.bar(x+offset,v,width=.2,label=label,color=color)
ax.set(title='缺口补鱼更接近开塘的品质结构',xticks=x,xticklabels=names,ylabel='每塘平均条数')
ax.legend(fontsize=8,ncol=2)
ax=axs[1,0]
xx=np.arange(len(bin_names))
for offset,v,label,color in [(-.25,d['before_restock']['length_bin_counts'],'补鱼前','#dbb683'),(0,d['after_restock']['length_bin_counts'],'补新生幼鱼','#b7c9c1'),(.25,b['daily'][0]['after_restock']['length_bin_counts'],'候选混龄补鱼','#367c74')]:
    ax.bar(xx+offset,v,width=.25,label=label,color=color)
ax.set(title='补鱼来源决定日结后小鱼增加多少',xticks=xx,xticklabels=bin_names,ylabel='每塘平均条数')
ax.tick_params(axis='x',labelrotation=20)
ax.legend(fontsize=9)
ax=axs[1,1]
for i,name in enumerate(names):
    vals=[initial_q[i]]+[r['after_restock']['quality_counts'][i] for r in a['daily']]
    ax.plot(np.arange(DAYS+1),vals,label=name,color=qcolors[i],lw=2)
ax.set(title='对照：按开塘比例补鱼，高品质逐渐积累',xlabel='天数',ylabel='日结后每塘平均条数')
ax.legend(ncol=4,fontsize=9)
fig.suptitle('参考高级塘 · 一天带走20条并补回100条',fontsize=19,fontweight='bold')
fig.savefig(OUT/'harvest.png',dpi=160)
fig.savefig(OUT/'harvest.svg')
plt.close(fig)
print(json.dumps(dict(replicated_ponds=N, mean_initial_success_per_hour=mean_success, raw_day=raw_day, day1=d, mixed_day1=b['daily'][0], day7=a['daily'][6], day30=a['daily'][-1]),ensure_ascii=False,indent=2))
