"""Prefab hierarchy regressions; run with the text-catalog Python environment."""
import importlib.util
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

MODULE_PATH=Path(__file__).resolve().parents[1]/'export-runtime.py'
SPEC=importlib.util.spec_from_file_location('runtime_export',MODULE_PATH)
EXPORT=importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(EXPORT)


def ref(oid,guid=None):
    return dict(fileID=oid,**({'guid':guid} if guid else {}))


def stripped(kind,source,instance,guid):
    return kind,dict(m_CorrespondingSourceObject=ref(source,guid),m_PrefabInstance=ref(instance))


def instance(guid,parent,renames):
    return 'PrefabInstance',dict(m_SourcePrefab=ref(100100000,guid),m_Modification=dict(
        m_TransformParent=ref(parent),m_Modifications=[dict(target=ref(oid,guid),propertyPath='m_Name',value=name)
                                                      for oid,name in renames.items()]))


class PrefabBindingTests(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory()
        self.paths={}
        self.guid_patch=patch.object(EXPORT,'guid_paths',lambda:self.paths)
        self.guid_patch.start()
        EXPORT.prefab.cache_clear()
        self.add('base',{
            1:('GameObject',dict(m_Name='Base')),
            2:('RectTransform',dict(m_GameObject=ref(1),m_Father=ref(0))),
            3:('GameObject',dict(m_Name='Label')),
            4:('RectTransform',dict(m_GameObject=ref(3),m_Father=ref(2))),
            5:('MonoBehaviour',dict(m_GameObject=ref(3),m_Text='First text')),
            6:('GameObject',dict(m_Name='Label')),
            7:('RectTransform',dict(m_GameObject=ref(6),m_Father=ref(2))),
            8:('MonoBehaviour',dict(m_GameObject=ref(6),m_Text='Second text')),
        })
        self.add('variant',{
            100:instance('base',0,{1:'Variant',3:'Middle',6:'Other'}),
            201:stripped('GameObject',1,100,'base'),
            202:stripped('RectTransform',2,100,'base'),
            203:stripped('GameObject',3,100,'base'),
            204:stripped('RectTransform',4,100,'base'),
            205:stripped('MonoBehaviour',5,100,'base'),
        })
        self.add('host',{
            401:('GameObject',dict(m_Name='Host')),
            402:('RectTransform',dict(m_GameObject=ref(401),m_Father=ref(0))),
            500:instance('variant',402,{201:'Nested',203:'TranslatedTarget'}),
            600:stripped('MonoBehaviour',205,500,'variant'),
            601:stripped('GameObject',203,500,'variant'),
            602:stripped('RectTransform',204,500,'variant'),
            603:('GameObject',dict(m_Name='AddedChild')),
            604:('RectTransform',dict(m_GameObject=ref(603),m_Father=ref(602))),
        })

    def tearDown(self):
        EXPORT.prefab.cache_clear()
        self.guid_patch.stop()
        self.temp.cleanup()

    def add(self,guid,objects):
        path=Path(self.temp.name)/(guid+'.prefab')
        path.write_text('%YAML 1.1\n'+''.join(
            '--- !u!1 &'+str(oid)+'\n'+EXPORT.yaml.safe_dump({kind:data},sort_keys=False)
            for oid,(kind,data) in objects.items()))
        Path(str(path)+'.meta').write_text('guid: '+guid+'\n')
        self.paths[guid]=path
        return EXPORT.prefab(path)

    def get(self,guid):
        return EXPORT.prefab(self.paths[guid])

    def test_stripped_variant_root_and_inherited_component(self):
        variant=self.get('variant')
        self.assertEqual('',variant.node_path('202'))
        self.assertEqual('Middle',variant.node_path('205'))
        # References using the original asset GUID must still inherit renames.
        self.assertEqual('Middle','/'.join(n for n,_ in variant.source_chain(ref(5,'base'))[1:]))

    def test_nested_variant_uses_ids_for_duplicate_names_and_local_aliases(self):
        host=self.get('host')
        self.assertEqual('Nested/TranslatedTarget',host.node_path('600'))
        self.assertEqual('Nested/TranslatedTarget',host.node_path('601'))
        self.assertEqual('Nested/TranslatedTarget/AddedChild',host.node_path('604'))
        self.assertEqual('Nested/Other','/'.join(n for n,_ in host.instance_chain('500',ref(8,'base'))[1:]))

    def test_modification_binding_targets_intermediate_variant_identity(self):
        host=self.get('host')
        # document[2] is the nested PrefabInstance, modification[1] targets 203.
        self.assertEqual('Nested/TranslatedTarget',host.binding_path(dict(
            location='document[2]/PrefabInstance/m_Modification/m_Modifications/1/value')))

    def test_second_variant_layer_preserves_all_source_identities(self):
        final=self.add('final',{
            700:instance('variant',0,{201:'FinalRoot',203:'FinalLabel'}),
            701:stripped('RectTransform',202,700,'variant'),
            702:stripped('MonoBehaviour',205,700,'variant'),
        })
        self.assertEqual('',final.node_path('701'))
        self.assertEqual('FinalLabel',final.node_path('702'))
        self.assertEqual('FinalLabel','/'.join(n for n,_ in final.source_chain(ref(5,'base'))[1:]))

    def test_omitted_inherited_gameobject_can_be_renamed_by_outer_prefab(self):
        self.add('header',{
            900:instance('base',0,{1:'CozyPageHeader'}),
            902:stripped('RectTransform',2,900,'base'),
            903:('GameObject',dict(m_Name='Title')),
            904:('RectTransform',dict(m_GameObject=ref(903),m_Father=ref(902))),
            905:('MonoBehaviour',dict(m_GameObject=ref(903),m_Text='Header title')),
        })
        header_go=1^900
        host=self.add('header_host',{
            1001:('GameObject',dict(m_Name='Panel')),
            1002:('RectTransform',dict(m_GameObject=ref(1001),m_Father=ref(0))),
            1100:instance('header',1002,{header_go:'DesignHeader'}),
            1101:stripped('MonoBehaviour',905,1100,'header'),
        })
        self.assertEqual('DesignHeader/Title',host.node_path('1101'))
        self.assertEqual('CozyPageHeader',self.get('header').source_chain(ref(header_go,'header'))[0][0])


if __name__=='__main__':
    unittest.main()
