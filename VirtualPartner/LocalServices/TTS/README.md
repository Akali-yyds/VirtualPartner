# VirtualPartner TTS Wrapper

Stage 2.11 uses this small local HTTP wrapper so Unity does not depend on GPT-SoVITS API details.

Start GPT-SoVITS `api_v2.py` first on `127.0.0.1:9880`, then start this wrapper:

```bash
python tts_service.py
```

Endpoints:

- `GET /health`
- `POST /tts`

The default `toki_default` voice maps to `voices/toki/default/ch0187_minieventboxshop_5.ogg` and the same-name `.txt` prompt.
