"""Source-only facing audit. Reports contradictions; never transforms a clip."""
import argparse
import hashlib
import json
from pathlib import Path
import sys

import numpy as np
from foundation_audit import facing_conflicts
from lab import save


def describe(positions):
    p = np.asarray(positions)
    unit = lambda v: v / np.maximum(np.linalg.norm(v, axis=-1, keepdims=True), 1e-8)
    hip = unit(np.cross(p[:, 2] - p[:, 1], p[:, 9] - p[:, 0]))
    chest = unit(np.cross(p[:, 17] - p[:, 16], p[:, 12] - p[:, 9]))
    result = facing_conflicts(p)
    result['hipChestAngleMedianDegrees'] = float(np.median(np.degrees(np.arccos(np.clip(np.sum(hip * chest, axis=-1), -1, 1)))))
    result['frames'] = len(p)
    return result


def run(index, repo, output):
    sys.path.insert(0, str(repo))
    import torch
    from utils.motion_process import recover_from_ric
    from visualization import BVH_mod as BVH
    output.mkdir(parents=True, exist_ok=False)
    # Independent anatomical reference: official labelled BVH OFFSETS, with
    # identity rotations. Do not sample the animation embedded in that file.
    template_path = repo / 'visualization/data/template.bvh'
    template = BVH.load(str(template_path), need_quater=True)
    global_positions = template.offsets.copy()
    for joint, parent in enumerate(template.parents):
        if parent >= 0:
            global_positions[joint] += global_positions[parent]
    order = [0, 1, 5, 9, 2, 6, 10, 3, 7, 11, 4, 8, 12, 14, 18, 13, 15, 19, 16, 20, 17, 21]
    reference = global_positions[order][None].copy()
    reference[:, :, 0] *= -1
    rows = []
    manifest = json.loads(index.read_text(encoding='utf-8'))
    for case in manifest['cases']:
        for feature in sorted(Path(case['source']).glob('segments/*/features-0.npy')):
            data = torch.tensor(np.load(feature), dtype=torch.float32)
            official = recover_from_ric(data, 22).numpy()
            raw_file = feature.with_name('raw-0.npy')
            raw_error = float(np.max(np.abs(np.load(raw_file) - official))) if raw_file.exists() else None
            official[:, :, 0] *= -1
            exported = json.loads(feature.with_name('motion.json').read_text(encoding='utf-8'))
            positions = np.array([[[v[c] for c in 'xyz'] for v in f['positions']] for f in exported['frames']])
            rows.append(dict(case=case['case'], segment=feature.parent.name, source=str(feature),
                             sha256=hashlib.sha256(feature.read_bytes()).hexdigest(),
                             rawVsOfficialMax=raw_error, exportVsOfficialMax=float(np.max(np.abs(positions - official))),
                             **describe(official)))
    example_path = repo / 'example_data/000612.npy'
    example = recover_from_ric(torch.tensor(np.load(example_path), dtype=torch.float32), 22).numpy()
    example[:, :, 0] *= -1
    report = dict(policy='Direction cues are not facial ground truth. No automatic flip, rejection, resampling or correction.',
                  officialTemplate=dict(sha256=hashlib.sha256(template_path.read_bytes()).hexdigest(), **describe(reference)),
                  officialExample=dict(sha256=hashlib.sha256(example_path.read_bytes()).hexdigest(), **describe(example)), samples=rows)
    save(output / 'source-facing.json', report)
    print(json.dumps(dict(samples=len(rows), exportMax=max(r['exportVsOfficialMax'] for r in rows),
                          conflicts=[dict(case=r['case'], segment=r['segment'], fraction=r['fraction']) for r in rows if r['fraction'] > 0]), indent=2))


if __name__ == '__main__':
    p = argparse.ArgumentParser()
    p.add_argument('--index', type=Path, required=True)
    p.add_argument('--repo', type=Path, default=Path('F:/Project/MotionExperiments/momask'))
    p.add_argument('--output', type=Path, required=True)
    args = p.parse_args()
    run(args.index, args.repo, args.output)
