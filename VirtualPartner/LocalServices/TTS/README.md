# VirtualPartner TTS Wrapper

Stage 2.11 uses this small local HTTP wrapper so Unity does not depend on GPT-SoVITS API details.

Start GPT-SoVITS `api_v2.py` first on `127.0.0.1:9880`, then start this wrapper:

```bash
python tts_service.py
```

Endpoints:

- `GET /health`
- `POST /tts`

The default `toki_default` voice maps to `voices/toki/s6_g8/CH0187_MemorialLobby_1_1.wav` and the same-name `.txt` prompt.

## Streaming GPT-SoVITS

Unity uses buffered wav for short lines and streaming TTS for longer lines. A streaming request looks like:

```json
{
  "voiceId": "toki_default",
  "text": "Hello.",
  "speed": 1.0,
  "format": "raw",
  "stream": true,
  "streamingMode": 1
}
```

The wrapper forwards GPT-SoVITS chunks as HTTP chunked `audio/raw`. The raw audio is PCM16 little-endian, mono, 32000 Hz. Unity plays it through a streaming `AudioClip`.

Default mode is `streamingMode: 1` for better quality. `streamingMode: 3` is available for lower latency with lower quality.

## Kivo voice downloader

Download Kivo Wiki voices into the same flat `.ogg` + `.txt` layout:

```bash
python download_kivo_voices.py https://kivo.wiki/data/character/77 --server jp --output voices/toki/default
```

Server aliases:

- `jp`, `ja`, `日服`
- `cn`, `zh`, `国服`
- `kr`, `ko`, `韩服`

By default the script downloads every voice category from the character voice
page and keeps existing files. Use `--categories battle,lobby,event` to filter,
`--overwrite` to replace existing files, and `--dry-run` to preview the output.
