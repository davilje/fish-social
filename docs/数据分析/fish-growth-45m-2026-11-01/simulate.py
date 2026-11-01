"""45m growth design simulation, not production code. Requires numpy and matplotlib.
Run: python simulate.py --samples 100000 --seed 451101
"""
import argparse
import json
from pathlib import Path
import numpy as np
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
from matplotlib.font_manager import FontProperties

parser = argparse.ArgumentParser()
parser.add_argument('--samples', type=int, default=100000)
parser.add_argument('--seed', type=int, default=451101)
args = parser.parse_args()
if args.samples < 1000:
    parser.error('at least 1000 samples are required')
out = Path(__file__).resolve().parent
rng = np.random.default_rng(args.seed)
N = args.samples
CAP = 45.0
BIRTH = .1
QUALITY = ['普通', '优良', '稀有', '史诗', '传说', '神话', '至尊']
RATES = np.array([.7, .9, 1.15, 1.5, 1.9, 2.4, 3.0])
WEIGHTS = np.array([38, 28, 18, 9, 4, 2, 1]) / 100

def effort(x):
    return 2*x + .12*x*x + .00006*x**4

def growth(age, rate, birth=BIRTH):
    progress = np.asarray(age) * np.asarray(rate)
    lo, hi = np.zeros(np.shape(progress)), np.broadcast_to(CAP - np.asarray(birth), np.shape(progress)).copy()
    capped = progress >= effort(hi)
    for _ in range(42):
        mid = (lo+hi)/2
        smaller = effort(mid) < progress
        lo, hi = np.where(smaller, mid, lo), np.where(smaller, hi, mid)
    value = np.asarray(birth) + (lo+hi)/2
    return np.where(capped, CAP, np.round(value, 3))

# Rejection sampling preserves continuity instead of putting probability mass on speed bounds.
z = rng.normal(-.125, .5, N)
while True:
    invalid = (z < np.log(.4)) | (z > np.log(2.5))
    if not invalid.any():
        break
    z[invalid] = rng.normal(-.125, .5, invalid.sum())
speeds = np.exp(z)
quality = rng.choice(7, size=N, p=WEIGHTS)
thresholds = [1, 5, 10, 20, 30, 40, 45]
milestones = [dict(length_m=v, baseline_days=float(effort(v-BIRTH)),
                   gray_days=float(effort(v-BIRTH)/RATES[0]), gold_days=float(effort(v-BIRTH)/RATES[-1])) for v in thresholds]
cohorts = []
for day in [1, 7, 30, 90, 180, 365]:
    lengths = growth(day, speeds * RATES[0])
    mm = np.floor(lengths*1000+.5).astype(int)
    display_mm = np.where(mm<1000,mm,np.where(mm<9995,((mm+5)//10)*10,((mm+50)//100)*100))
    cohorts.append(dict(display_distinct=int(len(np.unique(display_mm))), age_days=day, p10_m=float(np.quantile(lengths,.1)), median_m=float(np.median(lengths)),
                        p90_m=float(np.quantile(lengths,.9)), distinct_mm=int(len(np.unique(lengths))),
                        capped_percent=float(np.mean(lengths == CAP)*100)))
scenarios = []
scenario_lengths = {}
for mean in [7, 30, 90]:
    # Assumed residence ages, NOT a harvest/bite/ecology simulation.
    ages = rng.exponential(mean, N)
    lengths = growth(ages, speeds * RATES[quality])
    scenario_lengths[mean] = lengths
    scenarios.append(dict(mean_age_days=mean, median_m=float(np.median(lengths)),
                          at_least_percent={str(t):float(np.mean(lengths>=t)*100) for t in thresholds},
                          capped_count=int(np.sum(lengths == CAP))))
rate = speeds * RATES[0]
for day in [0, 1, 30, 365, 100000]:
    values = growth(day, rate)
    assert np.all((values >= BIRTH) & (values <= CAP))
    assert np.all(growth(day+1, rate) >= values)
assert np.all(growth(0, rate) == BIRTH)
assert all(abs(growth(m['baseline_days'], 1.0)-m['length_m']) < .0011 for m in milestones)
summary = dict(status='design-only', samples=N, seed=args.seed, birth_m=BIRTH,
               effort_coefficients=[2,.12,.00006], quality_rates=dict(zip(QUALITY,RATES.tolist())),
               individual_speed_distribution='LogNormal(mu=-0.125,sigma=0.5), rejection-truncated to [0.4,2.5]',
               species_factor=1, quality_weights=WEIGHTS.tolist(),
               milestones=milestones, same_age_gray_cohorts=cohorts, assumed_age_scenarios=scenarios,
               notes=['Formula simulation only; no actual player data, no capture selection or economic simulation.',
                      'Age means 7/30/90 days are assumed scenario inputs; not estimated from existing ecology.',
                      'All samples start at 0.1m and use species factor 1, isolating growth and quality.',
                      'Zero observed extremes in 100000 samples do not prove zero probability.'])
(out/'simulation.json').write_text(json.dumps(summary, ensure_ascii=False, indent=2), encoding='utf-8')
font = Path('/System/Library/Fonts/STHeiti Light.ttc')
if font.exists():
    plt.rcParams['font.family'] = FontProperties(fname=str(font)).get_name()
plt.rcParams.update({'axes.unicode_minus':False, 'font.size':11,'axes.spines.top':False,'axes.spines.right':False,
                     'figure.facecolor':'#f7faf9','axes.facecolor':'#f7faf9'})
colors=['#4d7768','#8ba981','#be9a53','#88688b','#b56c60','#c8823a','#b6a143']
fig, axes = plt.subplots(2,2,figsize=(14,10),layout='constrained')
ax=axes[0,0]
days=np.linspace(0,850,500)
for i in [0,2,4,6]:
    ax.plot(days,growth(days,RATES[i]),label=QUALITY[i],color=colors[i],linewidth=2)
ax.axhline(45,color='#6d756f',linestyle=':',linewidth=1)
ax.set(xlabel='鱼龄（天）',ylabel='长度（米）',title='同一个 45 米终点，品质改变成长速度',ylim=(0,47),xlim=(0,850))
ax.legend(frameon=False,ncol=2,fontsize=10);ax.grid(axis='y',alpha=.15)
ax=axes[0,1]
days=np.linspace(0,30,200)
for speed,label,color in [(.5,'慢个体 ×0.5','#9caca1'),(1,'标准个体 ×1','#26766b'),(2,'快个体 ×2','#c8823a')]:
    ax.plot(days,growth(days,.7*speed),label=label,color=color,linewidth=2)
ax.axhline(.585,color='#88688b',linestyle='--',linewidth=1.5,label='上一版普通鲤鱼潜力上界')
ax.set(xlabel='鱼龄（天）',ylabel='长度（米）',title='普通鱼也能持续长大：前 30 天',xlim=(0,30),ylim=(0,14))
ax.legend(frameon=False,fontsize=9);ax.grid(axis='y',alpha=.15)
ax=axes[1,0]
lengths=growth(7,speeds*.7)
ax.hist(lengths,bins=np.arange(0,6.05,.1),color='#26766b',alpha=.85)
ax.set(xlabel='长度（米，按 10cm 分组）',ylabel='数量',title=f'同为普通品质、7 天龄：{N:,} 条仍有差异',xlim=(0,6))
ax.grid(axis='y',alpha=.15)
ax=axes[1,1]
bins=[0,1,5,10,20,30,40,45.001]
labels=['<1m','1–5m','5–10m','10–20m','20–30m','30–40m','40–45m']
bottom=np.zeros(3)
for i in range(7):
    vals=[np.mean((scenario_lengths[d]>=bins[i])&(scenario_lengths[d]<bins[i+1]))*100 for d in [7,30,90]]
    ax.bar(range(3),vals,bottom=bottom,label=labels[i],color=['#d8e4d6','#a8c3ae','#6a9c87','#3d7c71','#baae72','#c68655','#8a5e65'][i])
    bottom+=vals
ax.set(xticks=range(3),xticklabels=['平均鱼龄 7 天','平均鱼龄 30 天','平均鱼龄 90 天'],ylabel='存量占比（%）',title='巨物数量还取决于鱼在塘里留多久',ylim=(0,100))
ax.legend(frameon=False,ncol=4,loc='upper center',bbox_to_anchor=(.5,-.14),fontsize=9)
fig.suptitle('45 米巨物成长方案 · 公式与情景模拟',fontsize=21)
fig.supxlabel('设计推演，尚未接入业务。出生统一 10cm、鱼种系数为 1；年龄分布为假设输入，不能代表玩家钓获概率。',fontsize=10,color='#53635d')
fig.savefig(out/'growth-design.png',dpi=150)
fig.savefig(out/'growth-design.svg')
print(json.dumps(summary, ensure_ascii=False, indent=2))
