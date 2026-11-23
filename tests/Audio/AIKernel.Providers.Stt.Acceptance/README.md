# Local speech recognition CPU acceptance

Build and pack the contracts and the provider with its native runtime as described in [STT.md](../../../src/Audio/AIKernel.Providers.Stt/STT.md). Restore this project with the same package feed.

From the [Google FLEURS CC BY 4.0 distribution](https://huggingface.co/datasets/google/fleurs/tree/70bb2e84b976b7e960aa89f1c648e09c59f894dd), place the `test.tsv` and `audio/test.tar.gz` files for `ja_jp` and `en_us` into one fixture directory, named `ja_jp-test.tsv`, `en_us-test.tsv`, `ja_jp-test.tar.gz` and `en_us-test.tar.gz`. Place the dataset revision in `fleurs-revision.txt`.

```powershell
python prepare_fleurs.py <fixture-directory>
dotnet run -c Release --no-restore -- <ggml-small.bin> <fixture-directory> <report.json>
```

The preparation tool selects the first four distinct test sentences in each language and converts the recorded samples to PCM16. It also creates a repeated 90-second utterance for cancellation and a 600-second 48 kHz stereo input for the duration boundary. The original clips and labels are credited to Google FLEURS. These files remain local test data.

The tests aggregate Japanese character error rate and English word error rate after NFKC, lowercasing and punctuation normalization. They also check source duration, ordered segments, text-only mode, language detection, silence, unsupported and malformed input, byte and duration limits, caller-owned streams and cancellation. Reported elapsed times cover each provider call. Peak working set is the process peak, including initialization. Use `--quick` for regression runs that omit the 600-second workload.
