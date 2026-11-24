"""Compare fresh LocalLlm/Core symbol measurements with the approved LocalLlm baseline."""
import argparse
import csv
from pathlib import Path
import sys

KINDS = {"Assembly", "NamedType", "Field", "Property", "Method"}
BASELINES = Path(__file__).resolve().parents[1] / "docs/operations"


def measurements(paths):
    symbols = {}
    for path in paths:
        with path.open(encoding="utf-8-sig", newline="") as stream:
            for line, row in enumerate(csv.reader(stream, delimiter="\t"), 1):
                if not row or row[0] == "Namespace":
                    continue
                if len(row) not in (5, 6):
                    raise ValueError(f"{path}:{line}: expected kind, symbol, MI, CC, coupling and optional source lines")
                kind, symbol = row[:2]
                if kind not in KINDS:
                    raise ValueError(f"{path}:{line}: unexpected symbol kind {kind!r}")
                key = (kind, symbol)
                if key in symbols:
                    raise ValueError(f"{path}:{line}: duplicate {key}")
                symbols[key] = tuple(int(n) for n in row[2:5])
    return symbols


def regressions(baseline, current):
    for key, limit in baseline.items():
        if key not in current:
            yield f"Missing {key[0]} {key[1]} (record any intentional move before updating the baseline)"
    for (kind, name), value in current.items():
        limit = baseline.get((kind, name))
        if limit is None:
            if kind != "Method":
                continue
            limit = (60, 10, 10)
        if value[0] < limit[0] or value[1] > limit[1] or value[2] > limit[2]:
            yield f"{kind} {name}: MI/CC/coupling {value}, required >= {limit[0]} / <= {limit[1]} / <= {limit[2]}"


def moved_baseline(baseline, path):
    """Compare each approved limit against exactly one current responsibility."""
    targets = {}
    with path.open(encoding="utf-8", newline="") as stream:
        for kind, previous, current in csv.reader(stream, delimiter="\t"):
            key = (kind, previous)
            if key not in baseline or key in targets:
                raise ValueError(f"Unknown or repeated source responsibility: {key}")
            targets[key] = (kind, current)
    result = {targets.get(key, key): value for key, value in baseline.items()}
    if len(result) != len(baseline):
        raise ValueError("Multiple baseline responsibilities map to the same measurement")
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("measurements", nargs="+", type=Path, help="Roslyn metric TSV for Core and Providers")
    parser.add_argument("--moves", type=Path, help="TSV with kind, old symbol, new symbol for responsibility moves")
    parser.add_argument("--baseline-step", choices=("2", "3", "4"), default="4", help="Approved step used for regression limits (default: 4)")
    args = parser.parse_args()
    baseline = measurements([BASELINES / f"local-llm-step{args.baseline_step}" / "quality-baseline.tsv"])
    if not any(kind == "Assembly" and name.startswith("LocalLlmProvider,") for kind, name in baseline):
        parser.error("missing LocalLlmProvider assembly baseline")
    if args.moves:
        baseline = moved_baseline(baseline, args.moves)
    errors = list(regressions(baseline, measurements(args.measurements)))
    for error in errors:
        print(error, file=sys.stderr)
    if errors:
        return 1
    print(f"Passed all {len(baseline)} baseline symbols and the thresholds for new methods.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
