"""TTAB reader (normal and field-encoded, as FreeSO's TTAB.Read). read_ttab(data) -> list of dicts."""
import struct

class _Normal:
    def __init__(s, d, p): s.d, s.p = d, p
    def _r(s, fmt):
        v = struct.unpack_from(fmt, s.d, s.p)[0]; s.p += struct.calcsize(fmt); return v
    def u16(s): return s._r('<H')
    def i16(s): return s._r('<h')
    def u32(s): return s._r('<I')
    def i32(s): return s._r('<i')
    def f32(s): return s._r('<f')

class _Field:
    W16 = (5, 8, 13, 16); W32 = (6, 11, 21, 32)
    def __init__(s, d, p):
        s.d = d; s.p = p; s.bit = 0; s.cur = d[p] if p < len(d) else 0; s.p += 1
    def bitr(s):
        r = (s.cur >> (7 - s.bit)) & 1
        s.bit += 1
        if s.bit > 7:
            s.bit = 0
            s.cur = s.d[s.p] if s.p < len(s.d) else 0
            s.p += 1
        return r
    def bits(s, n):
        t = 0
        for i in range(n): t = (t << 1) | s.bitr()
        return t
    def field(s, widths):
        if s.bitr() == 0: return 0
        w = widths[s.bits(2)]
        v = s.bits(w)
        if v & (1 << (w - 1)): v -= (1 << w)
        return v
    def u16(s): return s.field(s.W16) & 0xFFFF
    def i16(s): return s.field(s.W16)
    def u32(s): return s.field(s.W32) & 0xFFFFFFFF
    def i32(s): return s.field(s.W32)
    def f32(s): return struct.unpack('<f', struct.pack('<I', s.field(s.W32) & 0xFFFFFFFF))[0]

def read_ttab(d):
    n, ver = struct.unpack_from('<HH', d, 0)
    p = 4
    if ver < 9 or ver > 10: io = _Normal(d, p)
    else:
        code = d[p]; p += 1
        io = _Field(d, p) if code == 1 else _Normal(d, p)
    out = []
    for i in range(n):
        e = {}
        e['action'] = io.u16(); e['test'] = io.u16()
        nm = io.u32(); e['flags'] = io.u32(); e['tta'] = io.u32()
        e['atten_code'] = io.u32() if ver > 6 else 0
        e['atten'] = io.f32(); e['autothresh'] = io.u32(); e['join'] = io.i32()
        mot = []
        for j in range(nm):
            mn = io.i16() if ver > 6 else 0
            md = io.i16()
            pm = io.u16() if ver > 6 else 0
            mot.append((mn, md, pm))
        e['motives'] = mot
        if ver > 9: e['flags2'] = io.u32()
        out.append(e)
    return out
