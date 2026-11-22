"""Package original source and verified ARM64 publish output; no reference code/binaries."""
from pathlib import Path
import zipfile,json,hashlib
root=Path(__file__).resolve().parents[1];out=root.parent
publish=root/'artifacts/Aster-Island-win-arm64';visual=root/'artifacts/island-visuals'
report=json.loads((visual/'visual-report.json').read_text(encoding='utf-8-sig'))
assert (visual/'visual-report.json').stat().st_mtime >= max(p.stat().st_mtime for p in publish.glob('Aster*.dll')),'Visual report is older than the application assemblies'
assert not json.loads((root/'artifacts/reports/artifact-verification.json').read_text())['errors']
assert not json.loads((visual/'regression-report.json').read_text())['errors']
assert json.loads((root/'artifacts/reports/tests.json').read_text())['failed']==0
with zipfile.ZipFile(out/'Aster-Island-win-arm64.zip','w',zipfile.ZIP_DEFLATED,compresslevel=6) as archive:
 for p in sorted(publish.iterdir()):
  if p.is_file():archive.write(p,'Aster-Island/'+p.name)
 archive.write(root/'README.md','Aster-Island/README.md')
 for name in ['PRIVACY.md','VALIDATION.md','TROUBLESHOOTING.md']:archive.write(root/'docs'/name,'Aster-Island/'+name)
with zipfile.ZipFile(out/'Aster-Island-source.zip','w',zipfile.ZIP_DEFLATED,compresslevel=6) as archive:
 for p in sorted(root.rglob('*')):
  if not p.is_file():continue
  rel=p.relative_to(root)
  if any(part in ['bin','obj','.git','artifacts','__pycache__'] for part in rel.parts):continue
  archive.write(p,Path('Aster')/rel)
with zipfile.ZipFile(out/'Aster-Island-validation.zip','w',zipfile.ZIP_DEFLATED,compresslevel=6) as archive:
 for folder in [visual,root/'artifacts/reports']:
  for p in sorted(folder.rglob('*')):
   if p.is_file():archive.write(p,p.relative_to(root/'artifacts'))
names=['Aster-Island-win-arm64.zip','Aster-Island-source.zip','Aster-Island-validation.zip','Aster-Island-motion.gif','Aster-Island-preview.png']
hashes=[{'file':name,'bytes':(out/name).stat().st_size,'sha256':hashlib.sha256((out/name).read_bytes()).hexdigest()} for name in names]
(out/'Aster-Island-SHA256.json').write_text(json.dumps(hashes,indent=2),encoding='utf-8')
print(json.dumps(hashes,indent=2))
