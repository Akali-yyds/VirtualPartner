"""Bounded DiP probe; explicit preconditions rather than a fake current-pose prefix."""
from pathlib import Path
import json


def generate(args, text, out):
    root=Path(args.root)/'dip'
    checkpoint=root/'save/DiP_no-target_10steps_context20_predict40/model000600343.pt'
    if not checkpoint.exists() or checkpoint.stat().st_size!=232011477:
        raise FileNotFoundError('Official DiP checkpoint unavailable or incomplete')
    if args.steps not in (5,10):
        raise ValueError('DiP probe supports only 5/10 steps')
    # A known data prefix can verify model inference, but must never be reported as live character continuation.
    from dip_inference import run
    return run(args,text,out,checkpoint)
