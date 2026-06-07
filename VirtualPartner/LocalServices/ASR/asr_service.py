#!/usr/bin/env python3
import json
import sys
import threading
import time
import traceback
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import parse_qs, urlparse

import numpy as np


SERVICE_ROOT = Path(__file__).resolve().parent
CONFIG_PATH = SERVICE_ROOT / "config.json"
DEFAULT_CONFIG = {
    "host": "127.0.0.1",
    "port": 8766,
    "version": "vp-asr-sherpa-onnx-v1",
    "engine": "sherpa-onnx",
    "sample_rate": 16000,
    "input_device": "default",
    "max_record_seconds": 15,
    "status_poll_seconds": 0.25,
    "model": {
        "type": "online-zipformer2-ctc",
        "model_path": "models/asr/sherpa-onnx-streaming-zipformer-ctc-zh-int8-2025-06-30/model.int8.onnx",
        "tokens_path": "models/asr/sherpa-onnx-streaming-zipformer-ctc-zh-int8-2025-06-30/tokens.txt",
        "num_threads": 2,
        "provider": "cpu",
    },
    "vad": {
        "model_path": "models/vad/silero_vad.onnx",
        "threshold": 0.5,
        "min_silence_seconds": 0.8,
        "min_speech_seconds": 0.25,
    },
}


STATE_LOCK = threading.Lock()
STATE = {
    "session_id": "",
    "status": "idle",
    "text": "",
    "error": "",
    "duration": 0.0,
    "engine": DEFAULT_CONFIG["engine"],
    "started_at": 0.0,
    "latest_rms": 0.0,
    "peak_rms": 0.0,
    "speech_detected": False,
}
SESSION_SEQUENCE = 0
RUNTIME_LOCK = threading.Lock()
RUNTIME = {
    "config_signature": "",
    "ready": False,
    "warming": False,
    "error": "",
    "recognizer": None,
    "warmup_started_at": 0.0,
    "warmup_finished_at": 0.0,
}
MIC_LOCK = threading.Lock()
MIC_RUNTIME = {
    "ready": False,
    "warming": False,
    "error": "",
    "thread_started": False,
    "session_id": "",
    "cancel_requested": False,
    "started_at": 0.0,
    "peak_rms": 0.0,
    "speech_detected": False,
}


def load_config():
    config = dict(DEFAULT_CONFIG)
    config["model"] = dict(DEFAULT_CONFIG["model"])
    config["vad"] = dict(DEFAULT_CONFIG["vad"])
    if CONFIG_PATH.exists():
        with CONFIG_PATH.open("r", encoding="utf-8") as handle:
            loaded = json.load(handle)
        for key, value in loaded.items():
            if key in ("model", "vad") and isinstance(value, dict):
                config[key].update(value)
            else:
                config[key] = value
    return config


def resolve_service_path(relative_path):
    if not relative_path:
        return None
    path = Path(str(relative_path))
    if path.is_absolute():
        return path
    return SERVICE_ROOT / path


def json_bytes(payload):
    return json.dumps(payload, ensure_ascii=False).encode("utf-8")


def read_json_body(handler):
    length = int(handler.headers.get("Content-Length", "0"))
    if length <= 0:
        return {}
    return json.loads(handler.rfile.read(length).decode("utf-8"))


def get_input_device(config):
    value = config.get("input_device", "default")
    if value is None or str(value).strip().lower() in ("", "default"):
        return None
    try:
        return int(value)
    except (TypeError, ValueError):
        return str(value)


def check_imports():
    try:
        import sherpa_onnx  # noqa: F401
        import sounddevice  # noqa: F401
        return True, "ok"
    except Exception as exc:
        return False, str(exc)


def check_microphone(config):
    try:
        import sounddevice as sd

        device = get_input_device(config)
        info = sd.query_devices(device, "input")
        return {
            "ok": True,
            "device": "default" if device is None else str(device),
            "name": str(info.get("name", "")),
            "message": "ok",
        }
    except Exception as exc:
        return {
            "ok": False,
            "device": str(config.get("input_device", "default")),
            "name": "",
            "message": str(exc),
        }


def build_health(config):
    imports_ok, imports_message = check_imports()
    model_path = resolve_service_path(config.get("model", {}).get("model_path", ""))
    tokens_path = resolve_service_path(config.get("model", {}).get("tokens_path", ""))
    vad_path = resolve_service_path(config.get("vad", {}).get("model_path", ""))
    model_ok = model_path is not None and model_path.exists() and tokens_path is not None and tokens_path.exists()
    vad_ok = vad_path is not None and vad_path.exists()
    microphone = check_microphone(config)

    with STATE_LOCK:
        active_session = STATE["session_id"] if STATE["status"] in ("listening", "recognizing") else ""
        current_status = STATE["status"]

    runtime = get_runtime_snapshot()
    mic_runtime = get_microphone_snapshot()
    microphone_ok = microphone["ok"] and mic_runtime["ready"]
    success = imports_ok and model_ok and vad_ok and microphone_ok and runtime["ready"]
    return {
        "success": success,
        "message": "ok" if success else runtime["error"] or "degraded",
        "wrapper": {
            "ok": True,
            "version": config.get("version", DEFAULT_CONFIG["version"]),
            "message": "ok",
        },
        "engine": {
            "ok": imports_ok,
            "name": config.get("engine", DEFAULT_CONFIG["engine"]),
            "message": imports_message,
        },
        "model": {
            "ok": model_ok,
            "type": config.get("model", {}).get("type", ""),
            "modelPath": str(model_path) if model_path is not None else "",
            "tokensPath": str(tokens_path) if tokens_path is not None else "",
            "message": "ok" if model_ok else "model or tokens missing",
        },
        "vad": {
            "ok": vad_ok,
            "modelPath": str(vad_path) if vad_path is not None else "",
            "message": "ok" if vad_ok else "vad model missing",
        },
        "microphone": microphone,
        "audioInput": {
            "ready": mic_runtime["ready"],
            "warming": mic_runtime["warming"],
            "message": mic_runtime["error"] or ("ready" if mic_runtime["ready"] else "not ready"),
        },
        "activeSession": active_session,
        "currentStatus": current_status,
        "runtime": {
            "ready": runtime["ready"],
            "warming": runtime["warming"],
            "message": runtime["error"] or ("ready" if runtime["ready"] else "not ready"),
            "warmupSeconds": max(0.0, runtime["warmup_finished_at"] - runtime["warmup_started_at"])
            if runtime["warmup_finished_at"] > 0
            else 0.0,
        },
        "latestRms": STATE["latest_rms"],
        "peakRms": STATE["peak_rms"],
        "speechDetected": STATE["speech_detected"],
    }


def set_state(
    session_id=None,
    status=None,
    text=None,
    error=None,
    duration=None,
    latest_rms=None,
    peak_rms=None,
    speech_detected=None,
):
    with STATE_LOCK:
        if session_id is not None:
            STATE["session_id"] = session_id
        if status is not None:
            STATE["status"] = status
        if text is not None:
            STATE["text"] = text
        if error is not None:
            STATE["error"] = error
        if duration is not None:
            STATE["duration"] = duration
        if latest_rms is not None:
            STATE["latest_rms"] = latest_rms
        if peak_rms is not None:
            STATE["peak_rms"] = peak_rms
        if speech_detected is not None:
            STATE["speech_detected"] = speech_detected


def get_status_payload(session_id):
    with STATE_LOCK:
        if session_id and STATE["session_id"] and session_id != STATE["session_id"]:
            return {
                "success": False,
                "status": "failed",
                "text": "",
                "duration": 0.0,
            "engine": STATE["engine"],
            "error": "sessionId is not current",
            "latestRms": STATE["latest_rms"],
            "peakRms": STATE["peak_rms"],
            "speechDetected": STATE["speech_detected"],
        }

        return {
            "success": True,
            "sessionId": STATE["session_id"],
            "status": STATE["status"],
            "text": STATE["text"],
            "duration": STATE["duration"],
            "engine": STATE["engine"],
            "error": STATE["error"],
            "latestRms": STATE["latest_rms"],
            "peakRms": STATE["peak_rms"],
            "speechDetected": STATE["speech_detected"],
        }


def make_recognizer(config):
    import sherpa_onnx

    model_config = config.get("model", {})
    model_path = str(resolve_service_path(model_config.get("model_path", "")))
    tokens_path = str(resolve_service_path(model_config.get("tokens_path", "")))
    num_threads = int(model_config.get("num_threads", 2))
    provider = str(model_config.get("provider", "cpu"))

    if hasattr(sherpa_onnx.OnlineRecognizer, "from_zipformer2_ctc"):
        return sherpa_onnx.OnlineRecognizer.from_zipformer2_ctc(
            tokens=tokens_path,
            model=model_path,
            num_threads=num_threads,
            provider=provider,
        )

    recognizer_config = sherpa_onnx.OnlineRecognizerConfig(
        model_config=sherpa_onnx.OnlineModelConfig(
            tokens=tokens_path,
            num_threads=num_threads,
            provider=provider,
            ctc=sherpa_onnx.OnlineCtcModelConfig(model=model_path),
        ),
        decoding_method="greedy_search",
    )
    return sherpa_onnx.OnlineRecognizer(recognizer_config)


def get_config_signature(config):
    model_config = config.get("model", {})
    return json.dumps(
        {
            "model_path": str(resolve_service_path(model_config.get("model_path", ""))),
            "tokens_path": str(resolve_service_path(model_config.get("tokens_path", ""))),
            "num_threads": int(model_config.get("num_threads", 2)),
            "provider": str(model_config.get("provider", "cpu")),
        },
        ensure_ascii=False,
        sort_keys=True,
    )


def get_runtime_snapshot():
    with RUNTIME_LOCK:
        return dict(RUNTIME)


def warmup_runtime(config):
    signature = get_config_signature(config)
    with RUNTIME_LOCK:
        if RUNTIME["ready"] and RUNTIME["config_signature"] == signature:
            return True, ""
        if RUNTIME["warming"]:
            return False, "ASR runtime is warming up."
        RUNTIME["warming"] = True
        RUNTIME["ready"] = False
        RUNTIME["error"] = ""
        RUNTIME["warmup_started_at"] = time.time()
        RUNTIME["warmup_finished_at"] = 0.0

    try:
        recognizer = make_recognizer(config)
        with RUNTIME_LOCK:
            RUNTIME["recognizer"] = recognizer
            RUNTIME["config_signature"] = signature
            RUNTIME["ready"] = True
            RUNTIME["warming"] = False
            RUNTIME["error"] = ""
            RUNTIME["warmup_finished_at"] = time.time()
        return True, ""
    except Exception as exc:
        with RUNTIME_LOCK:
            RUNTIME["recognizer"] = None
            RUNTIME["ready"] = False
            RUNTIME["warming"] = False
            RUNTIME["error"] = str(exc)
            RUNTIME["warmup_finished_at"] = time.time()
        return False, str(exc)


def warmup_runtime_background(config):
    threading.Thread(target=warmup_runtime, args=(config,), daemon=True).start()


def get_ready_recognizer():
    with RUNTIME_LOCK:
        if not RUNTIME["ready"]:
            return None
        return RUNTIME["recognizer"]


def get_microphone_snapshot():
    with MIC_LOCK:
        return {
            "ready": MIC_RUNTIME["ready"],
            "warming": MIC_RUNTIME["warming"],
            "error": MIC_RUNTIME["error"],
            "session_id": MIC_RUNTIME["session_id"],
        }


def start_microphone_background(config):
    with MIC_LOCK:
        if MIC_RUNTIME["thread_started"]:
            return
        MIC_RUNTIME["thread_started"] = True
        MIC_RUNTIME["warming"] = True
        MIC_RUNTIME["ready"] = False
        MIC_RUNTIME["error"] = ""
    threading.Thread(target=microphone_loop, args=(config,), daemon=True).start()


def begin_microphone_session(session_id):
    with MIC_LOCK:
        if not MIC_RUNTIME["ready"]:
            return False, MIC_RUNTIME["error"] or "ASR microphone is warming up."

        MIC_RUNTIME["session_id"] = session_id
        MIC_RUNTIME["cancel_requested"] = False
        MIC_RUNTIME["started_at"] = time.time()
        MIC_RUNTIME["peak_rms"] = 0.0
        MIC_RUNTIME["speech_detected"] = False
        return True, ""


def end_microphone_session(session_id=""):
    with MIC_LOCK:
        if session_id and MIC_RUNTIME["session_id"] and session_id != MIC_RUNTIME["session_id"]:
            return
        MIC_RUNTIME["session_id"] = ""
        MIC_RUNTIME["cancel_requested"] = False


def microphone_loop(config):
    try:
        import sounddevice as sd

        sample_rate = int(config.get("sample_rate", 16000))
        chunk_seconds = 0.1
        chunk_size = int(sample_rate * chunk_seconds)
        device = get_input_device(config)

        try:
            vad, window_size = make_vad(config)
        except Exception as exc:
            with MIC_LOCK:
                MIC_RUNTIME["ready"] = False
                MIC_RUNTIME["warming"] = False
                MIC_RUNTIME["error"] = f"VAD init failed: {exc}"
            set_state(status="failed", error=f"ASR VAD init failed: {exc}")
            return

        vad_buffer = np.zeros(0, dtype=np.float32)
        vad_session = ""

        with sd.InputStream(
            samplerate=sample_rate,
            channels=1,
            dtype="float32",
            device=device,
            blocksize=chunk_size,
        ) as stream:
            with MIC_LOCK:
                MIC_RUNTIME["ready"] = True
                MIC_RUNTIME["warming"] = False
                MIC_RUNTIME["error"] = ""

            while True:
                chunk, _ = stream.read(chunk_size)
                chunk = np.asarray(chunk, dtype=np.float32).reshape(-1)
                vad_buffer, vad_session = process_microphone_chunk(
                    config, chunk, sample_rate, vad, window_size, vad_buffer, vad_session
                )
    except Exception as exc:
        with MIC_LOCK:
            MIC_RUNTIME["ready"] = False
            MIC_RUNTIME["warming"] = False
            MIC_RUNTIME["error"] = str(exc)
        set_state(status="failed", error=f"ASR microphone failed: {exc}")


def process_microphone_chunk(config, chunk, sample_rate, vad, window_size, vad_buffer, vad_session):
    rms = float(np.sqrt(np.mean(np.square(chunk)))) if chunk.size else 0.0
    max_record_seconds = max(1.0, float(config.get("max_record_seconds", 15)))

    with MIC_LOCK:
        session_id = MIC_RUNTIME["session_id"]
        if not session_id:
            return vad_buffer, vad_session

        session_started_at = float(MIC_RUNTIME["started_at"])
        elapsed = time.time() - session_started_at
        cancel_requested = MIC_RUNTIME["cancel_requested"]
        if cancel_requested:
            MIC_RUNTIME["session_id"] = ""
            MIC_RUNTIME["cancel_requested"] = False

    if cancel_requested:
        vad.reset()
        set_state(status="canceled", text="", error="", duration=elapsed)
        return np.zeros(0, dtype=np.float32), ""

    # Reset the VAD state when a new session begins so the previous utterance
    # does not bleed into this one.
    if vad_session != session_id:
        vad.reset()
        vad_buffer = np.zeros(0, dtype=np.float32)
        vad_session = session_id

    # Feed audio to silero VAD in fixed window_size slices.
    vad_buffer = np.concatenate([vad_buffer, chunk]) if vad_buffer.size else chunk.copy()
    while len(vad_buffer) >= window_size:
        vad.accept_waveform(vad_buffer[:window_size])
        vad_buffer = vad_buffer[window_size:]

    speech_now = bool(vad.is_speech_detected())
    timed_out = elapsed >= max_record_seconds
    if timed_out:
        # Force the VAD to finalize any in-progress speech at the recording cap.
        vad.flush()

    segment_samples = None
    if not vad.empty():
        segment_samples = np.asarray(vad.front.samples, dtype=np.float32)
        vad.pop()

    with MIC_LOCK:
        # Bail out if the session was canceled/replaced while we were processing.
        if MIC_RUNTIME["session_id"] != session_id:
            return vad_buffer, vad_session

        MIC_RUNTIME["peak_rms"] = max(float(MIC_RUNTIME["peak_rms"]), rms)
        MIC_RUNTIME["speech_detected"] = (
            bool(MIC_RUNTIME["speech_detected"]) or speech_now or segment_samples is not None
        )
        peak_rms = float(MIC_RUNTIME["peak_rms"])
        speech_detected = bool(MIC_RUNTIME["speech_detected"])
        if segment_samples is not None or timed_out:
            MIC_RUNTIME["session_id"] = ""

    set_state(
        duration=elapsed,
        latest_rms=rms,
        peak_rms=peak_rms,
        speech_detected=speech_detected,
    )

    if segment_samples is not None and segment_samples.size > 0:
        recognizer = get_ready_recognizer()
        if recognizer is None:
            set_state(status="failed", text="", error="ASR runtime is not ready.", duration=elapsed)
            vad.reset()
            return np.zeros(0, dtype=np.float32), ""

        set_state(status="recognizing", duration=elapsed)
        # Decode on a worker thread so sherpa-onnx recognition does not block the
        # resident microphone capture loop (which would drop incoming audio).
        threading.Thread(
            target=decode_session_async,
            args=(recognizer, segment_samples, sample_rate, session_id, session_started_at),
            daemon=True,
        ).start()
        vad.reset()
        return np.zeros(0, dtype=np.float32), ""

    if timed_out:
        # No complete speech segment within the recording window.
        set_state(status="done", text="", error="", duration=elapsed)
        vad.reset()
        return np.zeros(0, dtype=np.float32), ""

    return vad_buffer, vad_session


def decode_session_async(recognizer, samples, sample_rate, session_id, session_started_at):
    try:
        text = decode_samples(recognizer, samples, sample_rate)
    except Exception as exc:
        set_state(
            status="failed",
            text="",
            error=f"ASR decode failed: {exc}",
            duration=time.time() - session_started_at,
        )
        return

    # Only publish the result if this session is still current and was not canceled.
    with STATE_LOCK:
        if STATE["session_id"] != session_id or STATE["status"] == "canceled":
            return

    set_state(status="done", text=text, error="", duration=time.time() - session_started_at)


def make_vad(config):
    import sherpa_onnx

    vad_config = sherpa_onnx.VadModelConfig()
    vad_model = config.get("vad", {})
    vad_config.silero_vad.model = str(resolve_service_path(vad_model.get("model_path", "")))
    vad_config.silero_vad.threshold = float(vad_model.get("threshold", 0.5))
    vad_config.silero_vad.min_silence_duration = float(vad_model.get("min_silence_seconds", 0.8))
    vad_config.silero_vad.min_speech_duration = float(vad_model.get("min_speech_seconds", 0.25))
    vad_config.sample_rate = int(config.get("sample_rate", 16000))
    vad_config.validate()
    return sherpa_onnx.VoiceActivityDetector(vad_config, buffer_size_in_seconds=60), vad_config.silero_vad.window_size


def decode_samples(recognizer, samples, sample_rate):
    stream = recognizer.create_stream()
    stream.accept_waveform(sample_rate, samples)
    tail_padding = np.zeros(int(sample_rate * 0.5), dtype=np.float32)
    stream.accept_waveform(sample_rate, tail_padding)
    stream.input_finished()

    while recognizer.is_ready(stream):
        recognizer.decode_stream(stream)

    result = recognizer.get_result(stream)
    if hasattr(result, "text"):
        return str(result.text).strip()
    return str(result).strip()


def start_session(config):
    global SESSION_SEQUENCE

    with STATE_LOCK:
        if STATE["status"] in ("listening", "recognizing"):
            return False, "", "ASR service is busy."

        SESSION_SEQUENCE += 1
        session_id = f"asr_{int(time.time())}_{SESSION_SEQUENCE}"
        STATE["session_id"] = session_id
        STATE["status"] = "listening"
        STATE["text"] = ""
        STATE["error"] = ""
        STATE["duration"] = 0.0
        STATE["engine"] = config.get("engine", DEFAULT_CONFIG["engine"])
        STATE["started_at"] = time.time()
        STATE["latest_rms"] = 0.0
        STATE["peak_rms"] = 0.0
        STATE["speech_detected"] = False

    ok, microphone_error = begin_microphone_session(session_id)
    if not ok:
        set_state(status="failed", text="", error=microphone_error, duration=0.0)
        return False, "", microphone_error

    return True, session_id, ""


def cancel_session(session_id):
    with STATE_LOCK:
        current = STATE["session_id"]
        active = STATE["status"] in ("listening", "recognizing")
        if session_id and current and session_id != current:
            return False, "sessionId is not current"

        with MIC_LOCK:
            MIC_RUNTIME["cancel_requested"] = True

        if active:
            STATE["status"] = "canceled"
            STATE["error"] = ""
            STATE["duration"] = max(0.0, time.time() - float(STATE.get("started_at", time.time())))
        else:
            STATE["status"] = "canceled"
            STATE["error"] = ""

    return True, ""


class AsrHandler(BaseHTTPRequestHandler):
    server_version = "VirtualPartnerASR/0.1"

    def do_GET(self):
        parsed = urlparse(self.path)
        if parsed.path == "/health":
            self.send_json(200, build_health(load_config()))
            return

        if parsed.path == "/asr/status":
            query = parse_qs(parsed.query)
            session_id = query.get("sessionId", [""])[0]
            self.send_json(200, get_status_payload(session_id))
            return

        self.send_json(404, {"success": False, "message": "not found"})

    def do_POST(self):
        parsed = urlparse(self.path)
        if parsed.path == "/asr/start":
            config = load_config()
            health = build_health(config)
            if not health.get("success", False):
                runtime = health.get("runtime", {})
                audio_input = health.get("audioInput", {})
                warming = bool(runtime.get("warming", False)) or bool(audio_input.get("warming", False))
                status = 425 if warming else 503
                message = "ASR is warming up." if warming else health.get("message", "ASR unavailable")
                self.send_json(status, {"success": False, "message": message, "health": health})
                return

            ok, session_id, error = start_session(config)
            if not ok:
                self.send_json(409, {"success": False, "message": error, "busy": True})
                return

            self.send_json(200, {"success": True, "sessionId": session_id})
            return

        if parsed.path == "/asr/cancel":
            body = read_json_body(self)
            ok, error = cancel_session(str(body.get("sessionId", "")).strip())
            self.send_json(200 if ok else 409, {"success": ok, "message": "" if ok else error})
            return

        self.send_json(404, {"success": False, "message": "not found"})

    def log_message(self, fmt, *args):
        sys.stdout.write("[ASR] " + fmt % args + "\n")
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
    host = str(config.get("host", DEFAULT_CONFIG["host"]))
    port = int(config.get("port", DEFAULT_CONFIG["port"]))
    warmup_runtime_background(config)
    start_microphone_background(config)
    server = ThreadingHTTPServer((host, port), AsrHandler)
    print(f"[ASR] VirtualPartner ASR wrapper listening on http://{host}:{port}")
    print("[ASR] Press Ctrl+C to stop.")
    server.serve_forever()


if __name__ == "__main__":
    main()
