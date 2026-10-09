"""Isolated experiment jobs. Credentials are read only from the existing ignored config.

Outputs contain test prompts/results, never configuration or authorization headers.
Model repositories, weights and environments live outside the Unity repository.
"""
import argparse
import json
import os
from pathlib import Path
import sys
import time
import urllib.request
import urllib.error

HERE = Path(__file__).resolve().parent
PARENTS = [-1, 0, 0, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 9, 9, 12, 13, 14, 16, 17, 18, 19]


def save(path, value):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_suffix(path.suffix + '.tmp')
    temporary.write_text(json.dumps(value, ensure_ascii=False, indent=2), encoding='utf-8')
    os.replace(temporary, path)


def semantic(text, config_path):
    config = json.loads(Path(config_path).read_text(encoding='utf-8-sig'))
    endpoint = config.get('chatCompletionsUrl') or config['baseUrl'].rstrip('/') + '/v1/chat/completions'
    system = (HERE / 'semantic-system.txt').read_text(encoding='utf-8')
    payload = dict(model=config['model'], stream=True, temperature=0.2,
                   response_format={'type': 'json_object'},
                   messages=[{'role': 'system', 'content': system}, {'role': 'user', 'content': text}])
    request = urllib.request.Request(endpoint, data=json.dumps(payload).encode(),
        headers={'Authorization': 'Bearer ' + config['apiKey'], 'Content-Type': 'application/json'})
    start = time.perf_counter()
    first = None
    chunks = []
    with urllib.request.urlopen(request, timeout=90) as response:
        for line in response:
            if not line.startswith(b'data:'):
                continue
            data = line[5:].strip()
            if data == b'[DONE]':
                break
            event = json.loads(data)
            for choice in event.get('choices', []):
                chunk = choice.get('delta', {}).get('content') or ''
                if chunk:
                    if first is None:
                        first = time.perf_counter() - start
                    chunks.append(chunk)
    raw = ''.join(chunks)
    result = {'systemPrompt': system, 'raw': raw,
              'timings': {'firstToken': first, 'semanticComplete': time.perf_counter() - start}}
    try:
        result['semantic'] = json.loads(raw)
        result['status'] = 'GENERATED'
    except ValueError:
        result.update(status='FAILED', error='semantic_parse: response is not valid JSON')
    return result


def export_positions(positions, path, route, caption, seed, timings=None):
    import numpy as np
    positions = np.asarray(positions, dtype=np.float32)
    if positions.ndim != 3 or positions.shape[1:] != (22, 3) or not np.isfinite(positions).all():
        raise ValueError('Expected finite HumanML3D positions [frames,22,3]')
    # HumanML3D +X is anatomical left. Reflect X to Unity's body-right convention.
    positions = positions.copy()
    positions[:, :, 0] *= -1
    save(path, dict(version=1, route=route, caption=caption, seed=seed, fps=20,
        coordinates='body-right/up/forward; meters; HumanML22; X reflected from HumanML',
        parents=PARENTS, frames=[{'positions': [{'x': float(x), 'y': float(y), 'z': float(z)}
                                                for x, y, z in frame]} for frame in positions],
        timings=timings or {}))


def main():
    p = argparse.ArgumentParser()
    p.add_argument('--route', choices=['semantic', 'momask', 'dip', 'fixture'], required=True)
    p.add_argument('--output', required=True)
    p.add_argument('--config')
    p.add_argument('--text', default='')
    p.add_argument('--case', type=int)
    p.add_argument('--seed', type=int, default=11)
    p.add_argument('--root', default='F:/Project/MotionExperiments')
    p.add_argument('--device', default='cpu')
    p.add_argument('--steps', type=int, default=10)
    p.add_argument('--prefix')
    args = p.parse_args()
    os.environ.setdefault('HF_HOME',str(Path(args.root)/'hf-cache'))
    out = Path(args.output).resolve()
    out.mkdir(parents=True, exist_ok=True)
    case = json.loads((HERE / 'cases.json').read_text(encoding='utf-8'))[args.case] if args.case is not None else None
    text = case['text' if args.route == 'semantic' else 'english'] if case else args.text
    result = dict(route=args.route, seed=args.seed, input=text, case=case,
                  status='RUNNING', startedUtc=time.strftime('%Y-%m-%dT%H:%M:%SZ', time.gmtime()))
    save(out / 'result.json', result)
    start = time.perf_counter()
    monitor = None
    try:
        from resources import ResourceMonitor
        monitor=ResourceMonitor();monitor.__enter__()
        if args.route == 'semantic':
            result.update(semantic(text, args.config))
            if 'semantic' in result:
                save(out / 'semantic.json', result['semantic'])
        elif args.route == 'momask':
            from momask_adapter import generate
            result.update(generate(args, text, out))
        elif args.route == 'dip':
            from dip_adapter import generate
            result.update(generate(args, text, out))
        else:
            sys.path.insert(0, str(Path(args.root) / 'momask'))
            import numpy as np
            import torch
            from utils.motion_process import recover_from_ric
            source = Path(args.root) / 'momask/example_data/000612.npy'
            features = np.load(source)
            xyz = recover_from_ric(torch.from_numpy(features).float(), 22).numpy()
            export_positions(xyz, out / 'motion.json', 'fixture', 'Official example 000612; calibration only', 0)
            result.update(status='GENERATED', fixture=str(source), frames=len(xyz))
    except Exception as error:
        # Never include a request/config repr or a server body that could echo secrets.
        message = str(error)
        if args.route == 'semantic':
            message = 'HTTP ' + str(error.code) if isinstance(error, urllib.error.HTTPError) else type(error).__name__
        result.update(status='FAILED', error=message, errorType=type(error).__name__)
    result['totalSeconds'] = time.perf_counter() - start
    if monitor is not None:
        monitor.__exit__();save(out/'resources.json',monitor.samples)
    save(out / 'result.json', result)
    print(json.dumps({k: result[k] for k in ('status', 'route', 'totalSeconds')}, ensure_ascii=False))
    return 0 if result['status'] == 'GENERATED' else 1


if __name__ == '__main__':
    raise SystemExit(main())
