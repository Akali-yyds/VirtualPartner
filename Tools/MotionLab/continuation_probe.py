"""Evaluate upstream masked editing on saved samples. Never enable it by assumption."""
import argparse
import json
from pathlib import Path
from types import SimpleNamespace
import time
import numpy as np
from lab import save,export_positions


def main():
    p=argparse.ArgumentParser();p.add_argument('--previous',required=True);p.add_argument('--next',required=True);p.add_argument('--output',required=True);a=p.parse_args()
    previous=Path(a.previous).resolve();nxt=Path(a.next).resolve();out=Path(a.output).resolve();out.mkdir(parents=True,exist_ok=True)
    import momask_adapter
    args=SimpleNamespace(root='F:/Project/MotionExperiments',device='cuda:0',seed=11,frames=40,repeats=1,postprocess=False)
    warm=out/'warmup';warm.mkdir(exist_ok=True);momask_adapter.generate(args,'A person stands calmly.',warm)
    import torch
    from utils.motion_process import recover_from_ric
    vq,res,trans,opt=momask_adapter._cached
    repo=Path(args.root)/'momask';mean=np.load(repo/'checkpoints/t2m'/opt.vq_name/'meta/mean.npy');std=np.load(repo/'checkpoints/t2m'/opt.vq_name/'meta/std.npy')
    pre=np.load(previous/'features-0.npy')[-20:];future=np.load(nxt/'features-0.npy')
    if len(pre)+len(future)>196:raise ValueError('Prefix plus continuation exceeds validated window; use a shorter comparison sample')
    source=np.concatenate([pre,future]);x=torch.from_numpy(((source-mean)/std).astype(np.float32))[None].cuda()
    text=json.loads((nxt/'motion.json').read_text())['caption'];length=torch.tensor([len(source)//4],device='cuda')
    torch.cuda.synchronize();start=time.perf_counter()
    with torch.inference_mode():
        tokens,_=vq.encode(x);mask=torch.ones_like(tokens[...,0],dtype=torch.bool);mask[:,:5]=False
        mids=trans.edit([text],tokens[...,0].clone(),length,timesteps=18,cond_scale=4,temperature=1,topk_filter_thres=.9,edit_mask=mask)
        codes=res.generate(mids,[text],length,temperature=1,cond_scale=2)
        features=vq.forward_decoder(codes).cpu().numpy()[0]*std+mean
        joints=recover_from_ric(torch.from_numpy(features).float(),22).numpy()
        source_joints=recover_from_ric(torch.from_numpy(source).float(),22).numpy()
    torch.cuda.synchronize();seconds=time.perf_counter()-start
    np.save(out/'features.npy',features);export_positions(joints,out/'with-prefix.json','momask-edit',text,11);export_positions(joints[20:],out/'motion.json','momask-edit-continuation',text,11)
    # Align root translations before measuring shape continuity; absolute heading remains measurable.
    source_shape=source_joints-source_joints[:,0:1];shape=joints-joints[:,0:1]
    prefix_error=float(np.linalg.norm(shape[:20]-source_shape[:20],axis=-1).max())
    independent_jump=float(np.linalg.norm(source_shape[20]-source_shape[19],axis=-1).max())
    edited_jump=float(np.linalg.norm(shape[20]-shape[19],axis=-1).max())
    save(out/'comparison.json',dict(editSeconds=seconds,prefixMaxShapeDriftMeters=prefix_error,independentBoundaryMaxMeters=independent_jump,editedBoundaryMaxMeters=edited_jump,
        defaultEnabled=False,reason='Source joint metrics only; target continuity and intent must be reviewed. Residual generation may alter preserved prefix.'))


if __name__=='__main__':main()
