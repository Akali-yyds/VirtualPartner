# VirtualPartner ASR Wrapper

Stage 2.13 uses this small local HTTP service so Unity can request real voice input without owning microphone recording or VAD logic.

Default service URL:

```text
http://127.0.0.1:8766
```

## First Setup

Run these from this folder:

```bat
setup_venv.bat
download_models.bat
```

`setup_venv` creates `.venv` and installs:

- `sherpa-onnx==1.13.2`
- `sounddevice==0.5.5`
- `numpy`

`download_models` downloads:

- `sherpa-onnx-streaming-zipformer-ctc-zh-int8-2025-06-30`
- `silero_vad.onnx`

The downloaded model files live under `models/` and are ignored by git.

## Start

```bat
start_asr_service.bat
```

Then check:

```text
GET http://127.0.0.1:8766/health
```

## Endpoints

- `GET /health`
- `POST /asr/start`
- `POST /asr/cancel`
- `GET /asr/status?sessionId=...`

Unity starts one ASR session, polls status, and fills the Momotalk input when the result is done.

## Notes

- Use the system default microphone.
- The service keeps the microphone input stream open while it is running, so the Windows microphone indicator can appear before Unity starts a voice session.
- Speech segmentation uses the silero VAD model (`models/vad/silero_vad.onnx`). Tune `vad.threshold`, `vad.min_silence_seconds`, and `vad.min_speech_seconds` in `config.json` if segmentation cuts too early or too late.
- Recording is not saved by default.
- If the service is missing, busy, or fails, Unity shows ASR error but normal text chat still works.
- If the service listens but returns empty text, check `/health` for `latestRms` and `peakRms` to confirm the microphone is capturing audio, then adjust the `vad` thresholds.
