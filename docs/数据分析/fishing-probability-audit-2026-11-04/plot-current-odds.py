from pathlib import Path
import json
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
import numpy as np

root = Path(__file__).resolve().parent
data = json.loads((root/'current-odds.json').read_text())
plt.rcParams.update({'font.family':'sans-serif','font.sans-serif':['PingFang SC','Arial Unicode MS','DejaVu Sans'],
                     'axes.unicode_minus':False,'font.size':11,'axes.spines.top':False,'axes.spines.right':False})
fig, axs = plt.subplots(2,2,figsize=(14,10),layout='constrained')
fig.suptitle('当前钓鱼概率：代码默认值与计算示例\n2026-11-04 · 静态概率与收杆时长 · 非在线产量实测',fontsize=17)
qs=data['qualityTable']; x=np.arange(len(qs)); colors=['#9e9e9e','#4caf50','#2196f3','#9c27b0','#f44336','#ff9800','#b49a20']
a=axs[0,0]; a.bar(x,[q['biteAtCap']*100 for q in qs],color=colors,width=.6)
a.set(xticks=x,xticklabels=[q['name'] for q in qs],ylabel='每次判定概率（%）',title='被选中后：满尺寸基础咬钩率\n其他系数为1，无付费饵')
for i,q in enumerate(qs): a.text(i,q['biteAtCap']*100+.003,f"{q['biteAtCap']*100:g}%",ha='center',fontsize=9)
a.grid(axis='y',alpha=.15)
a=axs[0,1]; a.plot([q['name'] for q in qs],[q['biteAtCap']*100 for q in qs],'o-',color='#687970',label='基础免费饵')
a.plot([q['name'] for q in qs],[(q['biteAtCap']+.06)*100 for q in qs],'o-',color='#db8a37',label='匹配草食／杂食付费饵 +0.06')
a.set(yscale='log',ylabel='每次判定概率（%，对数轴）',title='付费饵加值远大于基础概率\n满尺寸，钓位／鱼竿／鱼塘／个体系数均为1')
a.grid(alpha=.15); a.legend(fontsize=9)
a=axs[1,0]; lo=np.linspace(.02,.35,300); hi=np.linspace(.350001,1,500)
a.plot(lo*100,(.22-.14*(lo-.02)/.33)*100,color='#c77638',label='≤35cm 幼鱼段')
a.plot(hi*100,.985*(1-(1-hi/40)**1.8)*100,color='#297763',label='>35cm 主曲线')
a.plot([35,35],[8,.985*(1-(1-.35/40)**1.8)*100],'--',color='#b55a50')
a.scatter([35,35.1],[8,.985*(1-(1-.351/40)**1.8)*100],color='#b55a50',zorder=5)
a.annotate('35.0cm：8%\n35.1cm：约1.55%',xy=(35,8),xytext=(47,13),arrowprops={'arrowstyle':'->','color':'#b55a50'},fontsize=10)
a.set(xlabel='体长（厘米）',ylabel='脱钩概率（%，减免前）',title='当前脱钩曲线在35厘米处断开')
a.legend(fontsize=9,loc='upper right'); a.grid(alpha=.15)
a=axs[1,1]; selected=[.04,.2,.35,1,3,15,40]; times=[next(r['hookMs'] for r in data['sizeTable'] if r['sizeM']==s)/60000 for s in selected]
a.barh([f'{s*100:g}cm' if s<1 else f'{s:g}m' for s in selected],times,color='#5b9485')
for i,v in enumerate(times): a.text(v+1,i,f'{v:.2f}分钟',va='center',fontsize=9)
a.set(xlabel='收杆时间（分钟）',xlim=(0,146),title='成功／自然脱钩都要等待收杆\n默认缩放1，期间不参加新的咬钩判定')
a.grid(axis='x',alpha=.15)
fig.savefig(root/'current-odds-comparison.png',dpi=160)
fig.savefig(root/'current-odds-comparison.svg')
print('current-odds-comparison.png / svg generated')
