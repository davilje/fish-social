"""Explicit localization calls retain reviewed keys and argument provenance."""
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

MODULE=Path(__file__).resolve().parents[1]/'extract.py'
SPEC=importlib.util.spec_from_file_location('catalog_extract',MODULE)
EXTRACT=importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(EXTRACT)


class LocalizedExtractionTests(unittest.TestCase):
    def setUp(self):
        EXTRACT.entries.clear()
        EXTRACT.localized_prototypes.clear()
        EXTRACT.reviewed_live_sources.clear()
        EXTRACT.skipped.clear()
        EXTRACT.excluded_debug.clear()
        self.temp=tempfile.TemporaryDirectory()

    def tearDown(self):
        self.temp.cleanup()

    def prototype(self,key,source,kind='代码常量'):
        internal='txt_'+str(len(EXTRACT.localized_prototypes)).zfill(16)
        EXTRACT.localized_prototypes[key]=dict(key=internal,module='通用界面',category='运行时界面文本',
            sourceText=source,zhCN=source,enUS='Edited English',note='Reviewed note',
            placeholders=EXTRACT.PLACEHOLDER.findall(source),sources=[dict(file='old.cs',line=1,column=1,
                location='OldMethod',kind=kind)])
        return internal

    def scan(self,body):
        path=Path(self.temp.name)/'View.cs'
        path.write_text('class View { void Draw() { '+body+' } }')
        EXTRACT.code_file(path,Path('desktop-overlay/View.cs'))

    def test_text_fallback_and_key_are_not_extracted_again(self):
        key=self.prototype('ui.test.title','保留原文')
        self.scan('var label=OverlayLocalization.Text("ui.test.title", "新的兜底文本");')
        self.assertEqual([key],list(EXTRACT.entries))
        result=EXTRACT.entries[key]
        self.assertEqual('保留原文',result['sourceText'])
        self.assertEqual('Edited English',result['enUS'])
        self.assertEqual('Reviewed note',result['note'])
        source=result['sources'][0]
        self.assertEqual(('desktop-overlay/View.cs','Draw','代码常量'),(source['file'],source['location'],source['kind']))
        self.assertIn('新的兜底文本',source['expression'])

    def test_nested_format_retains_independent_key_and_actual_arguments(self):
        key=self.prototype('ui.test.message','鱼获 {0}：{1:N0} 条','拼接模板')
        name=self.prototype('game.test.name','鲤鱼')
        self.scan('var message=DesktopLocalization.Format("ui.test.message", (object)(DesktopLocalization.Text("game.test.name", "鲤鱼")), (object)(count));')
        self.assertEqual({key,name},set(EXTRACT.entries))
        source=EXTRACT.entries[key]['sources'][0]
        self.assertEqual('拼接模板',source['kind'])
        self.assertEqual([0,1],[a['index'] for a in source['variables']])
        self.assertEqual('(object)(count)',source['variables'][1]['expression'])
        self.assertEqual('鱼获 {0}：{1:N0} 条',EXTRACT.entries[key]['sourceText'])

    def test_format_date_keeps_original_format_key(self):
        key=self.prototype('ui.test.date_format','yyyy年MM月dd日')
        self.scan('var dateText=OverlayLocalization.FormatDate("ui.test.date_format", date);')
        self.assertEqual([key],list(EXTRACT.entries))
        self.assertEqual([dict(index=0,expression='date')],EXTRACT.entries[key]['sources'][0]['variables'])

    def test_unknown_literal_key_fails_before_silently_losing_copy(self):
        with self.assertRaisesRegex(ValueError,'未登记'):
            self.scan('var label=OverlayLocalization.Text("ui.test.missing", "待登记文案");')

    def test_concat_migration_keeps_original_template_identity(self):
        self.scan('var label="数量：" + count + " 条";')
        original=list(EXTRACT.entries.values())[0]
        EXTRACT.entries.clear()
        self.scan('var label=DesktopLocalization.Concat(DesktopLocalization.Concat("数量：", count), " 条");')
        migrated=list(EXTRACT.entries.values())[0]
        self.assertEqual((original['key'],original['sourceText']),(migrated['key'],migrated['sourceText']))
        self.assertEqual(original['sources'][0]['variables'],migrated['sources'][0]['variables'])
        EXTRACT.entries.clear()
        self.scan('var label=(("数量：" + count) + " 条");')
        parenthesized=list(EXTRACT.entries.values())[0]
        self.assertEqual(original['sourceText'],parenthesized['sourceText'])

    def test_variadic_concat_preserves_every_operand_and_template_identity(self):
        self.scan('var label="数量：" + count + " 条；等级：" + level;')
        original=list(EXTRACT.entries.values())[0]
        EXTRACT.entries.clear()
        self.scan('var label=DesktopLocalization.Concat("数量：", count, " 条；等级：", level);')
        migrated=list(EXTRACT.entries.values())[0]
        self.assertEqual((original['key'],original['sourceText']),(migrated['key'],migrated['sourceText']))
        self.assertEqual('数量：{0} 条；等级：{1}',migrated['sourceText'])
        self.assertEqual(original['sources'][0]['variables'],migrated['sources'][0]['variables'])

    def test_variadic_concat_preserves_numeric_addition_as_one_argument(self):
        self.scan('var label=DesktopLocalization.Concat(count + bonus, " 条 / ", (level + 1), " 级");')
        entry=list(EXTRACT.entries.values())[0]
        self.assertEqual('{0} 条 / {1} 级',entry['sourceText'])
        self.assertEqual(['count + bonus','(level + 1)'],[v['expression'] for v in entry['sources'][0]['variables']])
        EXTRACT.entries.clear()
        self.scan('var label=count + bonus + " 条 / " + (level + 1) + " 级";')
        original=list(EXTRACT.entries.values())[0]
        self.assertEqual(entry['key'],original['key'])
        self.assertEqual(entry['sources'][0]['variables'],original['sources'][0]['variables'])

    def test_variadic_assignment_keeps_reviewed_append_identity(self):
        self.scan('label += "新增：" + count + " 条";')
        original=list(EXTRACT.entries.values())[0]
        identity=(original['key'],'desktop-overlay/View.cs','拼接模板','Draw')
        EXTRACT.entries.clear()
        with patch.object(EXTRACT,'reviewed_expression_hashes',return_value={identity:{'b'*64}}):
            self.scan('label = DesktopLocalization.Concat(label, "新增：", count, " 条");')
        migrated=list(EXTRACT.entries.values())[0]
        self.assertEqual(original['key'],migrated['key'])
        self.assertEqual('新增：{0} 条',migrated['sourceText'])
        self.assertEqual(original['sources'][0]['variables'],migrated['sources'][0]['variables'])
        EXTRACT.entries.clear()
        self.scan('label = DesktopLocalization.Concat("短标签");')
        self.assertEqual(['短标签'],[e['sourceText'] for e in EXTRACT.entries.values()])

    def test_nearby_localization_does_not_turn_protocol_id_into_copy(self):
        key=self.prototype('ui.test.preset','标准')
        self.scan('var preset=new Preset("960x560", OverlayLocalization.Text("ui.test.preset", "标准"));')
        self.assertEqual([key],list(EXTRACT.entries))
        self.scan('if (content == "取消") Play("ui_close");')
        self.assertEqual([key],list(EXTRACT.entries))

    def test_hierarchy_identity_fields_are_excluded_without_hiding_labels(self):
        path=Path(self.temp.name)/'View.cs'
        path.write_text('class View { static readonly string[] TabNodeNames = { "Tab_资料", "Tab_展示柜", "Tab_图鉴", "Tab_相册", "Tab_成就" }; '
                        'static readonly string[] TabLabels = { "资料", "展示柜" }; '
                        'void Draw() { var nodePath="Shell/Tab_资料"; Label("Tab_资料"); } }')
        EXTRACT.code_file(path,Path('desktop-overlay/View.cs'))
        self.assertEqual({'资料','展示柜','Tab_资料'},{e['sourceText'] for e in EXTRACT.entries.values()})
        entry=next(e for e in EXTRACT.entries.values() if e['sourceText']=='Tab_资料')
        self.assertEqual(['Draw'],[source['location'] for source in entry['sources']])
        self.assertEqual(6,EXTRACT.skipped['层级节点标识'])

    def test_deep_concat_inside_log_is_excluded(self):
        expression='"日志："'
        for _ in range(12):expression='DesktopLocalization.Concat('+expression+', count)'
        self.scan('Debug.Log('+expression+');')
        self.assertFalse(EXTRACT.entries)
        self.scan('LatencyTrace.Write(DesktopLocalization.Concat("诊断：", condition ? string.Empty : DesktopLocalization.Concat("事件：", action)));')
        self.assertFalse(EXTRACT.entries)

    def test_concat_preserves_review_identity_without_rewriting_actual_expression(self):
        from inactive_filter import source_signature
        self.scan('var label="数量：" + count + " 条";')
        original=list(EXTRACT.entries.values())[0]
        identity=(original['key'],'desktop-overlay/View.cs','拼接模板','Draw')
        reviewed_hash='a'*64
        EXTRACT.entries.clear()
        with patch.object(EXTRACT,'reviewed_expression_hashes',return_value={identity:{reviewed_hash}}):
            self.scan('var label=DesktopLocalization.Concat(DesktopLocalization.Concat("数量：", count), " 条");')
        migrated=list(EXTRACT.entries.values())[0]
        source=migrated['sources'][0]
        self.assertIn('DesktopLocalization.Concat',source['expression'])
        self.assertEqual(reviewed_hash,source_signature(migrated['key'],source)[-1])

    def test_art_metadata_is_excluded_without_hiding_normal_display_fields(self):
        path=Path(self.temp.name)/'layout.json'
        path.write_text(json.dumps(dict(animationStatus='动画制作状态',title='玩家入口')))
        EXTRACT.json_file(path,Path('desktop-overlay/OverlayResources/ponds/layered/scene/layout.json'))
        self.assertEqual(['玩家入口'],[e['sourceText'] for e in EXTRACT.entries.values()])
        EXTRACT.entries.clear()
        path=Path(self.temp.name)/'motion.json'
        path.write_text(json.dumps(dict(weather=dict(label='原图静态云'),title='天气选项')))
        EXTRACT.json_file(path,Path('fish-social-unity/Assets/Desktop/OverlayArt/Ponds/layered/scene/motion.json'))
        self.assertEqual(['天气选项'],[e['sourceText'] for e in EXTRACT.entries.values()])


if __name__=='__main__':
    unittest.main()
