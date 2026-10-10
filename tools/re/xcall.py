"""Find BHAVs that call a given tree id. python xcall.py <id> [iffname-filter]"""
import sys, os
sys.path.insert(0, os.path.dirname(__file__))
from simsfiles import *
G='/mnt/user-data/uploads/The Sims Legacy Collection/'
want=int(sys.argv[1]); filt=sys.argv[2] if len(sys.argv)>2 else None
for far in ['GameData/Global/Global.far','GameData/Objects/Objects.far','ExpansionPack5/ExpansionPack5.far']:
  f=read_far(G+far)
  for name,d in f.items():
    if not name.lower().endswith('.iff') or (filt and filt not in name): continue
    try: ch=read_iff(d)
    except: continue
    for t,cid,l,dat in ch:
      if t=='BHAV':
        for i,(op,tp,fp,o) in enumerate(read_bhav(dat)):
          if op==want: print(far.split('/')[-1],name,cid,l,'@',i)
