"""Package the tested glass update separately from the previous island release."""
from pathlib import Path
import json,zipfile,hashlib
root=Path(__file__).resolve().parents[1];out=root.parent;publish=root/'artifacts/Aster-Glass-win-arm64';visual=root/'artifacts/glass-visuals-final';reports=root/'artifacts/reports'
tests=json.loads((reports/'tests-glass.json').read_text());assert tests['failed']==0
wallpaper=json.loads((reports/'wallpaper.json').read_text())
assert all(v for v in wallpaper.values() if isinstance(v,bool))
devices=json.loads((reports/'devices-glass.json').read_text())
for key in ['maximizedForegroundStows','externalVolumeCallbackObserved','volumeKeyIntercepted','volumeActuallyChanged','volumeHudShown','brightnessActuallyChanged','brightnessEventObserved','brightnessHudShown']:assert devices.get(key) is True,key
assert json.loads((reports/'taskbar-probe.json').read_text())['restored'] is True
assert not json.loads((reports/'artifact-verification.json').read_text())['errors']
assert not json.loads((visual/'regression-report.json').read_text())['errors']
assert json.loads((visual/'artwork-report.json').read_text())['staleCoverCleared'] is True
assert (visual/'visual-report.json').stat().st_mtime>=max(p.stat().st_mtime for p in publish.glob('Aster*.dll')),'Visual report is stale'
with zipfile.ZipFile(out/'Aster-Glass-win-arm64.zip','w',zipfile.ZIP_DEFLATED,compresslevel=6) as archive:
 for p in sorted(publish.iterdir()):
  if p.is_file():archive.write(p,'Aster-Glass/'+p.name)
 archive.write(root/'README.md','Aster-Glass/README.md')
 for name in ['GLASS-UPDATE.md','GLASS-VALIDATION.md','PRIVACY.md','TROUBLESHOOTING.md','SPOTIFY-WALLPAPER.md']:archive.write(root/'docs'/name,'Aster-Glass/'+name)
with zipfile.ZipFile(out/'Aster-Glass-source.zip','w',zipfile.ZIP_DEFLATED,compresslevel=6) as archive:
 for p in sorted(root.rglob('*')):
  if not p.is_file():continue
  rel=p.relative_to(root)
  if any(part in ['bin','obj','artifacts','.git','__pycache__'] for part in rel.parts):continue
  archive.write(p,Path('Aster')/rel)
with zipfile.ZipFile(out/'Aster-Glass-validation.zip','w',zipfile.ZIP_DEFLATED,compresslevel=6) as archive:
 for p in sorted(visual.rglob('*')):
  if p.is_file():archive.write(p,Path('visuals')/p.relative_to(visual))
 for name in ['tests-glass.json','devices-glass.json','taskbar-probe.json','artifact-verification.json','wallpaper.json']:
  archive.write(reports/name,'reports/'+name)
 for p in reports.glob('devices-glass.*.png'):archive.write(p,'reports/'+p.name)
archive_preview=reports/'wallpaper.preview.png'
# The generated preview is original fixture art, not a screenshot of the user's desktop.
import shutil
shutil.copyfile(archive_preview,out/'Aster-Spotify-wallpaper-preview.png')
names=['Aster-Glass-win-arm64.zip','Aster-Glass-source.zip','Aster-Glass-validation.zip','Aster-Glass-preview.png','Aster-Glass-motion.gif','Aster-Spotify-wallpaper-preview.png']
result=[{'file':n,'bytes':(out/n).stat().st_size,'sha256':hashlib.sha256((out/n).read_bytes()).hexdigest()} for n in names]
(out/'Aster-Glass-SHA256.json').write_text(json.dumps(result,indent=2),encoding='utf-8');print(json.dumps(result,indent=2))
