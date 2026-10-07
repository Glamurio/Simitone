"""Find code references to strings in a 32-bit PE (e.g. Sims.exe).

Usage: python xref.py Sims.exe "string one" "string two" ...
Prints each string's VA and the code addresses that push/mov its address, with a heuristic
enclosing-function start. Requires: pip install pefile capstone
"""
import pefile, sys, struct, re
from capstone import Cs, CS_ARCH_X86, CS_MODE_32
pe = pefile.PE(sys.argv[1])
base = pe.OPTIONAL_HEADER.ImageBase
data = pe.get_memory_mapped_image()
targets = sys.argv[2:]
text = [s for s in pe.sections if s.Name.startswith(b'.text')][0]
tstart = text.VirtualAddress; tend = tstart + text.Misc_VirtualSize
code = data[tstart:tend]
# locate strings
def find_all(needle):
    out=[]; i=0
    while True:
        i = data.find(needle, i)
        if i<0: break
        out.append(i); i+=1
    return out
# function start heuristic: nearest preceding 'push ebp; mov ebp,esp' (55 8B EC) or after CC padding
def func_start(off):
    i = off
    while i > tstart:
        if data[i:i+3] == b'\x55\x8b\xec' : return i
        if data[i-1] in (0xCC,0xC3) and data[i] not in (0xCC,): 
            # possible start after padding
            if data[i-1]==0xCC: return i
        i -= 1
    return None
for t in targets:
    locs = [l for l in find_all(t.encode('latin1')+b'\x00') if data[l-1:l] == b'\x00' or True]
    for l in locs:
        va = base + l
        imm = struct.pack('<I', va)
        refs = [m.start() for m in re.finditer(re.escape(imm), code)]
        rs = []
        for r in refs:
            off = tstart + r
            fs = func_start(off)
            rs.append((hex(base+off), hex(base+fs) if fs else None))
        print(f"{t!r} @ {hex(va)} refs: {rs[:6]}")
