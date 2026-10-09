"""Adapter for pinned official MoMask. Keep raw positions and measure both official conversions."""
import os
import sys
import time
from pathlib import Path
from types import SimpleNamespace
_cached = None


def generate(args, text, out):
    global _cached
    repo = Path(args.root) / 'momask'
    sys.path.insert(0, str(repo))
    os.chdir(repo)
    import numpy as np
    import torch
    import clip
    if not hasattr(clip, '_motionlab_original_load'):
        clip._motionlab_original_load = clip.load
        clip.load = lambda name, *a, **k: clip._motionlab_original_load(name, *a, **dict(k, download_root=str(Path(args.root)/'clip-cache')))
    from gen_t2m import load_vq_model, load_trans_model, load_res_model
    from utils.get_opt import get_opt
    from utils.fixseed import fixseed
    from utils.motion_process import recover_from_ric
    from visualization.joints2bvh import Joint2BVHConvertor
    from lab import export_positions
    torch.set_num_threads(4)
    fixseed(args.seed)
    device = torch.device(args.device)
    if device.type == 'cuda':
        torch.cuda.reset_peak_memory_stats()
    def sync():
        if device.type == 'cuda':
            torch.cuda.synchronize()
    timings = {}
    start = time.perf_counter()
    if _cached is None:
        name = 't2m_nlayer8_nhead6_ld384_ff1024_cdp0.1_rvq6ns'
        model_opt = get_opt(str(repo/'checkpoints/t2m'/name/'opt.txt'), device=device)
        vq_opt = get_opt(str(repo/'checkpoints/t2m'/model_opt.vq_name/'opt.txt'), device=device)
        vq_opt.dim_pose = 263
        vq, vq_opt = load_vq_model(vq_opt)
        model_opt.num_tokens, model_opt.num_quantizers, model_opt.code_dim = vq_opt.nb_code, vq_opt.num_quantizers, vq_opt.code_dim
        opt = SimpleNamespace(device=device, name=name)
        residual_name = 'tres_nlayer8_ld384_ff1024_rvq6ns_cdp0.2_sw'
        res_opt = get_opt(str(repo/'checkpoints/t2m'/residual_name/'opt.txt'), device=device)
        res = load_res_model(res_opt, vq_opt, opt)
        trans = load_trans_model(model_opt, opt, 'latest.tar')
        for model in (vq,res,trans):
            model.eval().to(device)
        _cached=(vq,res,trans,model_opt)
    else:
        vq,res,trans,model_opt=_cached
    sync()
    timings['loadSeconds'] = time.perf_counter()-start
    mean = np.load(repo/'checkpoints/t2m'/model_opt.vq_name/'meta/mean.npy')
    std = np.load(repo/'checkpoints/t2m'/model_opt.vq_name/'meta/std.npy')
    length = torch.tensor([24], device=device, dtype=torch.long)  # 96 frames, 4.8 seconds
    # Measure actual text calls nested within official generation without replacing them.
    text_seconds = [0.0]
    originals=[]
    for model in (trans,res):
        original = model.encode_text
        originals.append(original)
        def timed_text(value, original=original):
            sync(); tick=time.perf_counter(); result=original(value); sync()
            text_seconds[0] += time.perf_counter()-tick
            return result
        model.encode_text=timed_text
    all_times=[]
    # One cold inference plus a second warm inference, identical seed/input. Save both.
    for repeat in range(2):
        fixseed(args.seed)
        sync();tick=time.perf_counter();text_seconds[0]=0
        with torch.inference_mode():
            indices=trans.generate([text],length,timesteps=18,cond_scale=4,temperature=1,topk_filter_thres=.9,gsample=False)
            indices=res.generate(indices,[text],length,temperature=1,cond_scale=5)
            decoded=vq.forward_decoder(indices).cpu().numpy()[0]
            features=decoded*std+mean
            joints=recover_from_ric(torch.from_numpy(features).float(),22).numpy()
        sync();elapsed=time.perf_counter()-tick
        all_times.append({'total':elapsed,'text':text_seconds[0],'generationExcludingText':elapsed-text_seconds[0]})
        np.save(out/('raw-'+str(repeat)+'.npy'),joints)
        if repeat==0:
            # Publish the first valid motion atomically. Replay need not wait for the
            # warm benchmark or optional BVH fitting; those remain separate evidence.
            export_positions(joints,out/'motion.json','momask-raw',text,args.seed,{'firstInference':all_times[0]})
    timings['inference']=all_times
    for model, original in zip((trans,res),originals):
        model.encode_text=original
    tick=time.perf_counter();converter=Joint2BVHConvertor()
    _,converted=converter.convert(joints,str(out/'official-no-foot-ik.bvh'),iterations=100,foot_ik=False)
    timings['bvhNoFootIkSeconds']=time.perf_counter()-tick
    tick=time.perf_counter()
    _,corrected=converter.convert(joints,str(out/'official-foot-ik.bvh'),iterations=100,foot_ik=True)
    timings['bvhFootIkSeconds']=time.perf_counter()-tick
    export_positions(converted,out/'converted.json','momask-bvh',text,args.seed)
    export_positions(corrected,out/'foot-ik.json','momask-foot-ik',text,args.seed)
    peak=torch.cuda.max_memory_allocated() if device.type=='cuda' else 0
    return dict(status='GENERATED',timings=timings,peakCudaAllocatedBytes=peak,
        frames=len(joints),device=str(device),motionSeconds=len(joints)/20,
        correctionMaxMeters=float(np.linalg.norm(corrected-joints,axis=-1).max()),
        limitations=['Finite clip; no persistent interaction semantics','Palm twist is unobserved in position output'])
