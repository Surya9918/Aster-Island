"""Verify published PE architecture and original-source dependency boundaries."""
from pathlib import Path
import struct,json,sys,re
root=Path(__file__).resolve().parents[1]
publish=root/'artifacts/Aster-Glass-win-arm64'
results=[];errors=[]
for p in sorted(publish.iterdir()):
 if p.suffix.lower() not in ['.exe','.dll']:continue
 b=p.read_bytes()
 if b[:2]!=b'MZ':errors.append(p.name+' is not a PE');continue
 off=struct.unpack_from('<I',b,0x3c)[0];machine,sections=struct.unpack_from('<HH',b,off+4)
 opt_size=struct.unpack_from('<H',b,off+20)[0];optional=off+24
 magic=struct.unpack_from('<H',b,optional)[0];directory=optional+(112 if magic==0x20b else 96)
 clr_rva=struct.unpack_from('<I',b,directory+14*8)[0]
 flags=0
 if clr_rva:
  section_start=optional+opt_size
  for i in range(sections):
   s=section_start+40*i;vs,va,raw_size,raw_ptr=struct.unpack_from('<IIII',b,s+8)
   if va<=clr_rva<va+max(vs,raw_size):flags=struct.unpack_from('<I',b,raw_ptr+clr_rva-va+16)[0];break
 il_only=bool(flags&1) and not bool(flags&2)
 valid=machine==0xaa64 or (machine==0x14c and il_only)
 if not valid:errors.append(f'{p.name}: unexpected machine 0x{machine:x}, CLR flags {flags}')
 results.append({'file':p.name,'machine':hex(machine),'managedILOnly':il_only,'accepted':valid})
sources=list((root/'src').rglob('*.cs'))+list((root/'src').rglob('*.xaml'))+list((root/'src').rglob('*.csproj'))
connections=[]
for p in sources:
 if 'obj' in p.parts or 'bin' in p.parts:continue
 t=p.read_text(encoding='utf-8-sig')
 if re.search(r'NotchPixel|SkiaSharp|WebView2|Velopack|Sentry|TelemetryClient',t):errors.append('Unexpected reference in '+str(p.relative_to(root)))
 if re.search(r'new HttpClient|new HttpClientHandler|\.GetAsync\(|TcpListener|WebSocket|UdpClient',t):connections.append(p.relative_to(root).as_posix())
if connections!=['src/Aster.Infrastructure/WeatherClient.cs']:errors.append('Unexpected network source boundary: '+str(connections))
if not results:errors.append('No published PE files found')
report={'peFiles':len(results),'sourceNetworkBoundary':connections,'errors':errors,'files':results}
dest=root/'artifacts/reports/artifact-verification.json';dest.parent.mkdir(parents=True,exist_ok=True);dest.write_text(json.dumps(report,indent=2),encoding='utf-8')
print(json.dumps({'peFiles':len(results),'networkSourceFiles':connections,'errors':errors},indent=2))
sys.exit(bool(errors))
