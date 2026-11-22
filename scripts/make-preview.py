"""Compose actual own-window test renders into a gallery and recorded animation."""
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
import json
root=Path(__file__).resolve().parents[1];src=root/'artifacts/glass-visuals-final';out=root.parent
fontpath='C:/Windows/Fonts/segoeui.ttf'
font=ImageFont.truetype(fontpath,17);small=ImageFont.truetype(fontpath,12)
def backdrop(size):
 im=Image.new('RGB',size);p=im.load()
 for y in range(size[1]):
  for x in range(size[0]):
   glow=max(0,1-((x/size[0]-.5)**2+(y/size[1]-.15)**2)**.5)
   p[x,y]=(int(14+10*glow),int(21+17*glow),int(30+24*glow))
 return im
items=[('20-live-controls','Real device controls'),('21-live-today','Your day, on your device'),('16-stowed','Tucked into the upper bar'),('17-volume-hud','Aster volume indicator'),('18-brightness-hud','Aster brightness indicator'),('01-idle','Smaller desktop pill')]
sheet=backdrop((1240,1150));draw=ImageDraw.Draw(sheet)
draw.text((32,20),'aster  /  an island of your own',font=font,fill='#dfeaf2')
for i,(name,label) in enumerate(items):
 shot=Image.open(src/(name+'.png')).convert('RGBA').crop((70,0,670,362))
 x=20+(i%2)*610;y=75+(i//2)*355
 shot.thumbnail((590,310),Image.Resampling.LANCZOS);sheet.paste(shot,(x,y),shot)
 draw.text((x+20,y+312),label,font=small,fill='#94b8bc')
sheet.save(out/'Aster-Glass-preview.png')
clips=json.loads((src/'recordings.json').read_text(encoding='utf-8-sig'))
base=backdrop((620,370));palette=sheet.quantize(colors=240)
frames=[];durations=[]
for clip in clips:
 files=sorted((src/'recordings'/clip['name']).glob('frame-*.png'))
 times=clip['frameTimesMs'];last=0
 for i,file in enumerate(files):
  im=base.copy();shot=Image.open(file).convert('RGBA').crop((32,0,652,370));im.paste(shot,(0,0),shot)
  frames.append(im.quantize(palette=palette,dither=Image.Dither.NONE))
  durations.append(max(10,round((times[i]-last)/10)*10));last=times[i]
 if durations:durations[-1]+=250
frames[0].save(out/'Aster-Glass-motion.gif',save_all=True,append_images=frames[1:],duration=durations,loop=0,optimize=False,disposal=2)
print('Wrote actual-render preview and recorded motion GIF.')
