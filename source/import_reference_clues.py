"""Build native clue cards from the attributed, pinned catalogue. No network needed."""
from pathlib import Path
import json, sys, math, xml.etree.ElementTree as ET

root=Path(__file__).resolve().parent.parent
data=json.loads((root/'assets/data/clue-catalogue.json').read_text(encoding='utf-8-sig'))
tiers=['easy','medium','hard','elite']
enabled=tiers[:tiers.index(sys.argv[1] if len(sys.argv)>1 else 'elite')+1]
rows=data['records']+data.get('legacy',[])
if not rows:
    rows=next(v for v in data.values() if isinstance(v,list) and v and isinstance(v[0],dict) and 'tier' in v[0])

def area(v):
    if 'origin' in v:
        p=v['origin'];s=v.get('size',{});return(p['x'],p['y'],s.get('x',1),s.get('y',1),p.get('level',0))
    if 'topleft' in v:
        a=v['topleft'];b=v['botright'];return(a['x'],a['y'],b['x']-a['x']+1,b['y']-a['y']+1,v.get('level',0))
    return(v['x'],v['y'],1,1,v.get('level',0))

def locstr(a):return '%s, %s | level %s'%(a[0],a[1],a[4])

guides={}
for line in (root/'assets/data/location-guides.tsv').read_text(encoding='utf-8-sig').splitlines():
    key,name,route=line.split('\t')
    guides[int(key)]=(name,route)
lodestones=[]
for line in (root/'assets/compass/positions.tsv').read_text(encoding='utf-8-sig').splitlines():
    name,x,y,*_=line.split('\t')
    if 'lodestone' in name.lower():lodestones.append((name,int(x),int(y)))

def approach(a):
    if not a or a[1]>4100 or a[1]<2500 or a[0]>4100:return ''
    name,x,y=min(lodestones,key=lambda l:(l[1]-a[0])**2+(l[2]-a[1])**2)
    dx=a[0]-x;dy=a[1]-y
    if abs(dx)+abs(dy)<8:return 'Start at the '+name+'.'
    direction=['east','north-east','north','north-west','west','south-west','south','south-east'][round(math.atan2(dy,dx)/(math.pi/4))%8]
    return 'From the '+name+', the destination lies '+direction+'.'

def card(row):
    typ=row['type'];details=[];a=None
    if 'id' not in row:
        node=ET.Element('clue',id='900001',tier=row['tier'],type=typ,heading=row['location_description'],action=row['answer'])
        for text in row['text']:ET.SubElement(node,'text').text=text
        ET.SubElement(node,'details').text=row['answer']+'\n\n'+row['status']
        return node
    if typ=='emote':
        a=area(row['area']);action=' / '.join(row['emotes']);heading=row['text'][0].split('. ')[0]
        details+=['Do: '+action]+['Wear: '+s for s in row['items']]
        if row['double_agent']:details+=['Defeat the double agent, then repeat the emote for Uri.']
        else:details+=['Repeat the emote if required to speak to Uri.']
    elif typ=='coordinates':
        c=row['coordinates'];lat=c['latitude'];lon=c['longitude']
        x=2440+(1 if lon['direction']=='east' else -1)*(32*lon['degrees']+math.ceil(lon['minutes']*8/15))
        y=3161+(1 if lat['direction']=='north' else -1)*(32*lat['degrees']+math.ceil(lat['minutes']*8/15))
        a=(x,y,1,1,0);action='Dig';heading=row['text'][0];details+=['Dig at the marked tile.','Coordinates: '+locstr(a)]
    else:
        sol=row['solution'];st=sol['type']
        if st=='talkto':
            action='Talk to '+sol['npc'];heading=action
            for spot in sol['spots']:
                aa=area(spot['range']);a=a or aa;details+=[action+' '+spot.get('description','')+' ('+locstr(aa)+').']
        elif st=='search':
            a=area(sol['spot']);action='Search '+sol['entity'];heading=action
            details+=[action+' at '+locstr(a)+'.']
        else:
            a=area(sol['spot']);action='Dig';heading='Dig '+sol.get('description','at the marked tile');details+=[heading+'.','Tile: '+locstr(a)]
    if row['id'] in guides:
        heading,route=guides[row['id']]
        details=([route,'']+details if typ=='emote' else [route])
    elif typ=='simple' and row['solution']['type']=='search':
        heading=row['text'][0].rstrip('.')
        details=[approach(a),heading+'.']
    elif typ in ('simple','cryptic','anagram') and row['solution']['type']=='talkto':
        first=row['solution']['spots'][0]
        heading=action+' '+first.get('description','')
        details=[approach(a)]+[action+' '+s.get('description','')+'.' for s in row['solution']['spots']]
    elif typ=='emote':
        details=[approach(a)]+details
    if a and a[4]>0:details.append('Use the stairs to floor '+str(a[4])+' above ground.')
    for c in row.get('challenge',[]):
        if c['type']=='challengescroll':
            details+=['','Challenge: '+c['question']]
            for answer in c['answers']:details+=['Answer: '+str(answer['answer'])+(' — '+answer['note'] if answer.get('note') else '')]
        elif c['type']=='wizard':details+=['Be ready to fight the wizard when digging.']
        elif c['type']=='slider':details+=['An NPC may give you a puzzle box. Open it for the puzzle helper.']
    if a:details+=['','Tile '+locstr(a)]
    node=ET.Element('clue',id=str(row['id']),tier=row['tier'],type=typ,heading=heading,action=action)
    if a:
        for k,v in zip(['x','y','width','height','level'],a):node.set(k,str(v))
    for text in row['text']:ET.SubElement(node,'text').text=text
    for text in row.get('anagram',[]):ET.SubElement(node,'anagram').text=text
    ET.SubElement(node,'details').text='\n'.join(details)
    return node

out=ET.Element('catalogue',version='1',source=data['source_commit'])
rows=rows+[dict(id=900002,tier='hard',type='coordinates',text=['05 degrees 35 minutes south,26 degrees 30 minutes east'],coordinates=dict(latitude=dict(degrees=5,minutes=35,direction='south'),longitude=dict(degrees=26,minutes=30,direction='east')))]
count={}
for row in rows:
    if row['tier'] not in enabled or row['type'] in ['scan','compass']:continue
    out.append(card(row));count[row['tier']]=count.get(row['tier'],0)+1
ET.indent(out)
ET.ElementTree(out).write(root/'assets/data/reference-clues.xml',encoding='utf-8',xml_declaration=True)
print(count)
