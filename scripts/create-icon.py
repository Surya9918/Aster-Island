"""Generate original geometric Aster artwork without third-party assets or packages."""
from pathlib import Path
import math,struct,zlib
out=Path(__file__).resolve().parents[1]/'src/Aster.App/Assets';out.mkdir(parents=True,exist_ok=True)
size=64
def chunk(tag,data):return struct.pack('>I',len(data))+tag+data+struct.pack('>I',zlib.crc32(tag+data)&0xffffffff)
rows=[]
for y in range(size):
 row=bytearray([0])
 for x in range(size):
  dx,dy=abs((x+.5)-32),abs((y+.5)-32)
  star=(dx/27+dy/8<=1) or (dx/8+dy/27<=1)
  row.extend((118,216,203,255) if star else (16,20,29,255) if dx<30 and dy<30 else (0,0,0,0))
 rows.append(row)
png=b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('>IIBBBBB',size,size,8,6,0,0,0))+chunk(b'IDAT',zlib.compress(b''.join(rows)))+chunk(b'IEND',b'')
(out/'aster.png').write_bytes(png)
(out/'aster.ico').write_bytes(struct.pack('<HHH',0,1,1)+struct.pack('<BBBBHHII',64,64,0,0,1,32,len(png),22)+png)
