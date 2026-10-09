import json
import os
import sys
import time
from pathlib import Path
from types import SimpleNamespace
_cached_model=None


def run(args,text,out,checkpoint):
    global _cached_model
    repo=Path(args.root)/'dip';sys.path.insert(0,str(repo));os.chdir(repo)
    import numpy as np
    import torch
    torch.set_num_threads(4);torch.manual_seed(args.seed);np.random.seed(args.seed)
    device=torch.device(args.device)
    if device.type=='cuda':torch.cuda.reset_peak_memory_stats()
    def sync():
        if device.type=='cuda':torch.cuda.synchronize()
    # hml_vec is already Cartesian after RIC recovery. SMPL is unused here; avoid downloading
    # licensed body meshes just for the official model's eager visualization constructor.
    import model.mdm as mdm
    class CartesianOnly:
        def __init__(self,*a,**k):self.smpl_model=torch.nn.Identity()
        def __call__(self,*a,**k):raise RuntimeError('SMPL conversion is deliberately unavailable in HumanML-only probe')
    mdm.Rotation2xyz=CartesianOnly
    from utils.model_util import get_model_args,load_saved_model
    from model.cfg_sampler import ClassifierFreeSampleModel
    from diffusion.respace import SpacedDiffusion,space_timesteps
    from diffusion import gaussian_diffusion as gd
    from data_loaders.humanml.scripts.motion_process import recover_from_ric,extract_features
    from data_loaders.humanml.utils.paramUtil import t2m_raw_offsets,t2m_kinematic_chain
    from data_loaders.humanml.common.skeleton import Skeleton
    from lab import export_positions,save
    options=SimpleNamespace(**json.loads(checkpoint.with_name('args.json').read_text()))
    tick=time.perf_counter()
    if _cached_model is None:
        model=mdm.MDM(**get_model_args(options,SimpleNamespace(dataset=SimpleNamespace())))
        load_saved_model(model,str(checkpoint),use_avg=True)
        model.to(device);model.eval();_cached_model=model
    else:model=_cached_model
    sync();load_time=time.perf_counter()-tick
    sampler=ClassifierFreeSampleModel(model)
    diffusion=SpacedDiffusion(use_timesteps=space_timesteps(10,[args.steps]),
        betas=gd.get_named_beta_schedule('cosine',10),model_mean_type=gd.ModelMeanType.START_X,
        model_var_type=gd.ModelVarType.FIXED_SMALL,loss_type=gd.LossType.MSE,rescale_timesteps=False,
        lambda_vel=0,lambda_rcxyz=0,lambda_fc=0,lambda_target_loc=0)
    mean=np.load(repo/'dataset/t2m_mean.npy');std=np.load(repo/'dataset/t2m_std.npy')
    if args.prefix:
        prefix_clip=json.loads(Path(args.prefix).read_text(encoding='utf-8'))
        positions=np.array([[[v['x'],v['y'],v['z']] for v in f['positions']] for f in prefix_clip['frames']],dtype=np.float32)
        positions[:,:,0]*=-1
        if positions.shape[0]<21:raise ValueError('Current-character prefix requires at least 21 measured frames at 20 FPS')
        # Retarget measured directions to the training skeleton proportions before encoding.
        reference_features=np.load(Path(args.root)/'momask/example_data/000612.npy')
        reference=recover_from_ric(torch.from_numpy(reference_features).float(),22).numpy()[0]
        skeleton=Skeleton(torch.from_numpy(t2m_raw_offsets),t2m_kinematic_chain,'cpu')
        target_offsets=skeleton.get_offsets_joints(torch.from_numpy(reference)).numpy()
        source_offsets=skeleton.get_offsets_joints(torch.from_numpy(positions[0])).numpy()
        ratio=(np.linalg.norm(target_offsets[4])+np.linalg.norm(target_offsets[7]))/(np.linalg.norm(source_offsets[4])+np.linalg.norm(source_offsets[7]))
        rotations=skeleton.inverse_kinematics_np(positions,[2,1,17,16],smooth_forward=True)
        skeleton.set_offset(torch.from_numpy(target_offsets))
        positions=skeleton.forward_kinematics_np(rotations,positions[:,0]*ratio)
        raw=extract_features(positions,.002,torch.from_numpy(t2m_raw_offsets),t2m_kinematic_chain,[2,1,17,16],[8,11],[7,10])
        prefix_type='converted-current-character; quality requires review'
    else:
        raw=np.load(Path(args.root)/'momask/example_data/000612.npy')
        prefix_type='official-dataset-example; NOT current-character continuation'
    prefix=torch.from_numpy(((raw[:20]-mean)/std).T.copy()).float()[None,:,None,:].to(device)
    np.save(out/'prefix-humanml.npy',raw[:20])
    times=[];outputs=[]
    initial_raw=raw[:20].copy()
    with torch.inference_mode():
        for segment in range(2):
            tick=time.perf_counter();embedding=model.encode_text([text]);sync();text_time=time.perf_counter()-tick
            y=dict(prefix=prefix,text=[text],text_embed=embedding,scale=torch.tensor([7.5],device=device),
                   mask=torch.ones((1,1,1,40),device=device,dtype=torch.bool),lengths=torch.tensor([40],device=device))
            tick=time.perf_counter()
            sample=diffusion.p_sample_loop(sampler,(1,263,1,40),clip_denoised=False,model_kwargs={'y':y},progress=False)
            sync();duration=time.perf_counter()-tick
            prefix=sample[...,-20:].clone()
            denorm=sample[0,:,0,:].T.cpu()*torch.from_numpy(std)+torch.from_numpy(mean)
            outputs.append(denorm.float().numpy());times.append(dict(textSeconds=text_time,generationSeconds=duration))
    # Integrate root velocity and heading once across prefix and both segments. Decoding
    # each segment independently loses the accumulated heading even if positions are aligned.
    features=np.concatenate([initial_raw]+outputs)
    all_positions=recover_from_ric(torch.from_numpy(features).float(),22).numpy()
    combined=all_positions[20:]
    np.save(out/'raw.npy',combined)
    timings=dict(loadSeconds=load_time,segments=times)
    export_positions(combined,out/'motion.json','dip',text,args.seed,timings)
    return dict(status='GENERATED',timings=timings,steps=args.steps,prefix=prefix_type,
        peakCudaAllocatedBytes=torch.cuda.max_memory_allocated() if device.type=='cuda' else 0,
        limitations=['Position output does not determine palm twist','Prefix morphology conversion and visual continuity require measured verification'])
