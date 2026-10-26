"""Generate independently decoded intra random-access pictures."""

import argparse
import hashlib
import json
import logging
import re
import subprocess
import tempfile
from pathlib import Path

from create_fixtures import HeifWriter, coded, native_pattern


def generate(
    ffmpeg: Path, root: Path, depth: int, slices: int = 1
) -> list[dict[str, str | int | list[str]]]:
    """Encode an open GOP and wrap one CRA and its BLA header variants."""
    pixel_format = {8: "yuv420p", 10: "yuv420p10le"}[depth]
    with tempfile.TemporaryDirectory() as temporary:
        stream = Path(temporary) / "open-gop.hevc"
        subprocess.run(
            [
                str(ffmpeg),
                "-hide_banner",
                "-loglevel",
                "error",
                "-f",
                "lavfi",
                "-i",
                "testsrc2=size=64x48:rate=1",
                "-frames:v",
                "5",
                "-pix_fmt",
                pixel_format,
                "-c:v",
                "libx265",
                "-x265-params",
                "keyint=2:min-keyint=2:scenecut=0:open-gop=1:bframes=0:pools=1:frame-threads=1:repeat-headers=1"
                + (f":slices={slices}:ctu=16:wpp=1" if slices > 1 else ""),
                "-y",
                str(stream),
            ],
            check=True,
            capture_output=True,
        )
        units = [
            unit
            for unit in re.split(b"\x00\x00\x00?\x01", stream.read_bytes())
            if len(unit) >= 2
        ]

    parameters: dict[int, bytes] = {}
    cra: list[bytes] = []
    for unit in units:
        kind = unit[0] >> 1
        if kind in (32, 33, 34):
            parameters[kind] = unit
        if kind == 21:
            cra.append(unit)
        elif cra:
            break
    if not cra:
        raise ValueError("The encoded stream contains no CRA picture.")

    manifest: list[dict[str, str | int | list[str]]] = []
    for name, kind in (
        ("cra", 21),
        ("bla-w-lp", 16),
        ("bla-w-radl", 17),
        ("bla-n-lp", 18),
    ):
        prefix = f"slices{slices}-" if slices > 1 else ""
        stem = f"{prefix}{name}-{depth}bit"
        slice_data = b"".join(
            b"\x00\x00\x00\x01" + bytes([(unit[0] & 1) | (kind << 1)]) + unit[1:]
            for unit in cra
        )
        encoded = b"".join(
            b"\x00\x00\x00\x01" + parameters[key] for key in (32, 33, 34)
        )
        encoded += slice_data
        elementary = root / f"{stem}.hevc"
        elementary.write_bytes(encoded)
        pattern = native_pattern(elementary, depth, False, ffmpeg)
        container = HeifWriter.write([coded(pattern)], 1)
        filename = f"{stem}.heic"
        (root / filename).write_bytes(container)
        manifest.append(
            {
                "file": filename,
                "width": 64,
                "height": 48,
                "inputSha256": hashlib.sha256(container).hexdigest().upper(),
                "rgbaSha256": hashlib.sha256(pattern.rgba.tobytes())
                .hexdigest()
                .upper(),
                "maxPixels": 16 * 1024 * 1024,
                "features": [name, f"{depth}bit", f"{slices}-slices"],
            }
        )
    return manifest


def main() -> None:
    """Append generated cases without regenerating unrelated corpus entries."""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--ffmpeg", type=Path, required=True)
    arguments = parser.parse_args()
    root = Path(__file__).resolve().parent
    generated = [
        entry
        for slices in (1, 2, 3)
        for depth in (8, 10)
        for entry in generate(arguments.ffmpeg, root, depth, slices)
    ]
    names = {entry["file"] for entry in generated}
    path = root / "manifest.json"
    existing = [
        entry for entry in json.loads(path.read_text()) if entry["file"] not in names
    ]
    path.write_text(json.dumps(existing + generated, indent=2) + "\n")
    logging.info("Created %d independently decoded IRAP fixtures", len(generated))


if __name__ == "__main__":
    logging.basicConfig(level=logging.INFO)
    main()
