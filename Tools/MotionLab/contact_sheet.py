import argparse
from pathlib import Path
from PIL import Image,ImageDraw


def create(root):
    samples=sorted(p for p in root.iterdir() if p.is_dir() and (p/'result.json').exists())
    for page in range(0,len(samples),12):
        canvas=Image.new('RGB',(1280,4*210),(22,26,35));draw=ImageDraw.Draw(canvas)
        for i,sample in enumerate(samples[page:page+12]):
            frame_folders=sorted(p for p in sample.glob('frames*') if p.is_dir())
            frames=sorted(frame_folders[-1].glob('*.png')) if frame_folders else []
            x=(i%3)*426;y=(i//3)*210
            if frames:
                frame=frames[min(len(frames)-1,len(frames)//2)]
                with Image.open(frame) as image:canvas.paste(image.resize((426,180)),(x,y))
            draw.text((x+8,y+185),sample.name,fill='white')
        canvas.save(root/('contact-sheet-'+str(page//12+1)+'.jpg'))


if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('root');a=p.parse_args();create(Path(a.root))
