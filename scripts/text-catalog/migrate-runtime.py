"""One-time, source-aware migration of reviewed C# copy to localization keys.

Dry run by default. --apply edits only matched display sources, never arbitrary
Chinese strings, control names, comparisons, editor tools, or server data.
"""
from pathlib import Path
from collections import defaultdict,Counter
import argparse,json,re,sys
import extract as scan

ROOT=Path(__file__).resolve().parents[2]
OUT=Path('/Users/red/Desktop/fish/output/runtime-localization-2026-10-28')
STRINGS={'string_literal','verbatim_string_literal','raw_string_literal','interpolated_string_expression'}

def member(node):
    while node:
        if node.type in {'method_declaration','constructor_declaration','property_declaration'}:
            n=node.child_by_field_name('name')
            return scan.text(n)if n else ''
        node=node.parent
    return ''

def candidates(tree):
    covered=set();items=[]
    for n in scan.walk(tree.root_node):
        if n.id in covered:continue
        if scan.plus(n)and any(x.type in STRINGS for x in scan.walk(n))and not(n.parent and scan.plus(n.parent)):
            value,variables=scan.make_template(n);kind='拼接模板'
            def consume(x):
                if x.type in STRINGS:covered.add(x.id)
                elif scan.plus(x):consume(x.child_by_field_name('left'));consume(x.child_by_field_name('right'))
            consume(n)
        elif n.type=='interpolated_string_expression':value,variables=scan.make_template(n);kind='插值模板'
        elif n.type in STRINGS:value,variables,kind=scan.literal(n),[],'代码常量'
        else:continue
        a=n.parent;already=False
        while a:
            if a.type=='invocation_expression'and re.match(r'(?:DesktopLocalization|OverlayLocalization)\.(?:Text|Format|Message|Data)\(',scan.text(a)):
                already=True;break
            a=a.parent
        if not already:items.append({'node':n,'value':value,'variables':variables,'kind':kind,'member':member(n)})
    return items

def in_comparison(node):
    a=node.parent
    for _ in range(3):
        if a is None:break
        if a.type in {'case_switch_label','constant_pattern'}:return True
        if a.type=='binary_expression'and any(c.type in {'==','!='}for c in a.children):return True
        if a.type=='invocation_expression':
            f=a.child_by_field_name('function')
            if f and re.search(r'\.(?:Equals|Contains|StartsWith|EndsWith|Find|FindDescendant|GetChild|Load)\b',scan.text(f)):return True
        a=a.parent
    return False

def plan(path,sources):
    raw=path.read_bytes();tree=scan.PARSERS['.cs'].parse(raw);found=candidates(tree)
    replacements={};const_declarations={};used=set();missing=[];matched=[]
    adapter='OverlayLocalization'if path.is_relative_to(ROOT/'desktop-overlay')else'DesktopLocalization'
    for e,s in sources:
        matches=[c for c in found if c['value']==e['sourceText']and c['kind']==s['kind']and c['member']==s['location']and c['node'].id not in used]
        if not matches:
            missing.append({'key':e['key'],'source':s});continue
        c=min(matches,key=lambda c:abs(c['node'].start_point.row+1-s['line']))
        n=c['node'];used.add(n.id)
        if in_comparison(n):
            missing.append({'key':e['key'],'source':s,'reason':'comparison-or-control-identity'});continue
        kind='format'if c['variables']else'text'
        args=c['variables'];target=n
        # Existing String.Format must receive a translated template, not a
        # translated result as its format string. Keep its original argument list.
        if n.parent and n.parent.type=='argument'and n.parent.parent and n.parent.parent.parent and n.parent.parent.parent.type=='invocation_expression':
            invocation=n.parent.parent.parent;fn=invocation.child_by_field_name('function')
            if fn and scan.text(fn)in {'string.Format','String.Format','System.String.Format'}:
                arguments=list(n.parent.parent.named_children);at=arguments.index(n.parent)
                if at in {0,1}:
                    target=invocation;kind='existing_format';args=arguments[at+1:]
        replacements[(target.start_byte,target.end_byte)]={'key':e['key'],'kind':kind,'node':n,'args':args}
        if kind=='text':
            a=n.parent
            while a and a.type not in {'field_declaration','local_declaration_statement','parameter','method_declaration'}:a=a.parent
            if a and a.type=='parameter':
                missing.append({'key':e['key'],'source':s,'reason':'optional-parameter'});del replacements[(target.start_byte,target.end_byte)];continue
            if a and a.type in {'field_declaration','local_declaration_statement'}:
                token=next((x for x in a.children if x.type=='modifier'and scan.text(x)=='const'),None)
                if token:const_declarations[(token.start_byte,token.end_byte)]='static readonly'if a.type=='field_declaration'else ''
        matched.append({'key':e['key'],'file':s['file'],'line':n.start_point.row+1,'kind':s['kind']})
    def render(start,end,skip=None):
        intervals=[r for r in replacements if start<=r[0]and r[1]<=end and r!=skip]
        outer=[r for r in intervals if not any(a[0]<=r[0]and r[1]<=a[1]and a!=r for a in intervals)]
        text=raw[start:end]
        for a,b in sorted(outer,reverse=True):text=text[:a-start]+replacement((a,b)).encode()+text[b-start:]
        return text.decode()
    def replacement(interval):
        r=replacements[interval];key=json.dumps(r['key']);n=r['node']
        if r['kind']=='text':return f'{adapter}.Text({key}, {scan.text(n)})'
        if r['kind']=='existing_format':args=[render(a.start_byte,a.end_byte,interval)for a in r['args']]
        else:
            args=[]
            for v in r['args']:
                # Variables can contain separately reviewed branch literals.
                expression=v['expression'];relative=raw.find(expression.encode(),n.start_byte,n.end_byte)
                value=render(relative,relative+len(expression.encode()),interval)if relative>=0 else expression
                args.append('(object)('+value+')')
        return f'{adapter}.Format({key}'+(', '+', '.join(args)if args else '')+')'
    edits=[]
    intervals=list(replacements)
    for interval in intervals:
        if not any(a[0]<=interval[0]and interval[1]<=a[1]and a!=interval for a in intervals):edits.append((*interval,replacement(interval)))
    edits.extend((a,b,value)for(a,b),value in const_declarations.items())
    result=raw
    for a,b,value in sorted(edits,reverse=True):result=result[:a]+value.encode()+result[b:]
    return result,matched,missing

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--apply',action='store_true');args=parser.parse_args()
    data=json.loads((ROOT/'unity-overlay文本表.json').read_text());files=defaultdict(list)
    for e in data['entries']:
        for s in e['sources']:
            if s['file'].endswith('.cs')and '/Editor/'not in s['file']:files[s['file']].append((e,s))
    report={'matched':[],'unmatched':[],'files':[]}
    for relative,sources in sorted(files.items()):
        path=ROOT/relative;result,matched,missing=plan(path,sources)
        report['matched']+=matched;report['unmatched']+=missing
        if result!=path.read_bytes():
            report['files'].append(relative)
            if args.apply:
                backup=OUT/'before'/relative;backup.parent.mkdir(parents=True,exist_ok=True);backup.write_bytes(path.read_bytes())
                path.write_bytes(result)
    OUT.mkdir(parents=True,exist_ok=True);(OUT/'migration-report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
    print(json.dumps({'files':len(report['files']),'matched':len(report['matched']),'unmatched':len(report['unmatched']),'unmatchedReasons':dict(Counter(x.get('reason','source-not-found')for x in report['unmatched']))},ensure_ascii=False))

if __name__=='__main__':main()
