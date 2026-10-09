"""Encode captured camera frames with their actual timestamps; do not speed up slow runs."""
import argparse
from pathlib import Path
import subprocess
import imageio_ffmpeg


def encode(folder):
    entries=sorted(folder.glob('*.png'))
    stamp=folder/'timestamps.txt'
    if not entries or not stamp.exists():return False
    output=folder.parent/(folder.name+'.mp4')
    if output.exists() and output.stat().st_mtime>=stamp.stat().st_mtime:return False
    times=[float(s) for s in stamp.read_text().splitlines()]
    if len(times)!=len(entries):raise ValueError('Frame/timestamp mismatch: '+str(folder))
    concat=[]
    for i,p in enumerate(entries):
        concat.append("file '"+p.resolve().as_posix().replace("'","'\\''")+"'")
        concat.append('duration '+str(times[i+1]-times[i] if i+1<len(times) else .1))
    concat.append(concat[-2])
    manifest=folder/'frames.txt';manifest.write_text('\n'.join(concat),encoding='utf8')
    subprocess.run([imageio_ffmpeg.get_ffmpeg_exe(),'-y','-loglevel','error','-safe','0','-f','concat','-i',str(manifest),
        '-vsync','vfr','-c:v','libx264','-threads','2','-pix_fmt','yuv420p','-crf','21',str(output)],check=True)
    return True


if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('root');a=p.parse_args()
    count=sum(encode(f) for f in Path(a.root).rglob('frames*') if f.is_dir())
    print('Encoded',count,'recordings')
