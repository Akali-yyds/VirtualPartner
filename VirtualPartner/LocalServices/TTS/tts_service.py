#!/usr/bin/env python3
import hashlib
import json
import os
import sys
import traceback
import urllib.error
import urllib.request
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path


SERVICE_ROOT = Path(__file__).resolve().parent
CONFIG_PATH = SERVICE_ROOT / "config.json"
DEFAULT_CONFIG = {
    "host": "127.0.0.1",
    "port": 8765,
    "version": "vp-tts-wrapper-gpt-sovits-v1",
    "upstream_tts_url": "http://127.0.0.1:9880/tts",
    "upstream_health_url": "http://127.0.0.1:9880/docs",
    "upstream_timeout_seconds": 180,
    "health_timeout_seconds": 5,
    "voices": {},
}


def load_config():
    config = dict(DEFAULT_CONFIG)
    if CONFIG_PATH.exists():
        with CONFIG_PATH.open("r", encoding="utf-8") as handle:
            loaded = json.load(handle)
        config.update(loaded)
    return config


def resolve_service_path(relative_path):
    if not relative_path:
        return None
    path = Path(relative_path)
    if path.is_absolute():
        return path
    return SERVICE_ROOT / path


def sha256_file(path):
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def sha256_text_file(path):
    return hashlib.sha256(path.read_text(encoding="utf-8").encode("utf-8")).hexdigest()


def clamp_speed(value):
    try:
        speed = float(value)
    except (TypeError, ValueError):
        speed = 1.0
    return max(0.5, min(2.0, speed))


def json_bytes(payload):
    return json.dumps(payload, ensure_ascii=False).encode("utf-8")


def read_json_body(handler):
    length = int(handler.headers.get("Content-Length", "0"))
    if length <= 0:
        return {}
    return json.loads(handler.rfile.read(length).decode("utf-8"))


def build_voice_status(config):
    voices = []
    for voice_id, voice in sorted(config.get("voices", {}).items()):
        ref_path = resolve_service_path(voice.get("ref_audio_path", ""))
        prompt_path = resolve_service_path(voice.get("prompt_text_path", ""))
        ok = True
        messages = []
        ref_hash = ""
        prompt_hash = ""

        if ref_path is None or not ref_path.exists():
            ok = False
            messages.append("reference audio missing")
        else:
            ref_hash = sha256_file(ref_path)

        if prompt_path is None or not prompt_path.exists():
            ok = False
            messages.append("prompt text missing")
        else:
            prompt_hash = sha256_text_file(prompt_path)

        voices.append(
            {
                "voiceId": voice_id,
                "ok": ok,
                "message": "; ".join(messages) if messages else "ok",
                "refAudioPath": str(ref_path) if ref_path is not None else "",
                "refAudioHash": ref_hash,
                "promptTextPath": str(prompt_path) if prompt_path is not None else "",
                "promptTextHash": prompt_hash,
                "promptLang": voice.get("prompt_lang", "ja"),
                "textLang": voice.get("text_lang", "zh"),
            }
        )
    return voices


def check_upstream(config):
    url = config.get("upstream_health_url") or config.get("upstream_tts_url", "").rsplit("/", 1)[0]
    timeout = float(config.get("health_timeout_seconds", 5))
    try:
        with urllib.request.urlopen(url, timeout=timeout) as response:
            return {
                "ok": True,
                "url": url,
                "message": f"HTTP {response.status}",
            }
    except Exception as exc:
        return {
            "ok": False,
            "url": url,
            "message": str(exc),
        }


def build_health(config):
    voices = build_voice_status(config)
    upstream = check_upstream(config)
    success = upstream["ok"] and any(voice["ok"] for voice in voices)
    return {
        "success": success,
        "message": "ok" if success else "degraded",
        "wrapper": {
            "ok": True,
            "version": config.get("version", DEFAULT_CONFIG["version"]),
            "message": "ok",
        },
        "upstream": upstream,
        "voices": voices,
    }


def build_upstream_request(config, body):
    voice_id = body.get("voiceId", "")
    voice = config.get("voices", {}).get(voice_id)
    if voice is None:
        raise ValueError(f"voiceId '{voice_id}' is not configured")

    ref_path = resolve_service_path(voice.get("ref_audio_path", ""))
    prompt_path = resolve_service_path(voice.get("prompt_text_path", ""))
    if ref_path is None or not ref_path.exists():
        raise ValueError(f"reference audio missing for voiceId '{voice_id}'")
    if prompt_path is None or not prompt_path.exists():
        raise ValueError(f"prompt text missing for voiceId '{voice_id}'")

    text = str(body.get("text", "")).strip()
    if not text:
        raise ValueError("text is required")

    return {
        "text": text,
        "text_lang": voice.get("text_lang", "zh"),
        "ref_audio_path": str(ref_path),
        "prompt_text": prompt_path.read_text(encoding="utf-8").strip(),
        "prompt_lang": voice.get("prompt_lang", "ja"),
        "text_split_method": voice.get("text_split_method", "cut5"),
        "batch_size": int(voice.get("batch_size", 1)),
        "media_type": voice.get("media_type", "wav"),
        "streaming_mode": bool(voice.get("streaming_mode", False)),
        "speed_factor": clamp_speed(body.get("speed", 1.0)),
    }


def post_upstream_tts(config, payload):
    data = json_bytes(payload)
    request = urllib.request.Request(
        config.get("upstream_tts_url", DEFAULT_CONFIG["upstream_tts_url"]),
        data=data,
        headers={"Content-Type": "application/json"},
        method="POST",
    )
    timeout = float(config.get("upstream_timeout_seconds", 180))
    with urllib.request.urlopen(request, timeout=timeout) as response:
        return response.read(), response.headers.get("Content-Type", "audio/wav")


class TtsHandler(BaseHTTPRequestHandler):
    server_version = "VirtualPartnerTTS/0.1"

    def do_GET(self):
        if self.path.split("?", 1)[0] != "/health":
            self.send_json(404, {"success": False, "message": "not found"})
            return

        try:
            self.send_json(200, build_health(load_config()))
        except Exception as exc:
            self.send_json(
                500,
                {
                    "success": False,
                    "message": str(exc),
                    "wrapper": {"ok": False, "version": DEFAULT_CONFIG["version"], "message": str(exc)},
                    "upstream": {"ok": False, "url": "", "message": "not checked"},
                    "voices": [],
                },
            )

    def do_POST(self):
        if self.path.split("?", 1)[0] != "/tts":
            self.send_json(404, {"success": False, "message": "not found"})
            return

        try:
            config = load_config()
            body = read_json_body(self)
            payload = build_upstream_request(config, body)
            audio, content_type = post_upstream_tts(config, payload)
            self.send_response(200)
            self.send_header("Content-Type", content_type or "audio/wav")
            self.send_header("Content-Length", str(len(audio)))
            self.end_headers()
            self.wfile.write(audio)
        except urllib.error.HTTPError as exc:
            self.send_json(
                exc.code,
                {
                    "success": False,
                    "message": exc.read().decode("utf-8", errors="replace"),
                },
            )
        except Exception as exc:
            self.send_json(
                400,
                {
                    "success": False,
                    "message": str(exc),
                },
            )

    def log_message(self, fmt, *args):
        sys.stdout.write("[TTS] " + fmt % args + "\n")
        sys.stdout.flush()

    def send_json(self, status, payload):
        data = json_bytes(payload)
        self.send_response(status)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Content-Length", str(len(data)))
        self.end_headers()
        self.wfile.write(data)


def main():
    config = load_config()
    host = config.get("host", DEFAULT_CONFIG["host"])
    port = int(config.get("port", DEFAULT_CONFIG["port"]))
    server = ThreadingHTTPServer((host, port), TtsHandler)
    print(f"VirtualPartner TTS wrapper listening on http://{host}:{port}")
    print(f"Upstream GPT-SoVITS: {config.get('upstream_tts_url')}")
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        print("Stopping TTS wrapper.")
    except Exception:
        traceback.print_exc()
    finally:
        server.server_close()


if __name__ == "__main__":
    main()
