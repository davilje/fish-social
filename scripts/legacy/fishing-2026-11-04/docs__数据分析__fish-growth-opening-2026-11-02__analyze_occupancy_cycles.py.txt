"""人数、日内曲线与长期周期：50/40/10补鱼方案的离线推演。"""
import json
import sys
import numpy as np
from simulate_full_pond import OUT, names, caps_base, weights, prob, t80, growth, intensity, bins, edges, table, plt, FontProperties

SEED = 2026110304
FIRST_N = 1024
LONG_N = 256
DAYS = 365
PROFILES = {f'constant_{n}':[n]*24 for n in [0,1,5,10,20]}
PROFILES['work_20'] = [0]*9+[20]*8+[0]*7
LABELS = {**{f'constant_{n}':f'全天{n}人' for n in [0,1,5,10,20]}, 'work_20':'09～17时20人，其余无人'}
SAMPLE_DAYS = {1,2,7,30,90,180,365}

class PondBatch:
    def __init__(self, n):
        self.n=n;self.rng=np.random.default_rng(SEED);self.rows=np.arange(n)
        self.q=self.rng.choice(7,(n,100),p=prob)
        self.cap=caps_base[self.q]*self.rng.triangular(.85,1,1.15,(n,100))
        k=np.log((self.cap-.04)/(.2*self.cap))/t80[self.q]
        upper=self.cap-(self.cap-.04)*np.exp(-14*k)
        size=.04+self.rng.random((n,100))*(upper-.04)
        self.born=np.log((self.cap-size)/(self.cap-.04))/k
        self.entered=np.zeros((n,100));self.alive=np.ones((n,100),dtype=bool)
        self.added=0;self.caught=0
        self.initial_q=np.array(self.snap(0)['quality_counts'])
        self.added_q=np.zeros(7);self.caught_q=np.zeros(7)

    def size(self, now):return growth(self.q,self.cap,self.born,now)

    def snap(self, now):
        size=self.size(now);ss=size[self.alive];qq=self.q[self.alive]
        gray=size[self.alive & (self.q==0)]
        return dict(count=float(self.alive.sum()/self.n),median_cm=float(np.median(ss)*100),
                    p90_cm=float(np.quantile(ss,.9)*100),mean_cm=float(ss.mean()*100),
                    gray_median_cm=float(np.median(gray)*100) if gray.size else None,
                    quality_counts=(np.bincount(qq,minlength=7)/self.n).tolist(),
                    length_bin_counts=(np.histogram(ss,edges)[0]/self.n).tolist(),
                    originals=float(np.sum(self.alive & (self.entered==0))/self.n))

    def restock(self, now, target):
        counts=np.stack([np.sum(self.alive & (self.q==i),axis=1) for i in range(7)],axis=1)
        holes=target-counts.sum(axis=1)
        assert np.all(holes==holes[0])
        for _ in range(int(holes[0])):
            deficit=np.maximum(0,weights-counts);w=deficit+weights*.1
            w[:,:2]*=np.where(deficit[:,:2]>0,2.25,1)
            w[:,2]*=np.where((counts[:,2]>0)&(counts[:,2]/counts.sum(axis=1)>=prob[2]*.6),.15,1)
            w[:,3:]*=np.where(counts[:,3:]>0,.02,1)
            picked=np.sum(np.cumsum(w,axis=1)<(self.rng.random(self.n)*w.sum(axis=1))[:,None],axis=1)
            slot=np.argmax(~self.alive,axis=1);assert np.all(~self.alive[self.rows,slot])
            cc=caps_base[picked]*self.rng.triangular(.85,1,1.15,self.n)
            stage,u=self.rng.random(self.n),self.rng.random(self.n)
            fraction=np.where(stage<.5,u*.2,np.where(stage<.9,.2+u*.6,.8+u*.7))
            self.q[self.rows,slot],self.cap[self.rows,slot],self.born[self.rows,slot]=picked,cc,now-fraction*t80[picked]
            self.entered[self.rows,slot],self.alive[self.rows,slot]=now,True
            counts[self.rows,picked]+=1
            self.added_q+=np.bincount(picked,minlength=7)/self.n
        self.added+=int(holes[0])
        assert np.all(self.alive.sum(axis=1)==target)
        return int(holes[0])

    def remove(self, slots, size):
        rr=self.rows[:,None] if slots.ndim==2 else self.rows
        assert np.all(self.alive[rr,slots])
        sizes=size[rr,slots].ravel();qualities=self.q[rr,slots].ravel()
        self.caught_q+=np.bincount(qualities,minlength=7)/self.n
        self.alive[rr,slots]=False
        self.caught+=slots.size/self.n
        return sizes

    def hour(self, absolute_hour, count, exact, record=True):
        start=(absolute_hour-1)/24;end=absolute_hour/24;catch_sizes=[]
        if count and exact:
            for event in range(count):
                now=start+(event+.5)/count/24
                size=self.size(now);w=np.where(self.alive,intensity(self.q,size,self.cap),0)
                slot=np.sum(np.cumsum(w,axis=1)<(self.rng.random(self.n)*w.sum(axis=1))[:,None],axis=1)
                catch_sizes.append(self.remove(slot,size))
        elif count:
            # 小时中点冻结相对倾向；指数竞赛等价于冻结权重的无放回加权抽样。
            size=self.size((start+end)/2);w=np.where(self.alive,intensity(self.q,size,self.cap),0)
            clocks=np.full(w.shape,np.inf)
            np.divide(-np.log(np.maximum(self.rng.random(w.shape),1e-300)),w,out=clocks,where=w>0)
            slots=np.argpartition(clocks,count-1,axis=1)[:,:count]
            catch_sizes.append(self.remove(slots,size))
        before=self.snap(end) if record else None
        added=0
        if absolute_hour%24==0:added=self.restock(end,100)
        elif np.all(self.alive.sum(axis=1)<=40):added=self.restock(end,80)
        if not record:return None
        after=self.snap(end) if added else before
        sizes=np.concatenate(catch_sizes) if catch_sizes else None
        return dict(hour=absolute_hour,before=before,after=after,added=added,
                    catch_count=count,catch_median_cm=float(np.median(sizes)*100) if sizes is not None else None,
                    catch_under_10_pct=float(np.mean(sizes<.1)*100) if sizes is not None else None)

    def validate(self):
        last=self.snap(self.current_day)
        assert abs(100+self.added-self.caught-last['count'])<1e-8
        assert np.allclose(self.initial_q+self.added_q-self.caught_q,last['quality_counts'])
        assert np.all(self.alive.sum(axis=1)==100)

def run_first(profile, exact):
    model=PondBatch(FIRST_N);initial=model.snap(0)
    hours=[model.hour(h,profile[h-1],exact) for h in range(1,25)]
    model.current_day=1;model.validate()
    return dict(initial=initial,hours=hours,caught=model.caught,added=model.added)

def run_long(profile):
    model=PondBatch(LONG_N);initial=model.snap(0);daily=[];sampled={}
    for day in range(1,DAYS+1):
        keep=day in SAMPLE_DAYS
        hours=[model.hour((day-1)*24+h,profile[h-1],False,record=keep) for h in range(1,25)]
        snap=model.snap(day);daily.append(dict(day=day,after=snap))
        if keep:sampled[str(day)]=hours
        model.current_day=day;model.validate()
    return dict(initial=initial,daily=daily,sampled=sampled,caught=model.caught,added=model.added)

if '--render-only' in sys.argv:
    output=json.loads((OUT/'occupancy-cycles-results.json').read_text())
    first=output['first_day'];long=output['long_run']
    drift=output['late_window_drift'];audit_diff=output['approximation_audit']
    assert output['first_day_ponds']==FIRST_N and output['long_run_ponds']==LONG_N and output['days']==DAYS
else:
    first={}
    for key,profile in PROFILES.items():
        first[key]=run_first(profile,True)
        print(f'首日完成：{LABELS[key]}',flush=True)
    audit=run_first(PROFILES['constant_20'],False)
    audit_diff=dict(median_cm=abs(audit['hours'][-1]['after']['median_cm']-first['constant_20']['hours'][-1]['after']['median_cm']),
                    under_10_count=abs(audit['hours'][-1]['after']['length_bin_counts'][0]-first['constant_20']['hours'][-1]['after']['length_bin_counts'][0]))
    long={}
    for key,profile in PROFILES.items():
        long[key]=run_long(profile)
        print(f'365天完成：{LABELS[key]}',flush=True)

    def window_stat(key,begin,end):
        records=[d['after'] for d in long[key]['daily'][begin-1:end]]
        return dict(median_cm=float(np.mean([r['median_cm'] for r in records])),
                    quality_counts=np.mean([r['quality_counts'] for r in records],axis=0).tolist(),
                    length_counts=np.mean([r['length_bin_counts'] for r in records],axis=0).tolist())

    drift={}
    for key in PROFILES:
        prev,last=window_stat(key,306,335),window_stat(key,336,365)
        drift[key]=dict(previous=prev,last=last,
                        median_change_cm=last['median_cm']-prev['median_cm'],
                        quality_l1_half_count=float(np.sum(np.abs(np.array(last['quality_counts'])-prev['quality_counts']))/2),
                        gold_change=last['quality_counts'][6]-prev['quality_counts'][6])

    output=dict(seed=SEED,first_day_ponds=FIRST_N,long_run_ponds=LONG_N,days=DAYS,profiles=PROFILES,
                first_day=first,long_run=long,late_window_drift=drift,approximation_audit=audit_diff,
                assumptions=dict(mix='50/40/10, age 0-.2/.2-.8/.8-1.5 times quality T80',
                                 throughput='one successful fish per player-hour, externally imposed, not runtime throughput',
                                 first_day='exact within-hour sequential selection and growth',
                                 long_run='hour midpoint growth frozen for relative selection; weighted sampling without replacement, end-hour real growth',
                                 other='same advanced reference pond, birth4cm, cap triangular +/-15%, no mortality, nutrition, explicit escapes, spatial effects or tackle failures'))
    (OUT/'occupancy-cycles-results.json').write_text(json.dumps(output,ensure_ascii=False,indent=2))

font=FontProperties(fname='/System/Library/Fonts/Hiragino Sans GB.ttc')
plt.rcParams.update({'font.family':font.get_name(),'axes.unicode_minus':False,'font.size':10,'axes.spines.top':False,'axes.spines.right':False})
colors=['#858e91','#5a9f83','#668dc0','#c79b53','#b36f73','#8d70af']
fig,axs=plt.subplots(2,2,figsize=(14,10),layout='constrained')
for (key,label),color in zip(LABELS.items(),colors):
    data=first[key];x=[0];med=[data['initial']['median_cm']];gray=[data['initial']['gray_median_cm']];count=[100]
    for h in data['hours']:
        for moment in ['before','after']:
            x.append(h['hour']);med.append(h[moment]['median_cm']);gray.append(h[moment]['gray_median_cm']);count.append(h[moment]['count'])
    axs[0,0].plot(x,med,label=label,color=color,lw=1.8)
    axs[0,1].plot(x,gray,label=label,color=color,lw=1.8)
    axs[1,0].plot(np.arange(1,25),[h['catch_median_cm'] for h in data['hours']],label=label,color=color,lw=1.8)
    axs[1,1].plot(x,count,label=label,color=color,lw=1.8)
for ax,title,ylabel in zip(axs.ravel(),['鱼塘存量：体长中位数','单看普通鱼：体长中位数','玩家当前小时钓获：体长中位数','库存与补鱼：人数改变补鱼频率'],['厘米','厘米','厘米','条']):
    ax.set(title=title,xlabel='开塘后小时',ylabel=ylabel,xticks=[0,6,9,12,17,24]);ax.legend(fontsize=8,ncol=2)
fig.suptitle('首日24小时 · 人数与工作时段对鱼群的影响',fontsize=18,fontweight='bold')
fig.savefig(OUT/'occupancy-day.png',dpi=160);fig.savefig(OUT/'occupancy-day.svg');plt.close(fig)

fig,axs=plt.subplots(2,2,figsize=(14,10),layout='constrained')
for (key,label),color in zip(LABELS.items(),colors):
    dd=long[key]['daily'];x=np.arange(1,DAYS+1)
    axs[0,0].plot(x,[d['after']['median_cm'] for d in dd],label=label,color=color,lw=1.5)
    axs[0,1].plot(x,[d['after']['length_bin_counts'][0] for d in dd],label=label,color=color,lw=1.5)
    axs[1,0].plot(x,[d['after']['quality_counts'][6] for d in dd],label=label,color=color,lw=1.5)
for ax,title,ylabel in zip([axs[0,0],axs[0,1],axs[1,0]],['每日结算后：整体体长中位数','每日结算后：不满10cm数量','每日结算后：至尊存量，检验慢变化'],['厘米','条／100条','条／100条']):
    ax.set(title=title,xlabel='天数',ylabel=ylabel);ax.legend(fontsize=8,ncol=2)
ax=axs[1,1]
for key,color in [('constant_1',colors[1]),('constant_20',colors[4]),('work_20',colors[5])]:
    hours=long[key]['sampled']['365']
    x=[0];values=[long[key]['daily'][363]['after']['median_cm']]
    for h in hours:
        for moment in ['before','after']:
            x.append(h['hour']-364*24);values.append(h[moment]['median_cm'])
    ax.plot(x,values,label=LABELS[key],color=color)
ax.set(title='第365天：后期仍有日内波动',xlabel='当日小时',ylabel='存量体长中位数（cm）');ax.legend(fontsize=8)
fig.suptitle('365天推演 · 指标趋稳不代表全部品质已稳定',fontsize=18,fontweight='bold')
fig.savefig(OUT/'occupancy-long.png',dpi=160);fig.savefig(OUT/'occupancy-long.svg');plt.close(fig)

day_table=table(['场景','每天钓走','0小时','6小时','12小时','18小时','24小时补完'],
                [[LABELS[key],sum(PROFILES[key]),f"{first[key]['initial']['median_cm']:.1f}cm",*[f"{first[key]['hours'][h-1]['after']['median_cm']:.1f}cm" for h in [6,12,18,24]]] for key in PROFILES])
long_table=table(['场景','第1天','第7天','第30天','第90天','第180天','第365天'],
                 [[LABELS[key],*[f"{long[key]['daily'][d-1]['after']['median_cm']:.1f}cm" for d in [1,7,30,90,180,365]]] for key in PROFILES])
drift_table=table(['场景','前30天中位体长均值','末30天中位体长均值','品质分布变化量','至尊数量变化'],
                  [[LABELS[key],f"{drift[key]['previous']['median_cm']:.2f}cm",f"{drift[key]['last']['median_cm']:.2f}cm",f"{drift[key]['quality_l1_half_count']:.2f}条",f"{drift[key]['gold_change']:+.2f}条"] for key in PROFILES])
report=f'''# 人数与时间变化：日内体长曲线、稳定态与周期波动

2026-11-03 · 50%幼鱼／40%成长鱼／10%较成熟鱼 · 仅离线分析，未修改业务算法。

## 模拟口径

同一参考高级塘，开塘100条、初始品质38/28/18/9/4/2/1、新成长速度及个体上限波动沿用前次。每人每小时成功带走1条是外部给定目标，不是代码默认概率的实测产量。比较0、1、5、10、20人全天钓鱼，以及09:00～17:00有20人、其余无人；全天20人可由不同玩家轮班，不假设同一玩家在线24小时。

每小时末检查≤40补至80，日结优先补到100，品质仍按原有缺口权重逐条选择；每条补入鱼以50/40/10独立抽取年龄阶段，按T80的0～20%／20～80%／80～150%赋龄并算体长。出生基准4cm与个体上限±15%仍是对照模型设定，不是当前程序补鱼参数；出生时间与入塘时间分开。无衰老死亡、繁殖、捕食、营养或脱钩额外成长，不模拟钓位隔离、耐久和收杆。

首日使用1024个独立塘，逐条捕获前更新成长。365天使用256个独立塘，为降低计算量，将本小时捕获倾向冻结在小时中点，用无放回加权抽样移除当小时捕获数，小时末按真实时间计算体长。首日20人场景，近似法与逐条法的日结中位体长差{audit_diff['median_cm']:.2f}cm，不满10cm数量差{audit_diff['under_10_count']:.2f}条；这是合并随机波动的敏感性参照，不能保证所有场景和长期误差都同样小。

以下体长均为所有模拟塘中存活鱼的合并中位数，非少量巨物拉高的算术平均数；小时结果为该小时补鱼后，24小时结果为日结后。玩家钓获另画曲线，不与存量混用。

## 首日：人数改变捕捞与补鱼频率

{day_table}

![首日不同人数下的体长与库存曲线](occupancy-day.png)

无人时只有原鱼自然成长，没有缺口就不补鱼；人少时原有鱼有更多时间长大，日结补鱼形成一次扰动；人多时普通、优良被反复带走，补鱼更频繁，存量与钓获体长会受到不同影响。某段存量体长上升，可能是小鱼被钓走或大鱼留下，并不意味着玩家更容易钓到大鱼；可对照普通鱼和实际钓获曲线。

## 多日变化：用同一时刻比较，避免把日内波动当趋势

{long_table}

![长期中位体长、小鱼比例与至尊存量](occupancy-long.png)

## 稳定态如何判断

库存总数可被规则稳定在一个范围内，但体长、鱼龄、品质不因此自动稳定。最后两个相邻30天窗口为第306～335天与第336～365天，比较每天同一结算时刻：

{drift_table}

20人全天场景，第7天与第365天的日结中位体长分别为{long['constant_20']['daily'][6]['after']['median_cm']:.1f}cm和{long['constant_20']['daily'][-1]['after']['median_cm']:.1f}cm，常见体长很快进入一个波动区间；但至尊从开塘平均{long['constant_20']['initial']['quality_counts'][6]:.1f}条增加到第365天的{long['constant_20']['daily'][-1]['after']['quality_counts'][6]:.1f}条，最后两个30天窗口仍增加约{drift['constant_20']['gold_change']:.2f}条。这支持“常见体长趋稳、极稀有存量仍在慢变”，不能认定完整生态已经稳定。该场景第365天算术平均体长为{long['constant_20']['daily'][-1]['after']['mean_cm']/100:.2f}米，而中位数只有{long['constant_20']['daily'][-1]['after']['median_cm']:.1f}厘米，说明少量巨物会严重影响平均值。

品质分布变化量为两窗口各品质平均条数差的绝对值总和的一半，用于观察整体结构移动；不是统计显著性或严格收敛判据。256塘的一年推演只能提供有限期证据，不能用某个指标接近平坦证明完整生态已稳定。特别要看不易钓获的至尊是否仍积累。

在固定非零捕捞压力、补鱼来源、品质权重和时刻安排下，可能逐渐形成重复的统计分布：同一日内时刻的体长结构相近，但单条鱼和单塘仍随机变化。高品质捕获倾向极低、仍有非零补入机会、又没有衰老死亡，使这部分库存可能在远长于普通鱼周转的时间尺度上继续变化。整体中位数先稳定，不代表稀有鱼群、鱼龄尾部也稳定。无人塘的鱼龄持续增加，体长则渐近各自上限，不能称为具有持续个体更替的生态平衡。

## 能否形成周期性波动

可以形成由规则和客流驱动的波动：阈值补鱼使库存下降后突然回升；新补入鱼通常较年轻，体长分布在补鱼时跳变，随后随生长和选择性捕捞变化；每天固定结算与重复的工作日程带来24小时相位。20人恒定捕捞时，开塘首日第3小时起每两小时补40条，日结把库存拉回100，下一天重走相同数量节奏。

1人全天每天只钓24条，不触发低水位，主要体现日结一次补鱼；5人会在第12小时首次触发，之后第20小时再触发；10人在第6小时首次触发，此后每4小时一次；20人首次第3小时，此后每2小时一次。09～17时20人的场景，工作时段反复捕捞补鱼，夜间停止移除让原有鱼继续长，构成更明显的客流日周期。具体体长跳变方向和幅度取决于当时存量与补入年龄，不能把库存锯齿直接当作同幅度的体长锯齿。

这些主要是人为补鱼、日结和客流带来的外部周期，不是模型已经产生繁殖、衰老或捕食的自主生态循环；客流随机时，周期会被扰动。若希望玩家感到有涨有落，现有规则已经能制造变化；若希望长期品质与鱼龄都可控，仍需要监测难钓鱼积累和老鱼去向，不能只监测鱼数或体长中位数。

原始数据occupancy-cycles-results.json，脚本analyze_occupancy_cycles.py，固定种子2026110304。逐日校验数量守恒、逐品质守恒、每天结算100条。曲线与结论均针对这个简化设计模型。
'''
(OUT/'occupancy-cycles-report.md').write_text(report)
print(json.dumps(dict(day_table=day_table,long_table=long_table,drift=drift,approximation_audit=audit_diff),ensure_ascii=False,indent=2))
