"""Package actual Unity frames; never synthesize or replace character motion."""
import argparse
import json
from pathlib import Path
import subprocess
import numpy as np
from PIL import Image,ImageDraw,ImageFont
import imageio_ffmpeg
from lab import save

def build(source,out,description):
    out.mkdir(parents=True,exist_ok=False)
    font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',22)
    small=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',16)
    layers=['Foundation','LegacyRaw','LegacyCorrected']
    title={'Foundation':'Foundation / no corrections','LegacyRaw':'Previous raw mapping','LegacyCorrected':'Previous corrected mapping'}
    for view in ['front','side','back']:
        count=len(list((source/layers[0]/view).glob('*.png')))
        encoder=subprocess.Popen([imageio_ffmpeg.get_ffmpeg_exe(),'-y','-loglevel','error','-f','rawvideo','-pix_fmt','rgb24','-s','1920x552','-r','20','-i','-','-an','-c:v','libx264','-crf','20','-pix_fmt','yuv420p','-movflags','+faststart',str(out/(view+'.mp4'))],stdin=subprocess.PIPE)
        try:
            for frame in range(count):
                canvas=Image.new('RGB',(1920,552),(23,27,33));draw=ImageDraw.Draw(canvas)
                for column,layer in enumerate(layers):
                    image=Image.open(source/layer/view/f'{frame:04}.png').convert('RGB').resize((640,480))
                    canvas.paste(image,(640*column,72))
                    draw.text((640*column+16,8),title[layer],font=font,fill='white')
                    draw.text((640*column+16,38),f'{view} | frame {frame:03} | {frame/20:.2f}s | source LEFT, Toki RIGHT',font=small,fill='#bcc8d9')
                if frame==count//2:canvas.save(out/(view+'.png'))
                encoder.stdin.write(canvas.tobytes())
        finally:encoder.stdin.close()
        if encoder.wait()!=0:raise RuntimeError('Video encode failed')
    data=json.loads((source/'frames.json').read_text())
    summary={}
    for layer in layers:
        frames=[r for r in data['frames'] if r['layer']==layer and r['frame']>=10]
        tilts=[r['pose']['headTilt'] for r in frames]
        summary[layer]=dict(frames=len(frames),directionErrorMax=max(r['directionError'] for r in frames),inferredHeadTiltMin=min(tilts),inferredHeadTiltMax=max(tilts),
                           neckUsesBindingDelta=any(r['pose'].get('neckUsesBindingDelta',False) for r in frames),
                           neckRotationErrorMax=max(r['pose'].get('neckRotationError',0) for r in frames),
                           neckSegmentDifferenceMax=max(r['pose'].get('neckSegmentDifference',0) for r in frames),
                           note='Head tilt is inferred, not a measured face normal. In v3 the neck is evaluated as binding-relative rotation; its unlike source/target segment directions are reported separately.')
    save(out/'summary.json',dict(source=data['source'],unityCapture=str(source),metrics=summary))
    videos=''.join(f'<h2>{v}</h2><video src="{v}.mp4" controls loop preload="metadata"></video>' for v in ['front','side','back'])
    import html
    (out/'index.html').write_text('''<!doctype html><meta charset="utf-8"><title>Toki foundation inspection</title><style>body{background:#171b21;color:#e5e9f0;font:17px sans-serif;margin:32px auto;max-width:1440px}video{width:100%}p{line-height:1.6}h2{font-size:20px}</style><h1>同一原始样本：基础传递 / 旧原始 / 旧修正</h1><p>'''+html.escape(description)+'''</p><p>原始输出未重抽。每列左侧是官方位置恢复的源骨架，右侧是实际 Unity 角色。橙色为左侧，蓝色为右侧，白色为中央链。</p><p>基础路径关闭比例、避碰、接地和显式头部目标。异常姿态保留用于定位，不代表视觉验收通过。脸部和手掌扭转没有观测真值。</p>'''+videos,encoding='utf-8')

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--capture',required=True);p.add_argument('--output',required=True);p.add_argument('--description',default='这是此前 30 秒演出的第 5 段轻微律动，不是拳击指令。');a=p.parse_args();build(Path(a.capture),Path(a.output),a.description)
