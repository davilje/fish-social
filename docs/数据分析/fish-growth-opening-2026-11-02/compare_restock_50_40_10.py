"""50/40/10补鱼结构：与前次全新生、70/25/5方案同口径比较。"""
import json
import numpy as np
from simulate_full_pond import OUT, N, SEED, names, bins, t80, run, table, plt, FontProperties

policies = ['newborn', 'mixed', 'mixed_50_40_10']
labels = ['全补4cm新生鱼', '70%／25%／5%', '50%／40%／10%']
results = {policy:run(policy) for policy in policies}
assert all(r['initial'] == results['newborn']['initial'] for r in results.values())
a = results['mixed_50_40_10']
before, after = a['hourly'][-1]['before'], a['hourly'][-1]['after']
for r in results.values():
    assert r['caught']['count'] == 480
    assert r['hourly'][-1]['after']['count'] == 100
    assert len(r['supplements']) == 12
    assert sum(r['hourly'][-1]['after']['quality_counts']) == 100
output = dict(seed=SEED,replicated_ponds=N,quality_names=names,length_bins=bins,results=results,
              assumptions=dict(reference='same seven-quality advanced pond, cap triangular(.85,1,1.15), birth 4cm, T80 1/2/4/5/7/10/14d',
                               successes='20/hour for 24h, externally fixed; relative probability unchanged',
                               refill='<=40 refill 80 hourly, day-end refill 100 takes priority, existing quality-deficit weights relative to 100',
                               stages='independent mixture per incoming fish; age uniform 0-.2*T80 / .2-.8*T80 / .8-1.5*T80; birth != entry',
                               excludes='spot isolation, migration, hook occupation, nutrition, explicit escape growth, tackle failure, species group fit and +/-10% individual biases'))
(OUT/'restock-50-40-10-results.json').write_text(json.dumps(output,ensure_ascii=False,indent=2))

def metric_rows():
    return [
        ['钓获体长中位数（cm）',*[f"{results[p]['caught']['length_quantiles_cm'][1]:.1f}" for p in policies]],
        ['钓获体长P90（cm）',*[f"{results[p]['caught']['length_quantiles_cm'][2]:.1f}" for p in policies]],
        ['钓获中不满10cm占比',*[f"{results[p]['caught']['length_bin_counts'][0]/480*100:.1f}%" for p in policies]],
        ['钓获中已达到自身上限80%的占比',*[f"{results[p]['caught']['over_80pct_cap_pct']:.1f}%" for p in policies]],
        ['日结后不满10cm数量',*[f"{results[p]['hourly'][-1]['after']['length_bin_counts'][0]:.1f}" for p in policies]],
        ['日结后体长中位数（cm）',*[f"{results[p]['hourly'][-1]['after']['size_median_cm']:.1f}" for p in policies]],
        ['日结后留下的开塘鱼',*[f"{results[p]['hourly'][-1]['after']['original_survivors']:.1f}" for p in policies]],
        ['被钓走补入鱼的入塘时长中位数（小时）',*[f"{results[p]['caught']['new_fish_residence_quantiles_hours'][1]:.2f}" for p in policies]],
    ]
summary_table = table(['指标',*labels],metric_rows())
stage_table = table(['品质','达到80%的时间','幼鱼的入塘鱼龄','成长鱼的入塘鱼龄','较成熟鱼的入塘鱼龄'],
                    [[name,f'{t80[i]:g}天',f'0～{t80[i]*.2:g}天',f'{t80[i]*.2:g}～{t80[i]*.8:g}天',f'{t80[i]*.8:g}～{t80[i]*1.5:g}天'] for i,name in enumerate(names)])
quality_table = table(['品质','开塘100条','全天钓走480条','日结前60条','日结补完100条'],
                      [[name,*[f'{v[i]:.2f}' if v[i]>=.01 else f'{v[i]:.4f}' for v in [a['initial']['quality_counts'],a['caught']['quality_counts'],before['quality_counts'],after['quality_counts']]]] for i,name in enumerate(names)])
size_table = table(['体长','全天钓走480条','钓获占比','日结前60条','日结补完100条'],
                   [[name,f"{a['caught']['length_bin_counts'][i]:.2f}",f"{a['caught']['length_bin_counts'][i]/480*100:.2f}%",f"{before['length_bin_counts'][i]:.2f}",f"{after['length_bin_counts'][i]:.2f}"] for i,name in enumerate(bins)])
c = a['caught']
report = f'''# 补鱼50%幼鱼、40%成长鱼、10%较成熟鱼：满塘全天推演

2026-11-03 · 4000个独立参考塘，仍按每小时已经成功带走20条、一天480条计算。只更新离线报告和预览，未修改游戏算法。

## 本次只调整补入鱼的年龄结构

开塘100条、七品质高级塘原始比例、品质上限基准与候选个体波动、自然成长速度、相对钓获概率、品质缺口补鱼权重，都与上一轮相同。每小时检查≤40补到80，日结补到100；途中11次各补40，日结再补40，全天补入480条。

本次每条补入鱼先按原有品质缺口规则确定品质，再独立以50%／40%／10%选择年龄阶段，不按阶段改变品质。每批40条平均20条幼鱼、16条成长鱼、4条较成熟鱼；单批随机结果可以波动，并非强制配额。

阶段定义沿用此前方案：幼鱼年龄为本品质T80的0～20%，成长鱼20%～80%，较成熟鱼80%～150%，区间内均匀抽取；T80指自然长到该鱼自身上限80%所需时间。阶段是年龄区间，不是体长百分比，较成熟鱼也不保证已经达到80%体长。

{stage_table}

参考模型中每条鱼都从出生4cm按成长公式算到补入鱼龄，再在鱼塘内继续长；4cm只是模型的出生基准，混龄鱼并非以4cm补入。出生日期按鱼龄倒推，入塘日期另记，不编造在本鱼塘吃饵、脱钩等经历。当前程序的“按个体上限12%～28%生成幼鱼”不是本次模拟的初始化规则，因此以下是设计方案比较，不是当前程序实测。

## 三种补鱼方案对比

{summary_table}

P90表示约90%的钓获不超过该体长。三种方案从完全一致的开塘鱼群开始，并在同样的24小时内完成480次成功捕获；各自全天所有补鱼均使用对应年龄结构，并非只替换最后一批。

## 50%／40%／10%的品质变化

{quality_table}

## 50%／40%／10%的体长变化

{size_table}

钓获体长P10／中位数／P90为{c['length_quantiles_cm'][0]:.1f}／{c['length_quantiles_cm'][1]:.1f}／{c['length_quantiles_cm'][2]:.1f}厘米。日结后中位体长为{after['size_median_cm']:.1f}厘米。稀有品质或大体长段的平均条数可小于1，单塘仍是整数，小数不是保证每塘都能钓到。

## 鱼的来源与成长经历

全天480条中约{c['original_count']:.1f}条来自开塘鱼，约{c['replenished_count']:.1f}条来自当天补入；开塘100条最后留下约{after['original_survivors']:.1f}条。被钓走的补入鱼，在本塘停留时长P10／中位数／P90为{c['new_fish_residence_quantiles_hours'][0]:.2f}／{c['new_fish_residence_quantiles_hours'][1]:.2f}／{c['new_fish_residence_quantiles_hours'][2]:.2f}小时，{c['new_fish_residence_under_6h_pct']:.1f}%在6小时内被带走。

这些鱼会带着补入前的真实鱼龄和体长进入，但在本塘的经历仍然只有几小时。提高混龄比例能改善玩家钓获体长，不能把入塘前的成长算作玩家在本塘见证的成长。两种时间应分别展示。

## 模型边界

这仍是“每小时成功20条已经实现”的条件推演，未验证现行基础饵概率能达到该速度；没有将等待时间、收杆占用、20钓位隔离、迁徙、竿损坏、睡眠或营养与脱钩加速成长接入完整服务器模型。其余参数沿用前次：出生4cm、个体上限候选三角波动±15%、个体上限归一化尺寸。只能比较同一模型下年龄结构改变的效果，不是线上产量承诺。

![三种补鱼年龄结构的钓获与存量对比](restock-50-40-10.png)

原始结果restock-50-40-10-results.json；复现脚本compare_restock_50_40_10.py与simulate_full_pond.py；固定种子2026110303，4000塘。逐品质校验初始＋补入−捕获＝日结，校验480条钓获、480条补入、最终100条，以及开塘鱼去留守恒。
'''
(OUT/'restock-50-40-10-report.md').write_text(report)

font=FontProperties(fname='/System/Library/Fonts/Hiragino Sans GB.ttc')
plt.rcParams.update({'font.family':font.get_name(),'axes.unicode_minus':False,'font.size':10,'axes.spines.top':False,'axes.spines.right':False})
fig,axs=plt.subplots(2,2,figsize=(14,10),layout='constrained')
colors=['#cba77c','#94b3a8','#367c74']
for ax,field,title,ylabel in [(axs[0,0],'caught','玩家钓获：不满10cm明显减少','占480条钓获的比例（%）'),(axs[0,1],'after','日结后：存量鱼的体长更分散','每塘平均条数')]:
    x=np.arange(len(bins))
    for i,(policy,label,color) in enumerate(zip(policies,labels,colors)):
        vals=results[policy]['caught']['length_bin_counts'] if field=='caught' else results[policy]['hourly'][-1]['after']['length_bin_counts']
        vals=np.array(vals)/(480 if field=='caught' else 1)*(100 if field=='caught' else 1)
        ax.bar(x+(i-1)*.25,vals,width=.25,label=label,color=color)
    ax.set(title=title,xticks=x,xticklabels=bins,ylabel=ylabel);ax.tick_params(axis='x',labelrotation=20);ax.legend(fontsize=9)
ax=axs[1,0]
vals=[results[p]['caught']['length_quantiles_cm'][1] for p in policies]
ax.bar(labels,vals,color=colors)
for i,v in enumerate(vals):ax.text(i,v+.15,f'{v:.1f}cm',ha='center')
ax.set(title='钓获体长中位数提高',ylabel='厘米',ylim=(0,max(vals)*1.2))
ax=axs[1,1]
x=np.arange(7)
ax.bar(x-.2,a['initial']['quality_counts'],.4,label='开塘100条',color='#94b3a8')
ax.bar(x+.2,after['quality_counts'],.4,label='50/40/10日结补完',color='#367c74')
ax.set(title='品质结构：沿用品质缺口补鱼',xticks=x,xticklabels=names,ylabel='每塘平均条数');ax.legend(fontsize=9)
fig.suptitle('满塘每天钓走480条 · 补鱼年龄结构对比',fontsize=18,fontweight='bold')
fig.savefig(OUT/'restock-50-40-10.png',dpi=160);fig.savefig(OUT/'restock-50-40-10.svg');plt.close(fig)
print(json.dumps(dict(summary=metric_rows(),new_caught=c,new_before=before,new_after=after),ensure_ascii=False,indent=2))
