"""Disassemble a range of a 32-bit PE and annotate immediates that point at strings.

Usage: python dis.py Sims.exe 0x45b3d2 400     (start VA in hex, byte count in decimal)
Requires: pip install pefile capstone
"""
import pefile, sys
from capstone import Cs, CS_ARCH_X86, CS_MODE_32
pe = pefile.PE(sys.argv[1]); base = pe.OPTIONAL_HEADER.ImageBase
data = pe.get_memory_mapped_image()
start = int(sys.argv[2],16); n = int(sys.argv[3])
md = Cs(CS_ARCH_X86, CS_MODE_32)
# string lookup for immediates
def s_at(va):
    off = va-base
    if 0 <= off < len(data):
        e = data.find(b'\x00', off, off+80)
        if e > off:
            s = data[off:e]
            if all(32 <= c < 127 for c in s) and len(s) >= 4: return s.decode()
    return None
for i, ins in enumerate(md.disasm(data[start-base:start-base+n], start)):
    extra = ''
    for tok in ins.op_str.replace(',', ' ').replace('[',' ').replace(']',' ').split():
        if tok.startswith('0x'):
            try:
                v = int(tok,16); s = s_at(v)
                if s: extra = f'   ; "{s}"'
            except: pass
    print(f"{ins.address:08x}: {ins.mnemonic} {ins.op_str}{extra}")
