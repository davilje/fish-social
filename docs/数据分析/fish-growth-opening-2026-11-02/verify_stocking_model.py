"""入塘口径最终模型的离线校验与图表；不连接数据库、不改变游戏参数。"""
import json
from pathlib import Path
import numpy as np
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
from matplotlib.font_manager import FontProperties

OUT = Path(__file__).resolve().parent
NAMES = ['普通', '优良', '稀有', '史诗', '传说', '神话', '至尊']
CAPS = [.2, .35, .5, .8, 3, 15, 40]
T80 = [1, 2, 3, 5, 7, 10, 14]
COLORS = ['#7d878e', '#459368', '#4487b4', '#9b74af', '#cd6c67', '#dd9338', '#aa9229']
FONT = FontProperties(fname='/System/Library/Fonts/Hiragino Sans GB.ttc')
plt.rcParams.update({'axes.spines.top': False, 'axes.spines.right': False, 'axes.grid': True, 'grid.alpha': .18})
RNG = np.random.default_rng(20261103)

def rate(s, c, t):
    return np.log((c-s)/(.2*c))/t

def length(days, s, c, t, bonus=0, entry=None):
    e = s if entry is None else entry
    return c-(c-e)*np.exp(-rate(s,c,t)*(np.asarray(days)+bonus))

def fmt(m):
    # 与正式显示策略保持单位区间；先按毫米量化，避免字符串反馈进成长。
    m = np.floor(np.asarray(m)*1000+.5)/1000
    return f'{float(m)*100:.1f}厘米' if m < 1 else f'{float(m):.2f}米' if m < 10 else f'{float(m):.1f}米'

def table(heads, rows):
    return '| '+' | '.join(heads)+' |\n| '+' | '.join(['---']*len(heads))+' |\n'+'\n'.join('| '+' | '.join(map(str,r))+' |' for r in rows)

fig, axes = plt.subplots(2, 2, figsize=(14, 10), layout='constrained')
days = np.linspace(0, 28, 400)
records, rows, opening_rows = [], [], []
for name, c, t, color in zip(NAMES, CAPS, T80, COLORS):
    s = np.floor(.04*RNG.triangular(.85,1,1.15,100000)*1000+.5)/1000
    cap = np.floor(c*RNG.triangular(.85,1,1.15,100000)*1000+.5)/1000
    k = rate(s,cap,t)
    l0 = s+RNG.random(len(s))*(length(14,s,cap,t)-s)
    residence = -np.log((cap-l0)/(cap-s))/k
    assert np.allclose(length(0,s,cap,t),s)
    assert np.allclose(length(t,s,cap,t),.8*cap)
    assert np.allclose(length(residence,s,cap,t),l0)
    assert np.all((residence>=0)&(residence<=14))
    assert np.all((s>=.034)&(s<=.046))
    values = length(days,.04,c,t)
    # 浮点数或毫米量化可能提前显示为上限，但不能超过上限。
    assert np.all(np.diff(values)>=0) and np.all(values<=c)
    # 大规格入塘鱼的延续：入塘即为E，生存时钟从0开始，k不重抽。
    e=length(.7*t,s,cap,t)
    assert np.allclose(length(0,s,cap,t,entry=e),e)
    assert np.allclose(length(1,s,cap,t,entry=e),length(.7*t+1,s,cap,t))
    assert np.all(length(residence,s,cap,t,bonus=.25)>=l0)
    axes[0,0].plot(days,values/c*100,color=color,label=name)
    axes[0,0].scatter([t],[80],color=color,s=24)
    mean_retained=[float(np.mean(cap-(cap-l0)*np.exp(-k*d))) for d in [0,1,3,7,14]]
    axes[1,1].plot([0,1,3,7,14],np.array(mean_retained)/c*100,color=color,label=name)
    quantiles=np.quantile(l0,[.1,.5,.9]).tolist()
    qage=np.quantile(residence,[.1,.5,.9]).tolist()
    rows.append([name,f'{t}天',*[fmt(length(d,.04,c,t)) for d in [0,1,3,7,14]]])
    opening_rows.append([name,*[fmt(x) for x in quantiles],f'{qage[1]:.2f}天',*[fmt(x) for x in mean_retained[1:]]])
    records.append(dict(quality=name,cap_base_m=c,t80_days=t,initial_length_p10_p50_p90_m=quantiles,residence_p10_p50_p90_days=qage,retained_mean_m=mean_retained))

axes[0,0].axhline(80,ls='--',lw=.8,color='#7d878e')
axes[0,0].legend(prop=FONT,ncol=4,fontsize=9,loc='lower right')
axes[0,0].set(ylim=(0,103))
axes[0,0].set_xlabel('幼鱼入塘后的天数',fontproperties=FONT)
axes[0,0].set_ylabel('占自身上限的比例（%）',fontproperties=FONT)
axes[0,0].set_title('自然成长：不同品质的T80',fontproperties=FONT)
sample=np.floor(.04*RNG.triangular(.85,1,1.15,100000)*1000+.5)/1000
axes[0,1].hist(sample*100,bins=np.arange(3.35,4.76,.1),color='#66a792',edgecolor='white')
axes[0,1].set(xlim=(3.3,4.7))
axes[0,1].set_xlabel('幼鱼基准投放长度（厘米）',fontproperties=FONT)
axes[0,1].set_ylabel('10万统计样本中的数量',fontproperties=FONT)
axes[0,1].set_title('幼鱼投放规格：4厘米±15%',fontproperties=FONT)
for s,c,label,color in [(.036,.18,'个体A：3.6厘米入塘，上限18厘米','#4487b4'),(.044,.22,'个体B：4.4厘米入塘，上限22厘米','#d08b54')]:
    d=np.linspace(0,4,200)
    axes[1,0].plot(d,length(d,s,c,1)*100,color=color,label=label)
axes[1,0].legend(prop=FONT,loc='lower right')
axes[1,0].set_xlabel('入塘后的天数',fontproperties=FONT)
axes[1,0].set_ylabel('体长（厘米）',fontproperties=FONT)
axes[1,0].set_title('同品质普通鱼：入塘尺寸与上限各自固定',fontproperties=FONT)
axes[1,1].set(ylim=(0,105))
axes[1,1].set_xlabel('开服后的天数',fontproperties=FONT)
axes[1,1].set_ylabel('平均体长／品质基准上限（%）',fontproperties=FONT)
axes[1,1].set_title('开服原始鱼群存活时的均长变化',fontproperties=FONT)
fig.suptitle('入塘时间 × 个体尺寸 × 渐近成长',fontproperties=FONT,fontsize=20)
fig.savefig(OUT/'stocking-growth.png',dpi=160)
fig.savefig(OUT/'stocking-growth.svg')
plt.close(fig)

result=dict(version='stocking-entry-20261103-v1',seed=20261103,samples_per_quality=100000,juvenile_entry_m=[.034,.04,.046],initial_residence_days=[0,14],records=records,assertions='L(0)=S; L(T80)=0.8C; opening inverse; range; monotonic; cap; mature entry continuity; nutrition separate',scope='Natural growth and opening only. No capture, replenishment, nutrition event simulation or database operations.')
(OUT/'stocking-results.json').write_text(json.dumps(result,ensure_ascii=False,indent=2))
report='''# 入塘口径修订与自然成长验证

2026-11-03 · 离线模型，未修改业务代码或数据库。

**4厘米改为投放幼鱼规格，每条固定3.4～4.6厘米；履历记录入塘时间和在塘生存时间，不再推算真实出生日期。** 开服前14～0天改为历史入塘批次，开服当天已呈现不同体长。规格和上限分别独立采用中心多、两端少的三角分布。

## 参考单鱼：从4厘米入塘

以下上限取各品质基准，仅自然成长，不含营养。正式鱼的投放规格和上限均有±15%固定差异。

'''+table(['品质','自然T80','入塘','1天','3天','7天','14天'],rows)+'''

![入塘规格、自然曲线、同品质个体与初始鱼群对比](stocking-growth.png)

## 开服初始分布与后续存活

每品质抽取100,000条统计样本，固定种子20261103；并非向实际鱼塘投放70万条鱼。S与C先生成并按0.001米保存，再在S至L14之间均匀抽长度，反解历史入塘时间。各塘初始品质仍用原权重，表中为同品质跨大量鱼塘的统计，不保证每塘都有所有品质。

'''+table(['品质','开服P10','开服P50','开服P90','在塘时长P50','存活+1天均长','+3天','+7天','+14天'],opening_rows)+'''

“存活+N天”只追踪原始鱼，没有捕获、补鱼、摄食或脱钩。长期无人捕捞，鱼仍会趋近各自上限；不能用这张图证明补鱼后形成稳定态。此前捕捞推演使用旧参数，仍保留为历史参考，尚未按本次入塘规格重跑。

## 时间与长度怎样解释

- 初始鱼：实际记录的历史入塘时长为0～14天；所有初始鱼当时都以各自3.4～4.6厘米规格入塘。
- 后续补入鱼：已明确确认50%幼鱼、40%成长鱼、10%较成熟鱼的混合规格，幼鱼以3.4～4.6厘米为基准、各阶段按曲线生成实际入塘长度；所有新补入鱼从实际投放时刻累计在本塘时长，不得假称早已在本塘生活。
- 摄食：另存额外成长分钟，能提前变大；不会让“在塘2天”变成“在塘3天”。
- 钓获：冻结入塘时间、在塘时长、入塘长度和本次尺寸等履历快照。入塘后长大量＝钓获长度−实际入塘长度。

## 校验范围

七品质合计700,000样本校验了幼鱼范围、自然T80、初始化反解0～14天、单调渐近、成长规格接入连续性与营养时间分离。通过的仅为离线数学模型，不代表现有服务器已经切换。每日04:00、补鱼事务、滚动营养额度、玩家历史保留与实际捕获产量需在开发阶段验证。

数据：[stocking-results.json](stocking-results.json)；脚本：[verify_stocking_model.py](verify_stocking_model.py)。
'''
(OUT/'入塘口径修订.md').write_text(report)
print(json.dumps({'verified_samples':700000,'report':'入塘口径修订.md','chart':'stocking-growth.png'},ensure_ascii=False))
