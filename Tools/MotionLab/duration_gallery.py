"""Encode uniform 30 FPS review captures and build the two-route comparison page."""
import argparse
import json
from pathlib import Path
import subprocess
import imageio_ffmpeg
from lab import HERE,save


def build(root):
    cases=json.loads((HERE/'duration_cases.json').read_text(encoding='utf8'))
    samples={};rows=[]
    for route in ['momask','dip']:
        for case in cases:
            folder=root/route/case['id'];result_path=folder/'result.json'
            if not result_path.exists():continue
            result=json.loads(result_path.read_text(encoding='utf8'));capture=folder/'capture.json';video=folder/'review.mp4'
            if capture.exists() and (not video.exists() or video.stat().st_mtime<capture.stat().st_mtime):
                subprocess.run([imageio_ffmpeg.get_ffmpeg_exe(),'-y','-loglevel','error','-framerate','30','-i',str(folder/'review-30fps/%05d.png'),'-c:v','libx264','-threads','2','-crf','20','-pix_fmt','yuv420p',str(video)],check=True)
            sample=dict(case=case,route=route,result=result,video=video.relative_to(root).as_posix() if video.exists() else None)
            for name in ['unity-timing','realtime-playback','capture']:
                p=folder/(name+'.json');sample[name]=json.loads(p.read_text(encoding='utf8')) if p.exists() else None
            samples[route+'/'+case['id']]=sample
            timing=result.get('timings',{})
            model=sum(s['generationSeconds']+s['textSeconds'] for s in timing.get('segments',[])) if route=='dip' else sum(s['total'] for s in timing.get('inference',[]))
            rows.append(dict(id=case['id'],category=case['category'],duration=case['seconds'],route=route,status=result['status'],
                warmRequestSeconds=result.get('warmRequestSeconds'),modelPipelineSeconds=model,unity=sample['unity-timing']))
    save(root/'comparison-summary.json',rows)
    template=(HERE/'duration_gallery.html').read_text(encoding='utf8')
    (root/'index.html').write_text(template.replace('__DATA__',json.dumps(dict(cases=cases,samples=samples),ensure_ascii=False).replace('<','\\u003c')),encoding='utf8')
    report=['# 短、中、长动作对照（2026-10-10）','','同一进程预热三种长度后计时。每条指令每路线一个样本，固定种子 11，不重抽优选。MoMask 18 步，DiP 10 步。','','| 指令 | 长度 | 路线 | 暖请求至动作文件（秒） | 模型流水线（秒） | Unity 载入至首动作（秒） |','|---|---:|---|---:|---:|---:|']
    for r in rows:
        number=lambda v:'—' if v is None else f'{v:.3f}'
        report.append('| '+r['id']+' | '+str(r['duration'])+' | '+r['route']+' | '+number(r['warmRequestSeconds'])+' | '+number(r['modelPipelineSeconds'])+' | '+number((r['unity'] or {}).get('firstBodyMotionSeconds'))+' |')
    report+=['','计时包括文本编码、动作生成、坐标转换/前缀处理、JSON 导出，排除模型加载与预热；不运行 TTS。Unity 重播单独计时，不能将相加值当成已测量的在线端到端延迟。MoMask 不等待可选 BVH 拟合；两路线使用同一个原始位置重定向器。','','DiP 每 2 秒生成下一段，同一完整英文指令重复输入各段，没有人为拆动作时间表；长动作是否真正完成顺序由录像判断。当前角色前缀沿用已有适配资源。MoMask 每档一次性生成整段。','','录像以 Unity captureFramerate=30 离线采样，原模型为 20 FPS；这提高录制完整性，没有增加模型本身的运动细节。片尾额外 0.5 秒是协调器交回 Idle，不属于模型生成。冷启动日志、预热输出、原始数组、诊断和每帧截图均保留。单种子小样本仅供本轮视觉选择，不代表质量稳定性。']
    (root/'README.md').write_text('\n'.join(report)+'\n',encoding='utf8')
    print('Built',len(samples),'review samples',root/'index.html')


if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('root');a=p.parse_args();build(Path(a.root))
