"""Generate independent FP32 reference vectors from the pinned upstream revision."""

import argparse
import json
from pathlib import Path

import torch
from tokenizers import Tokenizer
from transformers import AutoModel


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("model", type=Path, help="Directory with the official PyTorch configuration and weights")
    parser.add_argument("output", type=Path)
    args = parser.parse_args()
    torch.set_num_threads(2)
    tok = Tokenizer.from_file(str(args.model / "onnx" / "tokenizer.json"))
    model = AutoModel.from_pretrained(args.model, local_files_only=True).cpu().eval()
    tok.enable_padding(pad_id=1, pad_token="<pad>")

    ja = "画像をPNGで保存する"
    en = "Save the image as PNG"
    queries = [
        ja, "画像をPNG形式で書き出す", "明日の降水確率を教えてください",
        en, "Export the picture as a PNG file", "What is the chance of rain tomorrow?",
        "ＡＩと  日本語   の\t検索", "Search\u00a0for\t📷  photographs.", "Query with <mask> and <s>."
    ]
    cases = [
        ("Query", True, queries),
        ("Document", False, queries),
        ("Document", True, [ja, en]),
    ]
    references = []
    for purpose, normalize, texts in cases:
        prefix = "query: " if purpose == "Query" else "passage: "
        encoded = tok.encode_batch([prefix + text for text in texts])
        ids = torch.tensor([item.ids for item in encoded])
        mask = torch.tensor([item.attention_mask for item in encoded])
        with torch.inference_mode():
            hidden = model(input_ids=ids, attention_mask=mask).last_hidden_state
            vectors = hidden.masked_fill(~mask[..., None].bool(), 0.0).sum(1) / mask.sum(1)[..., None]
            if normalize:
                vectors = torch.nn.functional.normalize(vectors, p=2, dim=1)
        references.append({"Purpose": purpose, "Normalize": normalize, "Texts": texts, "Vectors": vectors.tolist()})

    # Find the exact 512-token boundary with the upstream tokenizer.
    within = ""
    for n in range(460, 512):
        text = " ".join(["image"] * n)
        if len(tok.encode("query: " + text).ids) == 512:
            within = text
            break
    if len(tok.encode("query: " + within).ids) != 512:
        raise RuntimeError("Could not construct the official 512-token fixture.")
    args.output.write_text(json.dumps({"Cases": references, "AtLimit": within}, ensure_ascii=False), encoding="utf-8")
    print(f"Generated {len(references)} independent PyTorch FP32 reference batches.")


if __name__ == "__main__":
    main()
