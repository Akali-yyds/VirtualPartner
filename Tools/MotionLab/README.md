# Motion Lab (experimental, separate from PhoneOS)

Open `VirtualPartner > Motion Lab > Open experiment panel`. Create/open `Assets/Scenes/MotionLab.unity`, then enter Play Mode. The scene contains a copied character, its own coordinator/IK/Idle/TTS, and no Momotalk, LlmRelay, history or memory component. The Python semantic worker reads the existing ignored LLM configuration and sends only the isolated instruction plus the recorded semantic protocol.

The panel supports generation, replay, interruption, reset, audio synchronization, screenshots, and raw/BVH/foot-IK MoMask variants. Custom MoMask/DiP text should be English: their text encoders are English models; the fixed corpus includes reviewed English versions alongside the Chinese instructions. This is a feasibility panel, not a production low-latency service. Interactive generation currently starts a worker and includes cold startup; `batch.py` measures resident-model behavior separately.

## Local environments and pinned upstreams

External root: `F:/Project/MotionExperiments`. Python 3.10.21, PyTorch 2.1.2+cu118, torchvision 0.16.2+cu118, NumPy 1.23.5, SciPy 1.10.1. MoMask uses matplotlib 3.7.5 / einops 0.6.1; DiP uses transformers 4.38.2. Exact package lists accompany the evidence. Existing GPT-SoVITS Python was not changed.

- `momask`: https://github.com/EricGuo5513/momask-codes — `94a6636c9c463b7a9414c3401a6f1b67e6c51824` (MIT code).
- `dip`: https://github.com/GuyTevet/motion-diffusion-model — `ef8edce6a53c6ab19e53b4d4dcf15bc0bc60a778` (MIT code).
- MoMask HumanML weights: official `prepare/download_models.sh` Google Drive id `1vXS7SHJBgWPt59wupQ5UUzhFObrnGkQ0`; extract into `momask/checkpoints/t2m` without deleting other directories.
- DiP weights/args: https://huggingface.co/guytevet/CLoSD/tree/main/checkpoints/dip/DiP_no-target_10steps_context20_predict40 — `model000600343.pt`. Download only the model and args, not optimizer states.
- CLIP: https://github.com/openai/CLIP (MIT). DistilBERT: `distilbert/distilbert-base-uncased` (Apache-2.0).
- Code licenses do not grant additional motion dataset or body-model rights. No SMPL mesh is distributed or used: DiP's unused eager SMPL converter is replaced by an explicitly unavailable Cartesian-only converter; motion inference and HumanML recovery remain upstream code. Further redistribution of weights/data needs their own terms checked.

## Reproduce

Run commands from the repository root; model adapters switch to their external checkout internally.

```powershell
& F:/Project/MotionExperiments/momask-env/Scripts/python.exe Tools/MotionLab/test_contract.py
& F:/Project/MotionExperiments/momask-env/Scripts/python.exe Tools/MotionLab/lab.py --route fixture --output VirtualPartner/Library/MotionLab/fixture-new
& F:/Project/MotionExperiments/momask-env/Scripts/python.exe Tools/MotionLab/batch.py --route momask --output VirtualPartner/Library/MotionLab/momask-new
& F:/Project/MotionExperiments/dip-env/Scripts/python.exe Tools/MotionLab/batch.py --route dip --output VirtualPartner/Library/MotionLab/dip-new
& F:/Project/MotionExperiments/momask-env/Scripts/python.exe Tools/MotionLab/lab.py --route semantic --case 0 --config VirtualPartner/UserSettings/VirtualPartnerLlmConfig.json --output VirtualPartner/Library/MotionLab/semantic-new
```

Use a new output directory for each run. Seeds 11/22/33 apply to local generation. Semantic repetitions are repeat requests, **not deterministic DeepSeek seeds**. All raw responses, including unsupported/rejected instructions, remain available. MoMask saves raw positions before either official 100-iteration conversion; both conversions are independently timed. DiP probes 10 or 5 respaced steps with 20 context frames and 40 predicted frames, twice. It decodes root integration across the concatenated prefix and predictions, rather than independently resetting segment headings.

In Play Mode, editor review entrypoints:

- `MotionLabReview.Checks()` and `StartFinalReview()` (all selected samples plus real TTS comparisons).
- `MotionLabReview.CapturePrefix()` captures the actual character; pass the resulting `character-prefix.json` to `batch.py --prefix <absolute path>`. Morphology is converted to HumanML proportions before feature extraction; this is a prototype, not a claim of validated closed-loop control.
- `MotionLabReview.Start(<absolute suite folder>)` replays and records all generated samples.
- `MotionLabReview.StartAudioComparison("semantic" | "momask" | "dip")` runs actual generation and real TTS concurrently under action-first and audio-synchronized scheduling. Start existing services first. Do not edit C# during recordings (Unity domain reload interrupts them).

`media.py <results root>` encodes actual camera frames using their timestamps. `summarize.py <results root>` generates an unfiltered HTML index and JSON measurements. Camera recordings are silent; actual audio events, errors, cache status and underruns are separately recorded. Frame-time results captured while recording include screenshot overhead and must not be presented as normal application performance.

`analyze.py <results root>` compares unmodified source clips, foot travel, 40-frame DiP joins and official correction magnitude. `package.py <results root> <external destination>` preserves **all** outputs, including failed/interrupted samples and PNGs, outside disposable Unity Library and writes a SHA-256 manifest. Run packaging only after capture and encoding finish. Existing captures are never deleted.

`StartRemainingAudioReview()` resumes learned-route recordings and tests all three routes again without capture. A report's `completed` means the harness finished; success additionally requires real audio, relevant movement, and no error. The HTML index explicitly distinguishes blocked/failed and interrupted comparisons. Results before 2026-10-10 waited for the worker's warm benchmark and both BVH conversions before replay; the revised harness starts from the atomically published first raw MoMask output. Imports/model load and real TTS synthesis still remain on the cold path. Do not attribute that scheduling change to a faster model.

## Interfaces and limitations

- `MotionLabSemantic`: ordered steps, semantic goals, explicit completion; grounded through existing `spatialPose`, without altering StagePlan.
- `MotionLabClip` v1: HumanML22 parent order, 20 FPS frames of body-right/up/forward Cartesian positions. HumanML X is reflected once on export. No credentials in jobs or results.
- `MotionLabRetargeter`: maps bone directions, preserves local bone lengths, blends entry over 250 ms and releases through `ActionCoordinator`; unmodeled wrist twist remains unconstrained. The original and foot-IK variants remain separately selectable. It measures actual final transforms, source/target foot motion and a coarse hand/torso intersection diagnostic. It does **not** enforce every anatomical axis limit or provide physical balance.
- Existing Debug ownership wins. Semantic holds use the existing runtime; finite learned clips do not implement persistent holds or independent partial takeover. Their generation results are not interaction passes for cases 10/11. Case 12 tests external interruption, not a learned response to a live stop token.
- Foot travel is measured from the first stable playback sample, normalized by leg length. Direction error excludes the 250 ms entry blend. Coarse collision counts are diagnostics, not full self-collision certification.
- The semantic planner is an experimental set of grounded relations, not an unlimited action vocabulary. The per-character head/torso semantic axes and naturalness still need manual validation. Failures are surfaced, not replaced by an unrelated clip.
- `finalPositionErrorRelative` and `finalOrientationErrorDegrees` measure fresh targets against final scene transforms after the coordinator commits. Only reports with positive `finalGeometrySamples` have this measurement; an unmeasured zero is not evidence of zero error. Earlier `geometry` text describes solver-buffer samples and can include stale groups.
- Captured character-prefix frames are nominally 20 FPS but currently include editor scheduling gaps; prefix retiming and closed-loop continuity are not validated. The official fixture is imported as a calibration aid, not certified as a correct action for every user instruction.

All experiment source is additive. The production Momotalk → LlmRelay → StagePlan → Player chain, PhoneOS settings, histories and long-term memory formats are unchanged.
