"""Export the reviewed bilingual table and explicit prefab bindings for players."""
from pathlib import Path
from functools import lru_cache
import json,re,sys
import yaml

ROOT=Path(__file__).resolve().parents[2]
CATALOG=ROOT/'unity-overlay文本表.json'

def write_json(path,value):
    path.parent.mkdir(parents=True,exist_ok=True)
    path.write_text(json.dumps(value,ensure_ascii=False,separators=(',',':'))+'\n',encoding='utf-8')

@lru_cache(None)
def guid_paths():
    return {re.search(r'^guid: (\w+)',p.read_text(),re.M)[1]:p.with_suffix('')
            for p in (ROOT/'fish-social-unity/Assets').rglob('*.prefab.meta')}

def file_id(ref):return str((ref or {}).get('fileID',0))

class Prefab:
    def __init__(self,path):
        self.path=path
        self.guid=re.search(r'^guid: (\w+)',Path(str(path)+'.meta').read_text(),re.M)[1]
        raw=path.read_text(encoding='utf-8-sig')
        ids=re.findall(r'^--- !u!\d+ &(-?\d+).*$',raw,re.M)
        cleaned=re.sub(r'^%.*$','',raw,flags=re.M)
        cleaned=re.sub(r'^--- !u!\d+ &(-?\d+).*$','---',cleaned,flags=re.M)
        self.docs=list(yaml.safe_load_all(cleaned))
        self.objects={oid:next(iter(doc.items()))for oid,doc in zip(ids,self.docs)if doc}
        self.names={oid:d['m_Name']for oid,(kind,d)in self.objects.items()if kind=='GameObject' and 'm_Name'in d}
        self.transforms={file_id(d.get('m_GameObject')):oid for oid,(kind,d)in self.objects.items()if kind in {'Transform','RectTransform'}and'm_GameObject'in d}
        self.memo={}
        self.instance_aliases={}
        for oid,(kind,data) in self.objects.items():
            instance=file_id(data.get('m_PrefabInstance'))
            if kind=='GameObject' and instance!='0':
                source=data['m_CorrespondingSourceObject']
                self.instance_aliases.setdefault(instance,[]).append(
                    ((source['guid'],file_id(source)),(self.guid,oid)))

    def source_chain(self,reference):
        """Resolve an asset reference, including references inherited by a variant."""
        guid=reference.get('guid',self.guid)
        oid=file_id(reference)
        if guid==self.guid and oid in self.objects:
            return self.node_chain(oid)
        if guid==self.guid:
            # Unity omits unmodified inherited objects from YAML. Their local
            # IDs are the source ID XOR the containing PrefabInstance ID; a
            # later prefab can still override such an omitted GameObject.
            candidates=[]
            for instance,(kind,data) in self.objects.items():
                if kind!='PrefabInstance':continue
                base_guid=data['m_SourcePrefab']['guid']
                original=str(int(oid)^int(instance))
                try:chain=self.instance_chain(instance,{'guid':base_guid,'fileID':original})
                except KeyError:continue
                candidates.append(chain)
            if len(candidates)==1:return candidates[0]
            raise KeyError(f'Implicit asset reference {guid}:{oid} has {len(candidates)} resolutions in {self.path}')
        # A variant's inherited objects can still be addressed in the base asset.
        # Follow its root instance instead of guessing from names or matching IDs
        # belonging to a different asset.
        candidates=[]
        for instance,(kind,data) in self.objects.items():
            if kind!='PrefabInstance' or file_id(data['m_Modification'].get('m_TransformParent'))!='0':continue
            try:
                chain=self.instance_chain(instance,reference)
            except KeyError:
                continue
            candidates.append(chain)
        if len(candidates)!=1:
            raise KeyError(f'Asset reference {guid}:{oid} has {len(candidates)} resolutions in {self.path}')
        return candidates[0]

    def instance_chain(self,oid,reference):
        data=self.objects[oid][1]
        base=prefab(guid_paths()[data['m_SourcePrefab']['guid']])
        chain=base.source_chain(reference)
        mods=data['m_Modification']
        renames=[((m['target'].get('guid',base.guid),file_id(m['target'])),m['value'])
                 for m in mods.get('m_Modifications',[]) if m['propertyPath']=='m_Name']
        result=[]
        for name,aliases in chain:
            # Preserve identities through every inheritance layer. Overrides can
            # target a stripped GameObject in an intermediate variant, whose
            # local file ID need not equal the original object's file ID.
            local=frozenset(local for source,local in self.instance_aliases.get(oid,[]) if source in aliases)
            if not local:
                local=frozenset((self.guid,str(int(source)^int(oid))) for guid,source in aliases if guid==base.guid)
            aliases=aliases|local
            for target,value in renames:
                if target in aliases:name=value
            result.append((name,aliases))
        parent=file_id(mods.get('m_TransformParent'))
        return self.node_chain(parent)+tuple(result)

    def node_chain(self,oid):
        """Return immutable (name, GameObject identities) nodes, including root."""
        oid=str(oid)
        if oid=='0':return ()
        if oid in self.memo:return self.memo[oid]
        kind,data=self.objects[oid]
        instance=file_id(data.get('m_PrefabInstance'))
        if instance!='0':
            result=self.instance_chain(instance,data['m_CorrespondingSourceObject'])
            if kind=='GameObject':
                name,aliases=result[-1]
                result=result[:-1]+((name,aliases|frozenset({(self.guid,oid)})),)
        elif kind=='GameObject':
            result=self.node_chain(self.transforms[oid])
        elif kind in {'Transform','RectTransform'}:
            parent=file_id(data.get('m_Father'))
            go=file_id(data['m_GameObject'])
            result=self.node_chain(parent)+((self.names[go],frozenset({(self.guid,go)})),)
        else:
            result=self.node_chain(file_id(data['m_GameObject']))
        self.memo[oid]=result
        return result

    def node_path(self,oid):
        # Transform.Find is relative to the loaded prefab's root, including for
        # variants whose root Transform exists only as a stripped source object.
        return '/'.join(name for name,_ in self.node_chain(oid)[1:])

    def binding_path(self,source):
        location=source['location']
        go=re.search(r'GameObject=(-?\d+)',location)
        if go:return self.node_path(go[1])
        document=int(re.search(r'document\[(\d+)\]',location)[1])
        data=self.docs[document]['PrefabInstance']
        instance_id=next(oid for oid,(kind,obj)in self.objects.items()if obj is data)
        index=int(re.search(r'm_Modifications/(\d+)/',location)[1])
        target=data['m_Modification']['m_Modifications'][index]['target']
        return '/'.join(name for name,_ in self.instance_chain(instance_id,target)[1:])

@lru_cache(None)
def prefab(path):return Prefab(path)

def main():
    data=json.loads(CATALOG.read_text())
    entries=[];bindings=[];errors=[]
    for e in data['entries']:
        item={k:e[k]for k in ['key','sourceText','zhCN','enUS']}
        data_args=set()
        for s in e['sources']:
            for v in s.get('variables',[]):
                if re.search(r'(species|qualityInfo|pond|speciesName|qualityName|pondName)',v['expression'],re.I)and not re.search(r'nickname|player|user',v['expression'],re.I):data_args.add(v['index'])
        if data_args:item['dataArguments']=sorted(data_args)
        if any(s['location']in {'QualityRankLabel','DietLabel','CatchGroupLabel','RarityLabel'}or s['file'].endswith(('WorldMapPonds.json','ProfileHubAchievements.json'))or('DesktopFishCatalog.cs'in s['file']and e['sourceText']in {'普通','优良','稀有','史诗','传说','神话','至尊'})for s in e['sources']):item['dataText']=True
        if not item['enUS'].strip():raise ValueError('Missing English: '+e['key'])
        entries.append(item)
        for source in e['sources']:
            if source['kind']!='Unity序列化' or not source['file'].endswith('.prefab'):continue
            try:
                path=prefab(ROOT/source['file']).binding_path(source)
                resource=source['file'].split('/Resources/',1)[1][:-7]
                bindings.append({'resource':resource,'path':path,'key':e['key']})
            except (KeyError,TypeError,ValueError,StopIteration)as exc:
                errors.append({'key':e['key'],'source':source,'error':repr(exc)})
    if errors:
        write_json(ROOT/'output/localization-binding-errors.json',errors)
        raise ValueError(f'{len(errors)} prefab bindings could not be resolved; no runtime files written')
    seen={}
    for b in bindings:
        identity=(b['resource'],b['path'])
        if identity in seen and seen[identity]!=b['key']:raise ValueError(f'Conflicting prefab binding: {identity}')
        seen[identity]=b['key']
    bindings=[{'resource':r,'path':p,'key':k}for(r,p),k in sorted(seen.items())]
    for directory in [ROOT/'fish-social-unity/Assets/Resources/Localization',ROOT/'desktop-overlay/OverlayResources/Localization',ROOT/'fish-social-unity/FishSocialOverlay/OverlayResources/Localization']:
        write_json(directory/'text-catalog.json',{'schemaVersion':1,'entries':entries})
    write_json(ROOT/'fish-social-unity/Assets/Resources/Localization/prefab-bindings.json',{'prefabs':bindings})
    print(json.dumps({'entries':len(entries),'prefabBindings':len(bindings)},ensure_ascii=False))

if __name__=='__main__':main()
