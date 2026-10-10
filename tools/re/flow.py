"""Print a BHAV as a control-flow tree (depth-first from node 0). python flow.py <far|iff> <iff-in-far> <bhav-id...>"""
import sys, os, struct
sys.path.insert(0, os.path.dirname(__file__))
from simsfiles import read_far, read_iff, read_bhav
import bhav as B
def flow(dat, names):
    nodes = list(read_bhav(dat)); out = []; seen = set()
    def txt(i):
        op, t, f, o = nodes[i]
        full = B.dis(dat, names).split('\n')[i]
        return full[5:full.index(' T:')].strip() if ' T:' in full else full
    def walk(i, ind, tag):
        if i in (253, 254, 255):
            out.append('  ' * ind + tag + {253: 'ERR', 254: 'TRUE', 255: 'FALSE'}[i]); return
        if i in seen:
            out.append('  ' * ind + tag + 'goto %d' % i); return
        seen.add(i)
        op, t, f, o = nodes[i]
        out.append('  ' * ind + '%s[%d] %s' % (tag, i, txt(i)))
        if t == f: walk(t, ind, '')
        else:
            walk(t, ind + 1, 'T: '); walk(f, ind + 1, 'F: ')
    walk(0, 0, ''); return '\n'.join(out)
if __name__ == '__main__':
    src, inner = sys.argv[1], sys.argv[2]
    data = read_far(src)[inner] if src.lower().endswith('.far') else open(src, 'rb').read()
    ch = read_iff(data); names = B.names_of(ch)
    g = os.environ.get('SIMS_GLOBAL')
    if g: names.update(B.names_of(read_iff(read_far(g)['Global.iff'])))
    for want in map(int, sys.argv[3:]):
        for t, cid, label, dat in ch:
            if t == 'BHAV' and cid == want:
                print('== BHAV %d %s' % (cid, label)); print(flow(dat, names))
