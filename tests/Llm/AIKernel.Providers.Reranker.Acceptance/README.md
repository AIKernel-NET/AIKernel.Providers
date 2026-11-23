# Local reranker CPU acceptance

Run this check using the Reranking contracts and Reranker Provider packages. For a local build, pack the contracts first and the provider second into the same local NuGet feed. Restore this test project with that feed and the standard dependency feed.

Place the fixed revision's ONNX model and tokenizer in the directory described in [RERANKER.md](../../../src/Llm/AIKernel.Providers.Reranker/RERANKER.md). Add the same revision's `model.safetensors`, `config.json`, `tokenizer_config.json`, `special_tokens_map.json` and `sentencepiece.bpe.model` for the PyTorch reference. With CPU PyTorch and Transformers installed, run the following from this directory.

```powershell
python reference.py <model-directory> <reference-json>
dotnet run -c Release --no-restore -- <model-directory> <reference-json>
```

The .NET process emits JSON with ranking results, maximum absolute logit error, cancellation timings and the peak process working set. The maximum-size test evaluates 16 pairs of 512 tokens in groups of four. The citation lookup test uses a fixture of source-bearing search results.
