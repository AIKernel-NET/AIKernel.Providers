"""Generate raw CPU PyTorch logits from the model publisher's Transformers weights."""

import argparse
import hashlib
import json
from pathlib import Path

import torch
from transformers import AutoModelForSequenceClassification, AutoTokenizer


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("model_directory", type=Path)
    parser.add_argument("output", type=Path)
    args = parser.parse_args()
    with (args.model_directory / "model.safetensors").open("rb") as weights:
        sha256 = hashlib.file_digest(weights, "sha256").hexdigest()
    if sha256 != "5daeca2481a76b5976a2bdc32f0a78532b6716da4f8cd3ff59460ef8d2f359b4":
        raise ValueError("Unexpected reference weights.")

    cases = json.loads(Path(__file__).with_name("cases.json").read_text(encoding="utf-8"))
    torch.set_num_threads(4)
    tokenizer = AutoTokenizer.from_pretrained(args.model_directory, local_files_only=True)
    model = AutoModelForSequenceClassification.from_pretrained(
        args.model_directory, local_files_only=True, use_safetensors=True
    ).to(device="cpu", dtype=torch.float32).eval()
    reference = {}
    with torch.inference_mode():
        for case in cases:
            candidates = case["candidates"]
            scores = []
            for start in range(0, len(candidates), 4):
                batch = candidates[start : start + 4]
                inputs = tokenizer(
                    [case["query"]] * len(batch), batch, padding=True, truncation=False, return_tensors="pt"
                )
                scores.extend(model(**inputs).logits.squeeze(-1).tolist())
            reference[case["name"]] = scores
        query, candidate = "text " * 128, "text " * 380
        inputs = tokenizer([query] * 4, [candidate] * 4, padding=True, truncation=False, return_tensors="pt")
        assert inputs.input_ids.shape == (4, 512)
        reference["boundary"] = model(**inputs).logits.squeeze(-1).tolist()
    args.output.write_text(json.dumps(reference, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
