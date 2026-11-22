"""Compare actual app-rendered PNGs with reviewed baselines; also check state geometry.
Run: python scripts/visual-regression.py --accept  (review first)
     python scripts/visual-regression.py           (compare; nonzero on failure)
Requires Pillow from requirements-visual.txt. No network or desktop capture.
"""
from pathlib import Path
from PIL import Image,ImageChops,ImageStat
import json,sys,shutil
root=Path(__file__).resolve().parents[1]
current=root/'artifacts/glass-visuals-final';baseline=root/'tests/glass-visual-baselines';baseline.mkdir(parents=True,exist_ok=True)
names=['01-idle','02-hover','04-expanded-music','05-expanded-focus','06-persistent-focus','07-notification','08-files','09-note','10-sound','11-simultaneous','12-hidden','13-restored','15-today','16-stowed','17-volume-hud','18-brightness-hud','19-hud-return','20-artwork-compact','21-artwork-expanded','22-artwork-cleared','23-hover-open']
accept='--accept' in sys.argv
results=[];errors=[]
report=json.loads((current/'visual-report.json').read_text(encoding='utf-8-sig'))
for check in report['checks']:
 if check['current']!=check['target']:errors.append(check['name']+' did not settle')
 if check['width']<=0 or check['height']<=0:errors.append(check['name']+' invalid geometry')
for name in names:
 path=current/(name+'.png');ref=baseline/(name+'.png')
 if not path.exists():errors.append('Missing '+name);continue
 actual=Image.open(path).convert('RGBA')
 if name=='12-hidden' and actual.getchannel('A').getbbox() is not None:errors.append('Hidden state contains visible pixels')
 if accept:shutil.copyfile(path,ref)
 if not ref.exists():errors.append('No reviewed baseline '+name);continue
 expected=Image.open(ref).convert('RGBA')
 if actual.size!=expected.size:errors.append(name+' changed canvas size');continue
 mask_actual=actual.getchannel('A').point(lambda x:255 if x>64 else 0)
 mask_expected=expected.getchannel('A').point(lambda x:255 if x>64 else 0)
 shape=ImageStat.Stat(ImageChops.difference(mask_actual,mask_expected)).mean[0]/255
 # A small mean tolerance allows clock/timer glyph changes, while catching material/layout changes.
 color=sum(ImageStat.Stat(ImageChops.difference(actual,expected)).mean)/4
 passed=shape<.003 and color<3.5
 if not passed:errors.append(name+' differs from reviewed geometry/material')
 results.append({'name':name,'shapeDifferenceRatio':shape,'meanChannelDifference':color,'passed':passed})
result={'baselineAccepted':accept,'comparisons':results,'errors':errors,'scope':'Initial reviewed baselines; no historical cross-device certification.'}
(current/'regression-report.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
print(json.dumps({'compared':len(results),'errors':errors},indent=2));sys.exit(bool(errors))
