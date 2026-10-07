"""Minimal read-only parsers for The Sims 1 data files (analysis helpers, not used by the game).

read_far(path) -> {name: bytes}      FAR1 archives (GameData/Objects/Objects.far, Global/Global.far)
read_iff(bytes) -> [(type, id, label, data)]
read_str(data) -> [(lang, string, comment)]   STR# chunks
read_bhav(data) -> [(opcode, true_ptr, false_ptr, operand8)]
objd_guid(data) -> GUID of an OBJD chunk

Example:
    from simsfiles import *
    g = read_far(r"D:/Games/The Sims/GameData/Global/Global.far")
    for t, cid, label, dat in read_iff(g["Global.iff"]):
        if t == "BHAV": print(cid, label)
"""
import struct, sys, os
def read_far(path):
    d = open(path,'rb').read()
    assert d[:8]==b'FAR!byAZ', d[:8]
    ver, moff = struct.unpack_from('<II', d, 8)
    n = struct.unpack_from('<I', d, moff)[0]; p = moff+4; out={}
    for i in range(n):
        s1,s2,off = struct.unpack_from('<III', d, p); p+=12
        if ver == 1:
            nl = struct.unpack_from('<I', d, p)[0]; p+=4
        else:
            nl = struct.unpack_from('<H', d, p)[0]; p+=2
        name = d[p:p+nl].decode('latin1'); p+=nl
        out[name] = d[off:off+s1]
    return out
def read_iff(d):
    assert d[:8]==b'IFF FILE', d[:16]
    p = 64; chunks=[]
    while p+76 <= len(d):
        t = d[p:p+4].decode('latin1'); size = struct.unpack_from('>I', d, p+4)[0]
        cid = struct.unpack_from('>h', d, p+8)[0]; flags = struct.unpack_from('>H', d, p+10)[0]
        label = d[p+12:p+76].split(b'\x00')[0].decode('latin1')
        chunks.append((t, cid, label, d[p+76:p+size]))
        if size < 76: break
        p += size
    return chunks
def read_str(dat):
    fmt = struct.unpack_from('<h', dat, 0)[0]
    out=[]
    if fmt == -3:  # 0xFFFD
        n = struct.unpack_from('<H', dat, 2)[0]; p=4
        for i in range(n):
            lang = dat[p]; p+=1
            e = dat.index(b'\x00', p); s = dat[p:e].decode('latin1'); p=e+1
            e = dat.index(b'\x00', p); c = dat[p:e].decode('latin1'); p=e+1
            out.append((lang,s,c))
    elif fmt == -2:
        n = struct.unpack_from('<H', dat, 2)[0]; p=4
        for i in range(n):
            e = dat.index(b'\x00', p); s = dat[p:e].decode('latin1'); p=e+1
            e = dat.index(b'\x00', p); c = dat[p:e].decode('latin1'); p=e+1
            out.append((1,s,c))
    elif fmt == -1:
        n = struct.unpack_from('<H', dat, 2)[0]; p=4
        for i in range(n):
            e = dat.index(b'\x00', p); s = dat[p:e].decode('latin1'); p=e+1
            out.append((1,s,''))
    elif fmt == 0:
        n = struct.unpack_from('<H', dat, 2)[0]; p=4
        for i in range(n):
            l = dat[p]; s = dat[p+1:p+1+l].decode('latin1'); p+=1+l
            out.append((1,s,''))
    elif fmt == -4:
        nsets = dat[2]; p=3
        for k in range(nsets):
            n = struct.unpack_from('<H', dat, p)[0]; p+=2
            for i in range(n):
                lang = dat[p]; p+=1
                l = dat[p]; s=dat[p+1:p+1+l].decode('latin1'); p+=1+l
                l = dat[p]; c=dat[p+1:p+1+l].decode('latin1'); p+=1+l
                out.append((lang,s,c))
    return out
def read_bhav(dat):
    v = struct.unpack_from('<H', dat, 0)[0]; p=2
    if v in (0x8000,0x8001):
        n = struct.unpack_from('<H', dat, p)[0]; p+=2+8
    elif v == 0x8002:
        n = struct.unpack_from('<H', dat, p)[0]; p+=2+8
    elif v == 0x8003:
        p += 7; n = struct.unpack_from('<I', dat, p)[0]; p+=4
    else: return []
    ins=[]
    for i in range(n):
        op,t,f = struct.unpack_from('<HBB', dat, p); operand = dat[p+4:p+12]; p+=12
        ins.append((op,t,f,operand))
    return ins
def objd_guid(dat):
    vals = struct.unpack_from('<%dH'%((len(dat)-4)//2), dat, 4)
    return vals[12] | (vals[13]<<16)
