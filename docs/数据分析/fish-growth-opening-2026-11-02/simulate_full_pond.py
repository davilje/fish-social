"""满塘全天每小时成功带走20条的条件推演；不修改业务数据。"""
import json
from pathlib import Path
import numpy as np
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
from matplotlib.font_manager import FontProperties

OUT = Path(__file__).resolve().parent
DATA = OUT.parents[2] / 'shared/generated/game-data'
stats = json.loads((DATA / 'fish_quality_stats.json').read_text())
names = [r['displayName'] for r in stats]
caps_base = np.array([r['sizeCapM'] for r in stats])
bite_base = np.array([r['biteBaseAtMaxSize'] for r in stats])
weights = np.array([38, 28, 18, 9, 4, 2, 1], dtype=float)
prob = weights / weights.sum()
t80 = np.array([1, 2, 4, 5, 7, 10, 14], dtype=float)
quality_fit = np.array([1.1, 1.05, 1, .7, .5, .4, .3])
N = 4000
SEED = 2026110303
MIX_THRESHOLDS = {'mixed': (.7, .95), 'mixed_50_40_10': (.5, .9)}
edges = np.array([0, .1, .2, .5, 1, 10, np.inf])
bins = ['不满10cm', '10～不足20cm', '20～不足50cm', '50cm～不足1m', '1～不足10m', '10m及以上']

def growth(q, cap, born, now):
    k = np.log((cap - .04) / (.2 * cap)) / t80[q]
    return cap - (cap - .04) * np.exp(-k * np.maximum(0, now - born))

def intensity(q, size, cap):
    juvenile = .22 - .14 * np.clip((size - .02) / .33, 0, 1)
    main = .985 * (1 - (1 - np.clip(size / 40, 0, 1)) ** 1.8)
    escape = np.where(size <= .35, juvenile, main)
    return weights[q] * bite_base[q] * (.35 + .65 * np.clip(size/cap, 0, 1)) * quality_fit[q] * (1-escape)

def snapshot(q, size, alive, born, entered, now):
    qq, ss = q[alive], size[alive]
    age = (now-born[alive])*24
    return dict(count=float(alive.sum()/N), quality_counts=(np.bincount(qq, minlength=7)/N).tolist(),
                length_bin_counts=(np.histogram(ss, edges)[0]/N).tolist(),
                original_survivors=float(np.sum(alive & (entered == 0))/N),
                original_quality_counts=(np.bincount(q[alive & (entered == 0)], minlength=7)/N).tolist(),
                age_under_6h=float(np.sum(age < 6)/N), age_under_24h=float(np.sum(age < 24)/N),
                size_median_cm=float(np.median(ss)*100))

def run(policy):
    if policy not in {'newborn', *MIX_THRESHOLDS}:
        raise ValueError(f'Unknown replenishment policy: {policy}')
    rng = np.random.default_rng(SEED)
    q = rng.choice(7, (N,100), p=prob)
    cap = caps_base[q] * rng.triangular(.85, 1, 1.15, (N,100))
    k = np.log((cap-.04)/(.2*cap))/t80[q]
    upper = cap - (cap-.04)*np.exp(-14*k)
    size = .04 + rng.random((N,100))*(upper-.04)
    born = np.log((cap-size)/(cap-.04))/k
    entered = np.zeros((N,100), dtype=float)
    alive = np.ones((N,100), dtype=bool)
    initial = snapshot(q,size,alive,born,entered,0)
    rr = np.arange(N)
    catches = {key:[] for key in ['q','size','age','residence','original','ratio']}
    hourly, supplements = [], []

    def restock(now, target):
        counts = np.stack([np.sum(alive & (q == i),axis=1) for i in range(7)],axis=1)
        holes = target-counts.sum(axis=1)
        assert np.all(holes == holes[0])
        added_counts = np.zeros(7,dtype=int)
        for _ in range(int(holes[0])):
            deficit = np.maximum(0,weights-counts)
            w = deficit+weights*.1
            w[:,:2] *= np.where(deficit[:,:2] > 0,2.25,1)
            w[:,2] *= np.where((counts[:,2]>0) & (counts[:,2]/counts.sum(axis=1)>=prob[2]*.6),.15,1)
            w[:,3:] *= np.where(counts[:,3:]>0,.02,1)
            picked = np.sum(np.cumsum(w,axis=1)<(rng.random(N)*w.sum(axis=1))[:,None],axis=1)
            slot = np.argmax(~alive,axis=1)
            assert np.all(~alive[rr,slot])
            cc = caps_base[picked]*rng.triangular(.85,1,1.15,N)
            source_u, age_u = rng.random(N),rng.random(N)
            young_cut, growing_cut = MIX_THRESHOLDS.get(policy, (.7, .95))
            fraction = np.where(source_u<young_cut,age_u*.2,np.where(source_u<growing_cut,.2+age_u*.6,.8+age_u*.7))
            ages = np.zeros(N) if policy == 'newborn' else fraction*t80[picked]
            q[rr,slot],cap[rr,slot],born[rr,slot] = picked,cc,now-ages
            entered[rr,slot],alive[rr,slot] = now,True
            counts[rr,picked] += 1
            added_counts += np.bincount(picked,minlength=7)
        assert np.all(alive.sum(axis=1)==target)
        supplements.append(dict(hour=int(now*24),added=float(holes[0]),target=target,
                                quality_counts=(added_counts/N).tolist()))

    for hour in range(1,25):
        for event in range(20):
            now = (hour-1+(event+.5)/20)/24
            size = growth(q,cap,born,now)
            w = np.where(alive,intensity(q,size,cap),0)
            picked = np.sum(np.cumsum(w,axis=1)<(rng.random(N)*w.sum(axis=1))[:,None],axis=1)
            assert np.all(alive[rr,picked])
            values = [q[rr,picked],size[rr,picked],(now-born[rr,picked])*24,
                      (now-entered[rr,picked])*24,entered[rr,picked]==0,size[rr,picked]/cap[rr,picked]]
            for key,value in zip(catches,values): catches[key].append(value.copy())
            alive[rr,picked] = False
        now = hour/24
        before = snapshot(q,growth(q,cap,born,now),alive,born,entered,now)
        if hour == 24:
            restock(now,100)
        elif np.all(alive.sum(axis=1)<=40):
            restock(now,80)
        after = snapshot(q,growth(q,cap,born,now),alive,born,entered,now)
        hourly.append(dict(hour=hour,before=before,after=after))

    c = {key:np.concatenate(value) for key,value in catches.items()}
    added_q = np.sum([s['quality_counts'] for s in supplements],axis=0)
    caught_q = np.bincount(c['q'],minlength=7)/N
    assert np.allclose(np.array(initial['quality_counts'])+added_q-caught_q,hourly[-1]['after']['quality_counts'])
    assert sum(s['added'] for s in supplements)==480
    assert np.sum(c['original'])/N+hourly[-1]['after']['original_survivors']==100
    result = dict(policy=policy,initial=initial,hourly=hourly,supplements=supplements,
                  caught=dict(count=len(c['q'])/N,quality_counts=caught_q.tolist(),
                              length_bin_counts=(np.histogram(c['size'],edges)[0]/N).tolist(),
                              original_count=float(np.sum(c['original'])/N),
                              replenished_count=float(np.sum(~c['original'])/N),
                              true_age_under_6h_pct=float(np.mean(c['age']<6)*100),
                              new_fish_residence_under_6h_pct=float(np.mean(c['residence'][~c['original']]<6)*100),
                              length_quantiles_cm=(np.quantile(c['size'],[.1,.5,.9])*100).tolist(),
                              over_80pct_cap_pct=float(np.mean(c['ratio']>=.8)*100),
                              true_age_quantiles_hours=np.quantile(c['age'],[.1,.5,.9]).tolist(),
                              new_fish_residence_quantiles_hours=np.quantile(c['residence'][~c['original']],[.1,.5,.9]).tolist(),
                              by_quality={names[i]:dict(mean_size_cm=float(np.mean(c['size'][c['q']==i])*100) if np.any(c['q']==i) else None,
                                                       true_age_under_24h_pct=float(np.mean(c['age'][c['q']==i]<24)*100) if np.any(c['q']==i) else None,
                                                       over_80pct_cap_pct=float(np.mean(c['ratio'][c['q']==i]>=.8)*100) if np.any(c['q']==i) else None)
                                          for i in range(7)}),added_quality_counts=added_q.tolist())
    return result

def table(head,rows):
    return '| '+' | '.join(head)+' |\n| '+' | '.join(['---']*len(head))+' |\n'+'\n'.join('| '+' | '.join(map(str,row))+' |' for row in rows)

if __name__ == '__main__':
    results={policy:run(policy) for policy in ['newborn','mixed']}
    a,b=results['newborn'],results['mixed']
    before,after=a['hourly'][-1]['before'],a['hourly'][-1]['after']
    output=dict(seed=SEED,replicated_ponds=N,quality_names=names,length_bins=bins,results=results,
                assumptions=dict(successes='fixed 20 each hour, evenly spaced; total 480; not current probability throughput forecast',
                                 reference='seven-quality advanced pond; bamboo rod, basic bait, group fit 1; well-mixed fish pool',
                                 growth='T80 1/2/4/5/7/10/14d, 4cm birth, individual cap triangular(.85,1,1.15), length-uniform opening ages -14..0d',
                                 refill='hour-end check <=40 fill 80, daily end fill 100 takes priority; current quality deficit weights relative to capacity 100',
                                 omitted='spatial isolation, occupancy, migration, tackle failure, sleep, nutrition, explicit escapes and individual +/-10% bite/escape bias'))
    (OUT/'full-pond-results.json').write_text(json.dumps(output,ensure_ascii=False,indent=2))
    quality_table=table(['品质','开塘100条','全天钓走480条','日结前60条','日结补完100条'],
                        [[names[i],*[f'{v[i]:.2f}' for v in [a['initial']['quality_counts'],a['caught']['quality_counts'],before['quality_counts'],after['quality_counts']]]] for i in range(7)])
    size_table=table(['体长','全天钓走480条','日结前60条','补幼鱼后100条','候选混龄补完100条'],
                    [[bins[i],*[f'{v[i]:.2f}' for v in [a['caught']['length_bin_counts'],before['length_bin_counts'],after['length_bin_counts'],b['hourly'][-1]['after']['length_bin_counts']]]] for i in range(6)])
    report=f'''# 满塘全天捕捞：每小时20条，一天480条

    2026-11-03 · 4000个独立参考塘的离线条件推演，未修改业务算法。

    ## 场景与口径

    接受每小时已经成功带走20条这一前提，共480条，保持上一报告的相对捕获倾向，不据基础概率判断是否能达到该产量。采用七品质参考高级塘，开塘100条；成长、个体上限波动和开塘初始化沿用上一报告。默认补入鱼都是出生4cm的新生鱼；这是对照假设，补鱼鱼龄尚未最终确定。另列此前候选70%幼鱼、25%成长鱼、5%较成熟鱼的混龄方案。

    捕获对象按“品质抽取权重×品质咬钩基数×尺寸咬钩系数×竹竿品质适配×成功不脱钩概率”选择；每次捕获后重新计算存量。按新方案以个体上限归一化尺寸，不含鱼种适配差异。没有模拟钓位隔离、收杆占用、迁徙、睡眠、耐久、吃饵打窝及脱钩的额外生长。结果是这个简化模型的平均值，不能直接当作线上预测或其他类别塘的结果。

    ## 数量与补鱼节奏

    第1小时100→80，第2小时80→60，第3小时60→40，触发补40条到80。以后每两小时重复80→60→40→80。第3、5、7、9、11、13、15、17、19、21、23小时各补40条，共11次、440条。第24小时80→60，日结再补40条回到100。

    全天总计：开塘100＋途中补440＋日结补40−捕获480＝日结100条。沿用现有按品质缺口补鱼，理想品质数按容量100计算，逐条重算权重；低水位只决定补到80，不把品质理想数另改成80。

    ## 品质变化

    {quality_table}

    补鱼优先恢复缺少的普通、优良，因而能维持总量并大致拉回品质结构，但这不等于恢复鱼龄结构。表格小数是多塘平均；极小值经四舍五入可显示0，不代表绝无可能。

    ## 体长变化

    {size_table}

    上表前三个结果来自“全补新生鱼”；最后一列来自独立运行的候选混龄方案，全天过程都会受混龄补入影响，并非只在最后一刻换40条鱼。

    新生补鱼场景，全天钓获体长P10／中位数／P90为{a['caught']['length_quantiles_cm'][0]:.1f}／{a['caught']['length_quantiles_cm'][1]:.1f}／{a['caught']['length_quantiles_cm'][2]:.1f}厘米；日结后中位体长为{after['size_median_cm']:.1f}厘米。

    ## 原有鱼与新鱼去了哪里

    初始100条中，约{a['caught']['original_count']:.1f}条首日被钓走，约{after['original_survivors']:.1f}条留到日结；留下的原有鱼偏向不易钓获的品质。480条钓获中约{a['caught']['replenished_count']:.1f}条来自当天补入，占{a['caught']['replenished_count']/480*100:.1f}%。

    被钓走的补入鱼，入塘到钓走的时长P10／中位数／P90为{a['caught']['new_fish_residence_quantiles_hours'][0]:.2f}／{a['caught']['new_fish_residence_quantiles_hours'][1]:.2f}／{a['caught']['new_fish_residence_quantiles_hours'][2]:.2f}小时，其中{a['caught']['new_fish_residence_under_6h_pct']:.1f}%不到6小时就被带走。新生补鱼时，这也是真实鱼龄；混龄方案的入塘时长不能等同真实鱼龄。

    日结后100条中，约{after['age_under_6h']:.1f}条真实鱼龄不足6小时，约{after['age_under_24h']:.1f}条不足1天。即使普通1天可自然长到上限80%，也不代表它能在满负荷捕捞下活够1天。数量补足与年龄平衡是两件事。

    ## 设计含义

    该规则能防止总量持续下降，但全天480条的持续捕捞会使低品质鱼快速周转，出现“新生鱼反复补入、几小时内被钓走，较难钓的鱼长期留下”的分层。仅加快普通鱼到80%的速度不能消除这一问题；如果希望满塘仍能钓到有成长经历的鱼，后续需要确定补鱼来源和入塘鱼龄，或调整成功捕获对象的相对选择方式。本报告不替你改动这些规则。

    ![满塘一天的库存、品质、体长与鱼龄对比](full-pond.png)

    原始数据full-pond-results.json；复现脚本simulate_full_pond.py，固定随机种子2026110303，numpy与matplotlib。校验总库存、分品质守恒、每日480条捕获和480条补入，以及开塘100条的去留守恒。
    '''
    (OUT/'full-pond-report.md').write_text(report)

    font=FontProperties(fname='/System/Library/Fonts/Hiragino Sans GB.ttc')
    plt.rcParams.update({'font.family':font.get_name(),'axes.unicode_minus':False,'font.size':10,'axes.spines.top':False,'axes.spines.right':False})
    fig,axs=plt.subplots(2,2,figsize=(14,10),layout='constrained')
    ax=axs[0,0]
    xx,yy=[0],[100]
    for h in a['hourly']:
        xx.extend([h['hour'],h['hour']]);yy.extend([h['before']['count'],h['after']['count']])
    ax.plot(xx,yy,color='#367c74',lw=2)
    ax.axhline(40,color='#be8458',ls='--',label='低水位40条')
    ax.set(title='库存能维持：途中11次补鱼，日结再补一次',xlabel='开塘后小时',ylabel='存活鱼数',xticks=[0,3,6,12,18,24],ylim=(0,110));ax.legend()
    ax=axs[0,1];x=np.arange(7)
    for offset,vals,label,color in [(-.25,a['initial']['quality_counts'],'开塘','#b7c9c1'),(0,before['quality_counts'],'日结前','#dbb683'),(.25,after['quality_counts'],'日结补完','#367c74')]:
        ax.bar(x+offset,vals,.25,label=label,color=color)
    ax.set(title='缺口补鱼维持品质构成',xticks=x,xticklabels=names,ylabel='每塘平均条数');ax.legend(fontsize=9)
    ax=axs[1,0];x=np.arange(6)
    for offset,vals,label,color in [(-.25,a['initial']['length_bin_counts'],'开塘','#b7c9c1'),(0,after['length_bin_counts'],'全天补新生鱼','#dbb683'),(.25,b['hourly'][-1]['after']['length_bin_counts'],'全天候选混龄补鱼','#367c74')]:
        ax.bar(x+offset,vals,.25,label=label,color=color)
    ax.set(title='总量恢复，体长分布仍会变小',xticks=x,xticklabels=bins,ylabel='每塘平均条数');ax.tick_params(axis='x',labelrotation=20);ax.legend(fontsize=9)
    ax=axs[1,1]
    ax.bar(['被钓走的开塘鱼','被钓走的补入鱼'],[a['caught']['original_count'],a['caught']['replenished_count']],color=['#b7c9c1','#dbb683'])
    ax.set(title='全天480条钓获，大部分来自当天补入',ylabel='每塘平均条数')
    for i,value in enumerate([a['caught']['original_count'],a['caught']['replenished_count']]):ax.text(i,value+5,f'{value:.1f}条',ha='center')
    fig.suptitle('满塘压力推演 · 每小时成功20条，持续24小时',fontsize=18,fontweight='bold')
    fig.savefig(OUT/'full-pond.png',dpi=160);fig.savefig(OUT/'full-pond.svg');plt.close(fig)
    print(json.dumps(dict(caught=a['caught'],before=before,after=after,mixed_after=b['hourly'][-1]['after'],supplements=a['supplements']),ensure_ascii=False,indent=2))
