"""Verify the version of each freshly packed Provider and its bundled manifests."""

import argparse
import json
from pathlib import Path
import re
import xml.etree.ElementTree as ET
from zipfile import ZipFile


# Module-specific versions explicitly approved outside the 0.1.4 release line.
MODULE_VERSIONS = {
    "AIKernel.Providers.Image": {"0.2.0", "0.3.0"},
    "AIKernel.Providers.Flux": {"0.1.5"},
    "AIKernel.Providers.Flux.Cuda": {"0.1.5"},
    "AIKernel.Providers.LocalLlm": {"0.1.5", "0.1.6", "0.3.0"},
}


def check_package(path: Path) -> None:
    with ZipFile(path) as archive:
        spec = next(name for name in archive.namelist() if name.endswith(".nuspec"))
        metadata = ET.fromstring(archive.read(spec)).find("{*}metadata")
        package_id = metadata.findtext("{*}id")
        version = metadata.findtext("{*}version")
        if not version or not (
            re.fullmatch(r"0\.1\.4(?:\.[1-9]\d*|-dev\d+)?", version)
            or version in MODULE_VERSIONS.get(package_id, ())
        ):
            raise ValueError(f"{package_id} {version}: expected a version in the 0.1.4 line")
        for name in archive.namelist():
            if name.endswith(".provider.json"):
                manifest_version = json.loads(archive.read(name))["version"]
                if manifest_version != version:
                    raise ValueError(
                        f"{package_id} {version}: {name} declares {manifest_version}"
                    )


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("directory", type=Path)
    args = parser.parse_args()
    packages = sorted(args.directory.glob("*.nupkg"))
    if not packages:
        parser.error(f"no NuGet packages found in {args.directory}")
    for path in packages:
        check_package(path)
    print(f"Verified metadata for {len(packages)} NuGet packages")


if __name__ == "__main__":
    main()
