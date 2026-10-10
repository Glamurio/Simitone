"""Rough BHAV disassembler (analysis helper).

python bhav.py <far-or-iff> <iff-name-in-far|-> <bhav-id> [more ids]
Resolves names of private (>=4096), semi-global (>=8192) and global (256..4095) trees when the
matching files are passed via env SIMS_GLOBAL (Global.far path).
"""
import struct, sys, os
sys.path.insert(0, os.path.dirname(__file__))
from simsfiles import read_far, read_iff, read_bhav
from primnames import PRIMS

SCOPES = ['MyAttr','SOAttr','TargAttr','Me','SO','Targ','Global','Lit','Temp','Param','SOID','TempByTemp',
 'TreeAdRange','SOTemp','MyMotive','SOMotive','SOSlot','SOMotiveByTemp','MyPD','SOPD','MySlot','SODef',
 'SOAttrByParam','RoomByTemp0','NeighborInSO','Local','Tuning','DynSprFlag','TreeAdPers','TreeAdMin',
 'MyPDByTemp','SOPDByTemp','NeighborPD','JobData','NhoodData','SOFunction','MyTypeAttr','SOTypeAttr',
 'NeighborsObjDef','Unused','LocalByTemp','SOAttrByTemp','TempXL','CityTime','TSOStdTime','GameTime',
 'MyList','SOList','Money32','MyLeadTileAttr','SOLeadTileAttr','MyLeadTile','SOLeadTile','SOMasterDef','FeatureLvl']
OPS = ['>','<','==','+=','-=',':=','*=','/=','flag?','setflag','clrflag','++<','%=','&=','>=','<=','!=','-->','|=','^=','sqrt=']
def scope(o, d):
    n = SCOPES[o] if o < len(SCOPES) else 'S%d' % o
    return '%s[%d]' % (n, d) if o != 7 else str(d)

def names_of(chunks):
    return {cid: label for t, cid, label, dat in chunks if t == 'BHAV'}

def dis(dat, names):
    out = []
    for i, (op, t, f, o) in enumerate(read_bhav(dat)):
        def ptr(x): return {253: 'ERR', 254: 'T', 255: 'F'}.get(x, str(x))
        if op >= 256:
            a = struct.unpack('<4h', o)
            s = 'call %d "%s" %s' % (op, names.get(op, '?'), a)
        elif op == 2:
            l, r = struct.unpack_from('<hh', o, 0); sg, opr, lo, ro = o[4], o[5], o[6], o[7]
            s = '%s %s %s' % (scope(lo, l), OPS[opr] if opr < len(OPS) else opr, scope(ro, r))
        else:
            s = '%s %s' % (PRIMS.get(op, 'prim%d' % op), o.hex())
        out.append('%3d: %-70s T:%s F:%s' % (i, s, ptr(t), ptr(f)))
    return '\n'.join(out)

if __name__ == '__main__':
    src, inner = sys.argv[1], sys.argv[2]
    data = read_far(src)[inner] if src.lower().endswith('.far') else open(src, 'rb').read()
    ch = read_iff(data)
    names = names_of(ch)
    g = os.environ.get('SIMS_GLOBAL')
    if g:
        names.update(names_of(read_iff(read_far(g)['Global.iff'])))
    for want in map(int, sys.argv[3:]):
        for t, cid, label, dat in ch:
            if t == 'BHAV' and cid == want:
                print('== BHAV %d %s' % (cid, label)); print(dis(dat, names))
