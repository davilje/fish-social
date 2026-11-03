"""构建鱼类参考手册。仅写本目录的资料与图表，不修改业务数据。"""
from pathlib import Path
import json
import math
import html
import numpy as np
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
from matplotlib.font_manager import FontProperties

OUT = Path(__file__).resolve().parent
ROOT = OUT.parents[2]
species = json.loads((ROOT/'shared/generated/game-data/fish_species.json').read_text())

FAO = 'https://www.fao.org/fishery/docs/DOCUMENT/aquaculture/CulturedSpecies/file/en/'
CD = 'https://www.fao.org/fishery/docs/CDrom/aquaculture/I1129m/file/en/'
FB = 'https://www.fishbase.se/summary/'
S = {
 'carp': ('FAO · 鲤鱼', CD+'en_commoncarp.htm'),
 'grass': ('FAO · 草鱼', FAO+'en_grasscarp.htm'),
 'silver': ('FAO · 鲢鱼', CD+'en_silvercarp.htm'),
 'bighead': ('FAO · 鳙鱼', FAO+'en_bigheadcarp.htm'),
 'tilapia': ('FAO · 尼罗罗非鱼', FAO+'en_niletilapia.htm'),
 'mandarin': ('FAO · 鳜鱼', FAO+'en_mandarinfish.htm'),
 'trout': ('FAO · 虹鳟', FAO+'en_rainbowtrout.htm'),
 'eel': ('FAO · 日本鳗鲡', FAO+'en_japaneseeel.htm'),
 'mullet': ('FishBase · 鲻鱼', FB+'Mugil-cephalus'),
 'crucian': ('FishBase · Carassius auratus，不能代表全部鲫属鱼', 'https://www.fishbase.org/summary/Carassius_auratus.html'),
 'loach': ('USGS · 泥鳅物种资料', 'https://nas.er.usgs.gov/queries/greatLakes/FactSheet.aspx?H=&Potential=N&Species_ID=498&Type=0'),
 'black': ('加拿大DFO · 青鱼', 'https://www.dfo-mpo.gc.ca/species-especes/profiles-profils/blackcarp-carpenoire-eng.html'),
 'catfish': ('FishBase · 鲶 Silurus asotus', FB+'silurus-asotus.html'),
 'yellowcat': ('FishBase · 黄颡鱼', FB+'Tachysurus-fulvidraco.html'),
 'snakehead': ('FishBase · 乌鳢', FB+'Channa_argus.html'),
 'bass': ('USFWS · 大口黑鲈资料，注意分类版本', 'https://www.fws.gov/species/largemouth-bass-micropterus-salmoides'),
 'wuchang': ('NCBI · 团头鲂/武昌鱼名称', 'https://www.ncbi.nlm.nih.gov/Taxonomy/Browser/wwwtax.cgi?id=75352'),
 'bream': ('FishBase · 鳊', FB+'282'),
 'changchun': ('2025年原始研究 · 长春鳊群体比较', 'https://xuebao.dlou.edu.cn/article/2025/2095-1388/202501015.html'),
 'sturgeon': ('NOAA · 中华鲟', 'https://www.fisheries.noaa.gov/species/chinese-sturgeon/resources'),
 'taimen': ('1998年原始研究 · 贝加尔湖流域哲罗鲑', 'https://pubs.usgs.gov/publication/1000685'),
 'ide': ('2019年原始研究 · 汤旺河瓦氏雅罗鱼', 'https://xuebao.dlou.edu.cn/article/2019/2095-1388/201906010.html'),
 'grasspaper': ('2021年原始研究 · 珠江中下游草鱼', 'https://www.schinafish.cn/cn/article/Y2021/I5/110'),
 'redporgy': ('FishBase · 真鲷', FB+'PAGRUS-MAJOR.html'),
 'grouper': ('FishBase · 赤点石斑鱼，仅作具体物种示例', FB+'Epinephelus_akaara.html'),
 'hairtail': ('FishBase · Trichiurus lepturus，仅代表该物种', FB+'Trichiurus-lepturus'),
 'mackerel': ('FishBase · 日本鲐', FB+'Scomber-japonicus.html'),
 'flounder': ('2025年研究团队会议摘要 · 牙鲆养殖', 'https://www.was.org/Meeting/Program/PaperDetail/166172'),
 'yelloweel': ('中科院水生所 · 黄鳝及性反转', 'https://www.ihb.cas.cn/xwdt/kydt/202510/t20251027_7997821.html'),
 'longcat': ('中科院水生所 · 长吻鮠食性研究', 'https://www.ihb.cas.cn/xwdt/kydt/202408/t20240823_7309953.html'),
 'golden': ('FishBase · 金线鲃属物种索引', 'https://www.fishbase.se/identification/SpeciesList.php?genus=Sinocyclocheilus'),
 'silverid': ('原始调查 · 海南鹦哥岭鱼类，含银鲴学名', 'https://www.biodiversity-science.net/EN/10.3724/SP.J.1003.2008.07021'),
}

# 学名候选不表示项目已经绑定相应分类。未充分核验的条目保留缺失值。
# 每行：ID、分类检索定位、生态组、认识重点、身份/资料限制。
ROWS = [
 ('crucian','鲫属 Carassius；以 C. auratus 资料作例','淡水常见鱼','常与鲤鱼一起出现，但不是小号鲤鱼。辨认时可留意口部、须及体形。','中文鲫鱼涉及不同种、群体和养殖品系；不能将欧洲鲫 C. carassius 的资料直接照搬。'),
 ('carp','Cyprinus carpio','淡水常见鱼','底栖觅食为主的杂食鱼，也会利用水体其他层的食物；吃谷物不表示严格草食。','不同品系和温度下成长明显不同；锦鲤条目需与此区分品系和物种层级。'),
 ('grass_carp','Ctenopharyngodon idella；旧资料常作 idellus','淡水常见鱼','成年偏植物食性，幼早期食物不同；大型成鱼不能仅靠“多活几天”来解释。','温暖地区的养殖周期不能直接用于寒冷地区；不要把鱼种培育时间当作成鱼上限时间。'),
 ('silver_carp','Hypophthalmichthys molitrix','滤食与植物食性','典型滤食型鱼，利用细小悬浮食物；不能与直接吞下一团饵的鱼等同。','藻类、颗粒大小和水体食物条件，比“人投了多少块饵”更能解释营养获得。'),
 ('bighead_carp','Hypophthalmichthys nobilis','滤食与植物食性','鳙鱼俗称胖头鱼，偏利用浮游动物等悬浮食物，与鲢有食物生态位差异。','滤食偏好不是绝对单一食谱；养殖育成周期不等于寿命或最大体型周期。'),
 ('black_carp','Mylopharyngodon piceus','淡水大型鱼','成鱼偏食软体动物，咽齿适于处理带壳食物；食物限制有物种特点。','“巨青”通常仍是青鱼的大个体，不是另一生物学物种。'),
 ('tilapia','罗非鱼类群；本手册数值例为 Oreochromis niloticus','暖水鱼','可利用多类食物，部分罗非鱼有口孵育幼行为；高繁殖补充不保证每条都长大。','养殖罗非鱼可能是不同种或杂交群体，尼罗罗非鱼数字不能自动覆盖全部条目。'),
 ('loach','泥鳅 Misgurnus anguillicaudatus 作为定位','底栖与鳗形鱼','小型、细长、近底层生活的代表；不能因为细长就与鳗鲡归为一类。','泥鳅与大鳞副泥鳅等相似鱼要先确认；资料中TL与SL须保留原标记。'),
 ('bream','鳊 Parabramis pekinensis，或地方泛称','鳊鲂与鲴类','较高、侧扁的体形与细长鱼差别很大，同长度的视觉体量也不同。','若此处指P. pekinensis，就可能与长春鳊条目重复；先确认美术与命名。'),
 ('bluntnose_bream','Megalobrama amblycephala','鳊鲂与鲴类','团头鲂也是通常所称的武昌鱼，名称不同不代表成长史不同。','不采用缺乏核对的网络极端尺寸；本次未提供可靠统一逐龄体长表。'),
 ('white_amur_bream','长春鳊：Parabramis pekinensis','鳊鲂与鲴类','2025年的群体比较研究明确使用这一学名；英文White amur bream也对应这一对象。','注意和鲂属的三角鲂区分；不要依据中文俗名另设一个无依据的物种。'),
 ('mandarin','鳜 Siniperca chuatsi','淡水捕食鱼','躲藏、伏击，幼早期便能捕食其他鱼苗；同类相食也可能使个体差异扩大。','资料中的克、公斤是体重，不能缺少长重关系就擅自换成厘米。'),
 ('topmouth','翘嘴鲌；候选定位 Culter alburnus','淡水捕食鱼','可先从鲌类的侧扁体形和捕食生活方式认识，辨认时留意口位和轮廓。','分类索引：尚未逐条核验该项目形象对应的物种及逐龄成长数据。'),
 ('black_bass','大口黑鲈类；传统资料作 Micropterus salmoides','淡水捕食鱼','伏击捕食者；幼鱼和较大个体利用的猎物会有变化。','近年大口黑鲈与佛罗里达相关类群的命名有调整；项目需注明所采用的分类版本。'),
 ('snakehead','Channa argus','淡水捕食鱼','乌鳢俗称黑鱼，属于鳢类，不是黑鲈，也不是鲶鱼。','成熟时间和体型取决于群体；这里只使用数据库明确列出的指标。'),
 ('catfish','以鲶 Silurus asotus 为例','底栖捕食鱼','有须、少鳞或无鳞的外观很醒目；“鲶鱼”俗称也常被用于多种不同的鲇形目鱼。','不能把南方大口鲇、胡子鲶或其他巨型鲶鱼的纪录归到同一个条目。'),
 ('yellow_catfish','Tachysurus fulvidraco；可见 Pelteobagrus 等旧属名','底栖捕食鱼','黄颡鱼的须和鳍棘有辨识度；偏底部摄食，雄鱼有守护卵与幼体行为。','查旧资料须检查异名；旧属名不同不代表存在多套不同生长上限。'),
 ('silver_bream','银鲴 Xenocypris argentea','鳊鲂与鲴类','辨认时应把鲴类与鲂、鲌分开；项目英文ID中的bream不是可靠分类依据。','本次核对到学名入口，未核验统一体长—年龄或寿命数值。'),
 ('redfin','红鳍鲌；Culter / Chanodichthys 等属名需核对','淡水捕食鱼','红色鱼鳍是外观线索，不能据此与所有“红鳍”的鱼合并。','项目中文名需要结合图像与当前分类资料定位；本版不强行给出唯一学名。'),
 ('river_chub','马口鱼；候选定位 Opsariichthys bidens','溪流与特殊鱼','可作为溪流鱼的认识入口；不能只按river_chub这个英文ID套入欧美同名鱼资料。','分类索引：体长、成熟及寿命尚无本次逐条核对数据。'),
 ('chinese_sabre','鳡；候选定位 Elopichthys bambusa','淡水大型鱼','与同属鲤形目但偏植物或滤食的鱼相比，生活方式可能非常不同。','分类索引：不要用“都是鲤形目”推导相同食性或成长速度。'),
 ('redeye','赤眼鳟；候选定位 Squaliobarbus curriculus','淡水常见鱼','中文带“鳟”字，不表示它就是虹鳟、哲罗鲑那样的鲑科鱼。','分类索引：名称后缀不能决定温度需求与成长参数。'),
 ('longsnout_cat','Leiocassis longirostris','底栖捕食鱼','俗称江团、鮰鱼；中科院水生所研究将其作为典型肉食鱼讨论营养适应。','与黄颡鱼同样有须，也不能共享逐龄长度；本版不填未经核对的成长年表。'),
 ('wuchang_bream','Megalobrama amblycephala','鳊鲂与鲴类','常见名称武昌鱼对应团头鲂；在生物学档案里应与团头鲂互相关联。','项目中保留两个ID是内容设计选择，不应据此重复计为两个现实物种。'),
 ('cn_mahseer','光唇鱼类；Acrossocheilus 等需具体化','溪流与特殊鱼','光唇鱼这个简称可能不足以确定具体种；不能因英文ID含mahseer就套用大型南亚鱼。','类群索引：先明确中文全名、产地及学名，才有可用的尺寸和成长资料。'),
 ('rock_carp','岩原鲤；候选定位 Procypris rabaudi','溪流与特殊鱼','中文含“鲤”并不意味着只是普通鲤鱼换一种颜色。','分类索引：本版未核验当地群体成长曲线与寿命；不挪用普通鲤鱼数字。'),
 ('barbel','项目写“花鲴”；可能拟指花䱻 Hemibarbus maculatus','溪流与特殊鱼','鲴与䱻是不同的字；英文barbel也无法唯一锁定物种。','身份待确认：这是需要核对的命名问题，未确认前不建立貌似精确的物种档案。'),
 ('snow_trout','裂腹鱼类；Schizothorax 等','溪流与特殊鱼','裂腹鱼是包含多个对象的类群名称，常与高原水域鱼类研究一起出现。','类群索引：不能给所有裂腹鱼设一个统一的现实最大体长、鱼龄和温度范围。'),
 ('golden_line','金线鲃属 Sinocyclocheilus 等待具体到种','溪流与特殊鱼','属内有不同栖息与形态类型，不能把一种洞穴鱼的特点赋给全部金线鲃。','类群索引：需要具体种名；本版不以属级最大值当作每种鱼的上限。'),
 ('bronze_carp','铜鱼；候选定位 Coreius heterodon','溪流与特殊鱼','属于需要结合河流生态认识的鱼种；不应仅因名字含carp就复制鲤鱼设定。','分类索引：铜鱼与近似名称、近缘种需区分；未填未经核验的数值。'),
 ('black_porgy','黑鲷；候选定位 Acanthopagrus schlegelii','海水与河口鱼','与真鲷同属鲷类认识范围，但不是同种鱼换一个颜色。','分类索引：性别、成长和繁殖资料要匹配具体物种；本版未填逐龄数字。'),
 ('red_porgy','Pagrus major','海水与河口鱼','真鲷是侧扁而较高体的海鱼；食物包括底栖无脊椎动物等。','常见长度与最大记录口径不同，不能直接作为同一分布的两端。'),
 ('yellow_croaker','大黄鱼；候选定位 Larimichthys crocea','海水与河口鱼','不能与小黄鱼仅凭市场俗名混为同一个物种档案。','分类索引：自然种群与养殖群体应分别查证；未填成长与寿命年表。'),
 ('hairtail','带鱼类；以下例子仅为 Trichiurus lepturus','海水与河口鱼','带状细长体形使它很长却不一定很重；长度不等于体量。','带鱼涉及多个种与分类问题，例子的记录不能覆盖全部中国带鱼。'),
 ('chinese_mackerel','日本鲐 Scomber japonicus 作为候选定位','海水与河口鱼','鲐、鲅等相似俗名应分开；游泳型体形与伏底鱼不同。','数据库的常见长度为叉长FL，不能与全长TL无标记混用。'),
 ('flounder','Paralichthys olivaceus','海水与河口鱼','牙鲆是平扁体形的底栖鱼，左右不对称的成鱼外观来自发育变化。','养殖上市时间只说明特定条件下的生产周期，不是寿命或生理极限。'),
 ('sea_bass','花鲈；优先核对 Lateolabrax maculatus','海水与河口鱼','花鲈不是大口黑鲈；都被俗称鲈鱼，不表示共享同一生活史。','部分旧数据库链接跳向L. japonicus；本版不把该页数字直接当作花鲈数据。'),
 ('pomfret','银鲳；Pampus 类群需明确具体种','海水与河口鱼','银色、高而侧扁的体形有辨识度，但市场名可能混入相似对象。','分类索引：应核实学名与地区，不能用某个银鲳旧名下的数字覆盖全部。'),
 ('grouper','石斑鱼类群；具体示例 Epinephelus akaara','海水与河口鱼','石斑鱼不是一个单一物种，不同种之间的体型和生活史差异很大。','本版赤点石斑鱼的30/58厘米仅属于示例种，不是整个类群的常见值与上限。'),
 ('mullet','Mugil cephalus 作为定位','海水与河口鱼','可见于海岸、河口等多类盐度环境；完整生活史不能只按静水淡水塘解释。','数据库记录保留SL；类群与区域鉴定仍应留意。'),
 ('rainbow_trout','Oncorhynchus mykiss','冷水与长寿鱼','有淡水定居和降海生活型；同一物种也可能有不同成长经历。','“冷水鱼”不等于任何低温都长得好，也不代表温度越低越好。'),
 ('lenok','细鳞鲑；Brachymystax 类群需确认','冷水与长寿鱼','可先按冷水鲑类理解，与热带罗非鱼的环境需求明显不同。','类群索引：分类与不同种群需核实，不能用哲罗鲑或虹鳟的曲线代替。'),
 ('taimen','Hucho taimen','冷水与长寿鱼','大型鲑科鱼，有较长生活史；能够形成跨多年的个体经历。','本版研究来自贝加尔湖流域，不能自动当成所有地区群体的平均寿命。'),
 ('whitefish','雅罗鱼；例为瓦氏雅罗鱼 Leuciscus waleckii','冷水与长寿鱼','项目英文ID为whitefish，但雅罗鱼不能直接套用鲑科白鲑资料。','中文雅罗鱼需具体化；本版成长研究只针对瓦氏雅罗鱼的一个水域群体。'),
 ('chinese_sturgeon','Acipenser sinensis','鲟类与洄游鱼','长寿、大型、溯河繁殖的代表；现实成长与成熟按多年尺度理解。','不要把海洋育肥与江河繁殖阶段压成静水鱼塘里的单一生活方式。'),
 ('dabry_sturgeon','达氏鲟；候选定位 Acipenser dabryanus','鲟类与洄游鱼','与中华鲟是不同物种；不能因为都叫鲟就复制中华鲟的全部生活史。','分类索引：本次未核验该物种专属的逐龄成长、最大体长和寿命数字。'),
 ('eel','鳗鲡属 Anguilla；例为日本鳗鲡 A. japonica','底栖与鳗形鱼','鳗鲡的海洋繁殖和幼体发育有独特阶段，与黄鳝不同。','项目需确定是日本鳗鲡还是其他鳗鲡；不能把黄鳝资料混入。'),
 ('ricefield_eel','Monopterus albus 类群','底栖与鳗形鱼','黄鳝虽像鳗鲡，但不是鳗鲡属；研究记录有先雌后雄的自然性反转。','不能用“鳝”和“鳗”俗称相近推导同一种生活史；群体和分类也需注意。'),
 ('koi','鲤的观赏选育品种，Cyprinus carpio 相关','观赏与特殊条目','色彩与花纹是主要辨识维度；观赏价值不是独立的生物学体型等级。','与鲤鱼建立品种关联；本版不为“锦鲤”虚构一条独立物种成长曲线。'),
 ('giant_black_carp','大型青鱼个体，Mylopharyngodon piceus','观赏与特殊条目','巨青适合表达“大个体”的收获，但名字本身不产生一个新物种。','与青鱼关联；个体大可以来自年龄、遗传、营养和存活经历。'),
]

# 明确本次核对范围；缺失的数值保持缺失。
DETAIL = {
 'crucian': ('体型例子：C. auratus 数据库常见长度10厘米TL，最大记录48厘米TL。','这不等于中国所有鲫鱼的成年分布；本版不据此设定平均寿命或逐龄成长。',['crucian']),
 'carp': ('温暖地区混养实例：一个养殖季可达0.6～1.0公斤；温带到1～2公斤可需2～4个养殖季。','温度与养殖条件能明显改变时间尺度；这些是体重，不是长度。',['carp']),
 'grass_carp': ('珠江群体研究样本体长103～665毫米，涉及0+～5+龄；文献拟合见第5章。','同一物种的一个水域群体模型，并非每条草鱼的保证长度；4厘米应理解为幼鱼规格。',['grasspaper','grass']),
 'silver_carp': ('滤食型鱼；FAO繁育实例选择4～6岁、体重超过2.5公斤的亲鱼。','这是繁育选用规格，不是首次成熟年龄，也不是所有成鱼的常见体重。',['silver']),
 'bighead_carp': ('中国育成实例：8～10个月达到约750～1500克的上市规格。','起点是育成投放阶段，不能解读成从孵化到最大尺寸；自然繁殖还涉及流水条件。',['bighead']),
 'black_carp': ('加拿大DFO概述记载体长可达约1.8米。','这是资料所列可达长度，不是该塘每条青鱼的成长目标；大型个体需要合适食物与存活机会。',['black']),
 'tilapia': ('尼罗罗非鱼实例：雄鱼育成5～8个月可达400～500克；池塘成熟年龄可约5～6个月。','成长、成熟是不同终点；过度繁殖与竞争可能造成小型化。',['tilapia']),
 'loach': ('USGS物种资料概述的常见体型约10～20厘米，最大长度记录28厘米SL。','常见范围与最大记录采用的口径需分别理解，不应把28厘米当作每条泥鳅最终都会到的值。',['loach']),
 'bream': ('若定位为P. pekinensis，FishBase列最大记录55厘米TL。','单一最大记录不能生成完整年龄曲线；中文俗名范围仍要先确定。',['bream']),
 'white_amur_bream': ('2025年长江与黑龙江养殖群体比较研究使用Parabramis pekinensis。','这是物种定位与不同群体研究的入口，本版不将体成分实验当作成长曲线。',['changchun']),
 'mandarin': ('初孵约4毫米；FAO年级描述为一年约400～600克、二年约1～1.5公斤。','毫米级出生、厘米级鱼种、公斤级成鱼是不同阶段；避免把4厘米写成现实孵化体长。',['mandarin']),
 'black_bass': ('USFWS传统物种资料给出常见长度16英寸，约40.6厘米。','这里只用于认识体型量级；因分类调整，不把旧资料中的极端纪录直接绑定项目的养殖群体。',['bass']),
 'snakehead': ('FishBase列最大记录100厘米TL，成熟描述约2岁、30厘米。','这些是文献记录或概述，不能推导全部群体到2岁都恰好30厘米。',['snakehead']),
 'catfish': ('以S. asotus为例：FishBase常见长度37厘米SL，最大记录130厘米TL。','这两个数字测量终点不同，不能直接算“常见到最大”的倍数。',['catfish']),
 'yellow_catfish': ('FishBase列最大记录34.5厘米TL，并描述雄性护卵及护幼行为。','本版不将数据库的模型估算世代时间转换成“几天长满”。',['yellowcat']),
 'longsnout_cat': ('中科院水生所研究将长吻鮠描述为高蛋白需求、糖利用能力较低的肉食鱼。','食物适配有种间差异；本次资料未提供可通用的逐龄长度表。',['longcat']),
 'bluntnose_bream': ('NCBI中M. amblycephala同时关联Wuchang bream与Bluntsnout bream名称。','已核对名称关系；不提供未经核对的极端长度和寿命。',['wuchang']),
 'wuchang_bream': ('与团头鲂通常指同一物种，见对应档案。','可以保留内容条目，但现实生物学档案应共用物种身份。',['wuchang']),
 'red_porgy': ('FishBase常见长度30厘米TL，最大记录100厘米SL；最大报告年龄26年。','口径不同、记录含义不同；26年不是野生个体的平均预期寿命。',['redporgy']),
 'hairtail': ('T. lepturus数据库常见长度100厘米TL，最大记录234厘米TL。','只代表该具体物种；带状外形说明“长”不一定意味着同等重量的大块头。',['hairtail']),
 'chinese_mackerel': ('以S. japonicus为例：常见长度30厘米FL，最大记录64厘米TL。','保留叉长与全长标记；不要把不同鲐种或旧分布资料混为一谈。',['mackerel']),
 'flounder': ('2025年养殖团队会议摘要概述：可在12～18个月达到0.8～1.2公斤上市规格。','这是会议摘要中的生产量级说明，证据细节少于完整生长论文，不作为野生成长曲线。',['flounder']),
 'grouper': ('仅以赤点石斑鱼E. akaara为例：常见长度30厘米TL，最大记录58厘米TL。','不同石斑鱼种可能相差很大；不能把这一示例套给项目尚未明确的整个石斑条目。',['grouper']),
 'mullet': ('M. cephalus数据库常见长度50厘米SL，最大记录100厘米SL，最大报告年龄16年。','最大年龄不是平均寿命；SL也不是含完整尾鳍的全长。',['mullet']),
 'rainbow_trout': ('FAO育成实例：到30～40厘米上市规格通常约9个月；更大规格可更久。','这是人工条件下的育成阶段；淡水与降海生活型的成长也可能不同。',['trout']),
 'taimen': ('贝加尔湖流域研究记载可达29岁、60公斤。','年龄与体重是该研究汇总的可达量级，不是同一条29岁个体必然重60公斤，也不是平均值。',['taimen']),
 'whitefish': ('以汤旺河瓦氏雅罗鱼为例：L∞约35.175厘米、k=0.26/年；第5章有重算曲线。','L∞是拟合参数，不是这类鱼的绝对硬上限；雅罗鱼条目仍需确认具体种。',['ide']),
 'chinese_sturgeon': ('NOAA概述：可达16英尺，约4.88米；寿命至少35年，雄雌成熟约8/14年。','可达长度不代表常态；它是溯河繁殖鱼，不是终生静水塘鱼。',['sturgeon']),
 'eel': ('FAO日本鳗鲡资料明确描述个体成长差异显著，需定期分级。','这支持“同种鱼有成长差异”，但不能据此推导统一±15%的现实区间。',['eel']),
 'ricefield_eel': ('中科院水生所研究介绍黄鳝具有先雌后雄的自然性反转特性。','不是所有鳗形鱼都如此；本版不混用鳗鲡寿命与成长参数。',['yelloweel']),
}
INDEX_SOURCES = {'silver_bream':['silverid'], 'golden_line':['golden'], 'koi':['carp'], 'giant_black_carp':['black']}

cards=[]
for sid,identity,group,intro,note in ROWS:
    f=next(f for f in species if f['speciesId']==sid)
    detail=DETAIL.get(sid)
    cards.append(dict(id=sid,name=f['name'],identity=identity,group=group,intro=intro,note=note,
        status='重点档案' if detail else '分类索引',
        fact=detail[0] if detail else '本版未提供经逐条核对的成长或寿命数值。',
        limit=detail[1] if detail else '此项用于分类检索；具体到种、地区和群体后再补充生物学参数。',
        sources=[dict(label=S[k][0],url=S[k][1]) for k in (detail[2] if detail else INDEX_SOURCES.get(sid,[]))],
        projectRange=[f['typicalMinM'],f['typicalMaxM']]))
assert len(cards)==50 and len({x['id'] for x in cards})==50
assert {x['id'] for x in cards}=={x['speciesId'] for x in species}
(OUT/'鱼种索引.json').write_text(json.dumps(cards,ensure_ascii=False,indent=2))

font=FontProperties(fname='/System/Library/Fonts/Hiragino Sans GB.ttc')
plt.rcParams.update({'font.family':font.get_name(),'axes.unicode_minus':False,'font.size':11,
    'figure.facecolor':'#f7f7f0','axes.facecolor':'#f7f7f0','text.color':'#253d37',
    'axes.labelcolor':'#253d37','xtick.color':'#52635d','ytick.color':'#52635d',
    'axes.spines.top':False,'axes.spines.right':False})
models=[('草鱼 · 珠江中下游',118.7,.128,.282,'#237e70'),('瓦氏雅罗鱼 · 汤旺河下游',35.175,.26,.5448,'#b77932')]
age=np.linspace(1,5,200)
fig,axes=plt.subplots(1,2,figsize=(13,5.5))
for label,cap,k,shift,color in models:
    length=cap*(1-np.exp(-k*(age+shift)))
    axes[0].plot(age,length,label=label,lw=3,color=color)
    axes[1].plot(age,length/cap*100,label=label,lw=3,color=color)
for ax in axes:
    ax.set_xlabel('模型年龄（年）');ax.set_xticks(range(1,6));ax.grid(alpha=.16);ax.legend(fontsize=9,loc='upper left')
axes[0].set(title='绝对体长：谁更大？',ylabel='文献模型体长（厘米）',ylim=(0,70))
axes[1].set(title='相对体长：接近各自 L∞ 的多少？',ylabel='文献模型体长 / L∞（%）',ylim=(0,100))
fig.suptitle('两个水域群体的文献拟合曲线 · 非同条件实验 · 非游戏参数',fontsize=15,x=.5,y=.98)
fig.text(.055,.025,'来源：珠江草鱼研究（2021）；汤旺河瓦氏雅罗鱼研究（2019）。按原公式重算，无置信区间；不外推到出生时。',fontsize=9,color='#53665d')
fig.tight_layout(rect=(0,.065,1,.93));fig.savefig(OUT/'growth-populations.png',dpi=170);plt.close(fig)

fig,ax=plt.subplots(figsize=(12,5.2))
culture=[('草鱼 · 鱼种培育 → 13～15cm或更大鱼种',4,6,'#44887a'),('尼罗罗非鱼 · 雄鱼育成 → 400～500g',5,8,'#5186b2'),('鳙鱼 · 育成 → 750～1500g',8,10,'#b58a3b'),('虹鳟 · 育成 → 30～40cm（通常值）',9,9,'#8b73a4'),('牙鲆 · 养殖摘要 → 0.8～1.2kg',12,18,'#bb7461')]
for y,(label,lo,hi,color) in enumerate(culture):
    if lo!=hi: ax.plot([lo,hi],[y,y],lw=12,solid_capstyle='round',color=color)
    else: ax.scatter([lo],[y],s=120,color=color,zorder=3)
    ax.text(hi+.5,y,f'{lo}～{hi}个月' if lo!=hi else '约9个月',va='center',fontsize=10)
ax.set_yticks(range(len(culture)),[r[0] for r in culture]);ax.invert_yaxis()
ax.set(xlim=(0,22),xlabel='文献所述阶段的用时（月）',title='不同生产阶段的时间量级 · 起终点不同，不能据此排快慢')
ax.grid(axis='x',alpha=.15)
fig.text(.035,.035,'来源：FAO物种养殖资料；牙鲆为2025年养殖团队会议摘要。线段表示文献周期范围，不是逐月长度曲线。',fontsize=9)
fig.tight_layout(rect=(0,.08,1,1));fig.savefig(OUT/'culture-times.png',dpi=170);plt.close(fig)

table='| 模型年龄 | 草鱼体长（厘米） | 瓦氏雅罗鱼体长（厘米） |\n| --- | --- | --- |\n'
calc=[]
for year in range(1,6):
    vals=[cap*(1-math.exp(-k*(year+shift))) for _,cap,k,shift,_ in models]
    table+=f'| {year}年 | {vals[0]:.1f} | {vals[1]:.1f} |\n'
    calc.append(dict(ageYears=year,grassLengthCm=vals[0],ideLengthCm=vals[1]))
(OUT/'文献曲线重算.json').write_text(json.dumps(dict(type='published_population_model_recalculation',note='模型预测，不是逐龄原始观测；不是游戏参数',models=[dict(name=label,LinfCm=cap,kPerYear=k,ageShiftYears=shift) for label,cap,k,shift,_ in models],values=calc),ensure_ascii=False,indent=2))

blocks=[]
for c in cards:
    links='；'.join(f'[{s["label"]}]({s["url"]})' for s in c['sources']) or '分类检索待补：本版不以候选学名宣称数值已核验。'
    blocks.append(f'''### {c['name']} · {c['status']}

- 分类定位：{c['identity']}；生态阅读组：{c['group']}。
- 认识重点：{c['intro']}
- 已核对资料 / 数值范围：{c['fact']}
- 如何理解：{c['limit']}
- 命名注意：{c['note']}
- 来源：{links}
''')
raw=(OUT/'阅读正文.md').read_text()
book=raw.replace('{{GROWTH_TABLE}}',table).replace('{{SPECIES_CARDS}}','\n'.join(blocks))
(OUT/'鱼类百科全书.md').write_text(book)
# HTML单独渲染卡片以支持快速检索；Markdown交付保留完整50条目。
html_md=raw.replace('{{GROWTH_TABLE}}',table).replace('{{SPECIES_CARDS}}','<div id="fish-directory"></div>')
(OUT/'页面正文.md').write_text(html_md)
print(json.dumps({'species':len(cards),'detailed':sum(c['status']=='重点档案' for c in cards),'markdown_chars':len(book),'files':['鱼类百科全书.md','鱼种索引.json','文献曲线重算.json','growth-populations.png','culture-times.png']},ensure_ascii=False))
