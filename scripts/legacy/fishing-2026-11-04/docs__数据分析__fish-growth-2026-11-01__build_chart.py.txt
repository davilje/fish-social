"""Render the formula comparison from shared source; requires matplotlib and the repository tsx dependency."""
import json
import os
from pathlib import Path
import subprocess

import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
from matplotlib.font_manager import FontProperties
import numpy as np

out = Path(__file__).resolve().parent
repo = out.parents[2]
module = (repo / 'shared/index.ts').as_uri()
script = """
import { calcIndividualFishLength, getSpecies } from '__MODULE__';
const species = getSpecies('carp');
const ages = Array.from({length: 337}, (_, i) => i / 48);
// Snapshot of the previous default model: 40m/7d shared curve, gray cap 0.2m, 0.01m precision.
const oldLength = age => Math.round(Math.max(.05, Math.min(.2,
  Math.round(40 * (1 - Math.pow(1 - Math.min(1, age / 7), 1.8)) * 100) / 100)) * 100) / 100;
const sizes = Array.from({length: 1000}, (_, i) => calcIndividualFishLength('gray', species, .05, 7, `fish-${i}`));
const curves = Array.from({length: 1000}, (_, i) => ages.map(age => calcIndividualFishLength('gray', species, .05, age, `fish-${i}`)));
console.log(JSON.stringify({ages, old: ages.map(oldLength), sizes, curves,
  summary: {samples: sizes.length, distinct: new Set(sizes).size, min: Math.min(...sizes), max: Math.max(...sizes)}}));
""".replace('__MODULE__', module)
result = subprocess.check_output([os.environ.get('FISH_REPORT_NODE', 'node'), '--import', 'tsx', '--input-type=module', '-e', script], cwd=repo, text=True)
data = json.loads(result)
font = Path('/System/Library/Fonts/STHeiti Light.ttc')
if font.exists():
    prop = FontProperties(fname=str(font))
    plt.rcParams['font.family'] = prop.get_name()
plt.rcParams['axes.unicode_minus'] = False
plt.rcParams.update({'font.size': 12, 'axes.spines.top': False, 'axes.spines.right': False, 'figure.facecolor': '#f7faf9', 'axes.facecolor': '#f7faf9'})
fig, axes = plt.subplots(1, 2, figsize=(14, 5.8), layout='constrained')
old, new = '#ae674c', '#26766b'
curves = np.array(data.pop('curves')) * 100
data['curveRangeCm'] = {'min': curves.min(axis=0).tolist(), 'median': np.median(curves, axis=0).tolist(), 'max': curves.max(axis=0).tolist()}
(out / 'comparison-data.json').write_text(json.dumps(data, ensure_ascii=False), encoding='utf-8')
ax = axes[0]
ax.fill_between(data['ages'], curves.min(axis=0), curves.max(axis=0), color=new, alpha=.17, label='新模型：1,000 条鱼的范围')
ax.plot(data['ages'], np.median(curves, axis=0), color=new, linewidth=2.4, label='新模型：中位长度')
ax.plot(data['ages'], np.array(data['old']) * 100, color=old, linewidth=2.4, linestyle='--', label='旧模型：同为普通品质')
ax.set(xlabel='鱼龄（天）', ylabel='长度（厘米）', title='成长曲线：长大后仍保留个体差异', xlim=(0,7), ylim=(0,65))
ax.legend(frameon=False, loc='lower right', fontsize=10)
ax.grid(axis='y', alpha=.16)
ax = axes[1]
ax.hist(np.array(data['sizes']) * 100, bins=np.arange(10, 65.1, 1), color=new, label=f"新模型：{data['summary']['distinct']} 种长度")
ax.hist([20] * 1000, bins=np.arange(10, 65.1, 1), color=old, alpha=.9, label='旧模型：全部 20.0cm')
ax.set(xlabel='长度（厘米，按 1cm 分组）', ylabel='鱼的数量', title='7 天同龄鱼：从统一触顶到自然分布', xlim=(10,65), ylim=(0,1080))
ax.legend(frameon=False, fontsize=10)
ax.annotate('旧：1,000 条', (20.5,1000), xytext=(25,880), arrowprops={'arrowstyle':'-', 'color':old}, color=old)
ax.text(46, 180, '新：43.9–58.5cm', color=new, fontsize=11)
ax.grid(axis='y', alpha=.16)
fig.suptitle('普通鲤鱼 · 新旧成长体系对比', fontsize=21, fontweight='bold')
fig.supxlabel('公式模拟：1,000 条鱼均从 5.0cm 出生；个体 ID 为 fish-0 至 fish-999。非线上捕获数据。', fontsize=10, color='#53635d')
fig.savefig(out / 'growth-comparison.png', dpi=150)
fig.savefig(out / 'growth-comparison.svg')
print(json.dumps(data['summary']))
