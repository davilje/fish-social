"""Render FISH-ECOLOGY-01 stress results; reads simulation JSON, never database state."""
import json
from pathlib import Path
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
from matplotlib.font_manager import FontProperties

OUT=Path(__file__).resolve().parent
data=json.loads((OUT/'implementation-simulation.json').read_text())
font=FontProperties(fname='/System/Library/Fonts/Hiragino Sans GB.ttc')
plt.rcParams.update({'axes.spines.top':False,'axes.spines.right':False,'axes.grid':True,'grid.alpha':.18})
fig,axes=plt.subplots(2,2,figsize=(14,10),layout='constrained')
colors={0:'#8c9692',5:'#3b947e',20:'#c98551'}
for run in [r for r in data['summaries'] if r['pondId']=='pond-calm']:
    count=run['anglers'];color=colors[count];label=f'{count}人同时在钓'
    first=run['firstDay'];days=run['daily']
    x=[0];y=[100]
    for hour in first[1:]:
        x += [hour['hour'],hour['hour']]
        y += [hour['before'],hour['after']]
    axes[0,0].plot(x,y,color=color,label=label,lw=2)
    axes[0,1].plot([h['hour'] for h in first],[h['medianM']*100 for h in first],color=color,label=label,lw=2)
    axes[1,0].plot([0]+[d['day'] for d in days],
                   [run['opening']['meanCapRatio']*100]+[d['afterSettlement']['meanCapRatio']*100 for d in days],
                   color=color,label=label,lw=2)
    if count:
        axes[1,1].plot([d['day'] for d in days],[d['catchMedianM']*100 for d in days],color=color,label=label,lw=2)
titles=['24小时库存：低水位补鱼＋04:00补足','第一天：在塘鱼中位体长',
        '30天：在塘鱼平均占自身上限的比例','30天：每天钓获鱼的中位体长']
xlabels=['开塘后小时（04:00开始）','开塘后小时','开塘后天数','开塘后天数']
ylabels=['在塘条数','体长（厘米）','占个体上限（%）','体长（厘米）']
for ax,title,xlabel,ylabel in zip(axes.flat,titles,xlabels,ylabels):
    ax.set_title(title,fontproperties=font,fontsize=14)
    ax.set_xlabel(xlabel,fontproperties=font)
    ax.set_ylabel(ylabel,fontproperties=font)
    ax.legend(prop=font,fontsize=10)
axes[0,0].set(ylim=(30,105),xlim=(0,24))
axes[0,0].axhline(40,color='#b96761',ls='--',lw=.8)
axes[0,1].set(xlim=(0,24))
axes[1,0].set(ylim=(0,105),xlim=(0,30))
axes[1,1].set(xlim=(1,30))
fig.suptitle('高级塘 · 10个固定种子均值\n给定成功捕获1条／人／小时的压力情景',fontproperties=font,fontsize=19)
fig.savefig(OUT/'implementation-simulation.png',dpi=160)
fig.savefig(OUT/'implementation-simulation.svg')
plt.close(fig)
print('Rendered implementation-simulation.png / .svg')
