"""Loopback HTTP adapter for a user-provisioned Kokoro ONNX runtime."""

import argparse
import io
import json
import math
import os
import threading
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--model", required=True)
    parser.add_argument("--voices", required=True)
    parser.add_argument("--port", type=int, default=8765)
    parser.add_argument("--cuda", action="store_true")
    parser.add_argument("--cuda-dll-directory")
    parser.add_argument("--profile", help="Optional ONNX Runtime profiling output prefix.")
    settings = parser.parse_args()

    dll_directory = None
    if settings.cuda_dll_directory and os.name == "nt":
        dll_directory = os.add_dll_directory(settings.cuda_dll_directory)
        os.environ["PATH"] = settings.cuda_dll_directory + os.pathsep + os.environ["PATH"]

    import onnxruntime as ort
    import soundfile
    from kokoro_onnx import Kokoro

    if settings.cuda_dll_directory and settings.cuda:
        ort.preload_dlls(directory=settings.cuda_dll_directory)
    options = ort.SessionOptions()
    options.intra_op_num_threads = 2
    if settings.profile:
        options.enable_profiling = True
        options.profile_file_prefix = settings.profile
    providers = ["CPUExecutionProvider"]
    if settings.cuda:
        providers.insert(0, ("CUDAExecutionProvider", {
            "device_id": 0, "cudnn_conv_algo_search": "DEFAULT",
        }))
    session = ort.InferenceSession(settings.model, sess_options=options, providers=providers)
    if settings.cuda and session.get_providers()[0] != "CUDAExecutionProvider":
        raise RuntimeError("CUDA was requested and did not initialize.")
    session.disable_fallback()
    synth = Kokoro.from_session(session, settings.voices)
    voices = sorted(voice for voice in synth.get_voices() if voice.startswith(("af_", "am_")))
    engine = "kokoro-82m"
    health = json.dumps({"engine": engine, "languages": ["en-US"], "voices": voices}).encode()
    synthesis_lock = threading.Lock()

    class Handler(BaseHTTPRequestHandler):
        def setup(self):
            self.request.settimeout(10)
            super().setup()

        def respond(self, status, body=b"", **headers):
            self.send_response(status)
            self.send_header("Content-Length", str(len(body)))
            self.send_header("Cache-Control", "no-store")
            for key, value in headers.items():
                self.send_header(key.replace("_", "-"), value)
            self.end_headers()
            try:
                self.wfile.write(body)
            except (BrokenPipeError, ConnectionResetError):
                pass

        def accepts_local_client(self):
            port = self.server.server_port
            hosts = {f"127.0.0.1:{port}", f"localhost:{port}"}
            if port == 80:
                hosts.update(("127.0.0.1", "localhost"))
            if self.headers.get("Host") in hosts and self.headers.get("Origin") is None:
                return True
            self.respond(403)
            return False

        def do_GET(self):
            if not self.accepts_local_client():
                return
            if self.path != "/health":
                self.respond(404)
                return
            self.respond(200, health, Content_Type="application/json")

        def do_POST(self):
            if not self.accepts_local_client():
                return
            if self.path != "/synthesize":
                self.respond(404)
                return
            if self.headers.get_content_type() != "application/json":
                self.respond(415)
                return
            try:
                length = int(self.headers.get("Content-Length", "0"))
                if not 0 < length <= 65536:
                    self.respond(413)
                    return
                data = json.loads(self.rfile.read(length))
                text, language, voice, speed = (data[key] for key in ("text", "language", "voice", "speed"))
                if (not isinstance(text, str) or not text.strip() or len(text) > 4096
                        or not isinstance(language, str) or not isinstance(voice, str)
                        or type(speed) not in (int, float) or not math.isfinite(speed)
                        or not 0.5 <= speed <= 2):
                    raise ValueError()
            except (ValueError, TypeError, KeyError, OverflowError):
                self.respond(400)
                return
            if language.lower() != "en-us":
                self.respond(422, X_Tts_Error="unsupported_language")
                return
            if voice not in voices:
                self.respond(422, X_Tts_Error="unsupported_voice")
                return
            if not synthesis_lock.acquire(blocking=False):
                self.respond(503)
                return
            try:
                samples, sample_rate = synth.create(text, voice=voice, speed=speed, lang="en-us")
                buffer = io.BytesIO()
                soundfile.write(buffer, samples, sample_rate, format="WAV", subtype="PCM_16")
                audio = buffer.getvalue()
            except Exception as error:
                self.log_error("Synthesis failed: %s", type(error).__name__)
                self.respond(500)
                return
            finally:
                synthesis_lock.release()
            self.respond(200, audio, Content_Type="audio/wav", X_Tts_Engine=engine, X_Tts_Voice=voice)

    with ThreadingHTTPServer(("127.0.0.1", settings.port), Handler) as server:
        print(f"Ready on http://127.0.0.1:{server.server_port}/ with {session.get_providers()}", flush=True)
        try:
            server.serve_forever()
        except KeyboardInterrupt:
            pass
        finally:
            if settings.profile:
                print(session.end_profiling(), flush=True)
            if dll_directory is not None:
                dll_directory.close()


if __name__ == "__main__":
    main()
