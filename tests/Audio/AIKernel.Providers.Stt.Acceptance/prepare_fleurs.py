"""Extract fixed FLEURS test fixtures and construct longer WAVs from the same recordings."""

import argparse
import csv
import json
import struct
import tarfile
import wave
from pathlib import Path


def extract(directory, locale, language):
    selected = {}
    seen = set()
    with (directory / f"{locale}-test.tsv").open(encoding="utf-8") as manifest:
        for row in csv.reader(manifest, delimiter="\t", quoting=csv.QUOTE_NONE):
            sentence_id, name, transcript = row[:3]
            if sentence_id not in seen:
                selected[name] = {"file": name, "language": language, "reference": transcript, "sentenceId": sentence_id}
                seen.add(sentence_id)
            if len(selected) == 4:
                break

    with tarfile.open(directory / f"{locale}-test.tar.gz", "r|gz") as archive:
        remaining = set(selected)
        for member in archive:
            name = Path(member.name).name
            if name in remaining and member.isfile():
                save_pcm16(directory / name, archive.extractfile(member).read())
                remaining.remove(name)
                if not remaining:
                    break
    if remaining:
        raise ValueError(f"Missing recordings: {remaining}")
    for case in selected.values():
        with wave.open(str(directory / case["file"]), "rb") as audio:
            case["durationTicks"] = audio.getnframes() * 10_000_000 // audio.getframerate()
    return list(selected.values())


def save_pcm16(path, data):
    if data[:4] != b"RIFF" or data[8:12] != b"WAVE":
        raise ValueError(f"Invalid FLEURS WAV header: {path.name}")
    pcm_format, samples = None, None
    offset = 12
    while offset + 8 <= len(data):
        kind, size = struct.unpack_from("<4sI", data, offset)
        body = offset + 8
        if kind == b"fmt ":
            pcm_format = struct.unpack_from("<HHIIHH", data, body)
        elif kind == b"data":
            samples = data[body : body + size]
        offset = body + size + (size & 1)
    if pcm_format is None or samples is None:
        raise ValueError(f"Incomplete FLEURS WAV: {path.name}")
    tag, channels, rate, _, _, bits = pcm_format
    if (channels, rate) != (1, 16000) or (tag, bits) not in ((1, 16), (3, 32)):
        raise ValueError(f"Unexpected FLEURS format: {pcm_format}")
    if tag == 3:
        encoded = bytearray(len(samples) // 2)
        for index, (sample,) in enumerate(struct.iter_unpack("<f", samples)):
            struct.pack_into("<h", encoded, index * 2, max(-32768, min(32767, round(sample * 32768))))
        samples = encoded
    with wave.open(str(path), "wb") as audio:
        audio.setnchannels(1)
        audio.setsampwidth(2)
        audio.setframerate(16000)
        audio.writeframes(samples)


def pcm(directory, name):
    with wave.open(str(directory / name), "rb") as audio:
        if (audio.getnchannels(), audio.getsampwidth(), audio.getframerate()) != (1, 2, 16000):
            raise ValueError(f"Unexpected FLEURS format: {name}")
        return audio.readframes(audio.getnframes())


def write_repeated(path, samples, rate, channels, duration):
    width = 2 * channels
    remaining = duration * rate
    with wave.open(str(path), "wb") as audio:
        audio.setnchannels(channels)
        audio.setsampwidth(2)
        audio.setframerate(rate)
        while remaining:
            batch = min(len(samples) // width, remaining)
            audio.writeframesraw(samples[: batch * width])
            remaining -= batch


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("directory", type=Path)
    directory = parser.parse_args().directory
    cases = extract(directory, "ja_jp", "ja") + extract(directory, "en_us", "en")
    revision = (directory / "fleurs-revision.txt").read_text(encoding="utf-8-sig").strip()
    (directory / "cases.json").write_text(json.dumps(
        {"source": "https://huggingface.co/datasets/google/fleurs", "license": "CC-BY-4.0",
         "revision": revision, "cases": cases}, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    samples = b"\0" * 16000
    english = pcm(directory, cases[4]["file"])
    samples += english + b"\0" * 32000
    write_repeated(directory / "cancel-en.wav", samples, 16000, 1, 90)
    # Repeat each mono sample over three 48 kHz frames and both stereo channels.
    stereo = b"".join(samples[i:i+2] * 6 for i in range(0, len(samples), 2))
    write_repeated(directory / "max-48k-stereo.wav", stereo, 48000, 2, 600)


if __name__ == "__main__":
    main()
