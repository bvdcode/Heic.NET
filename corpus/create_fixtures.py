"""Wrap original patterns in HEIF files and hash an independent reference."""

import argparse
import hashlib
import json
import logging
import re
import struct
import subprocess
from dataclasses import dataclass, field, replace
from decimal import ROUND_HALF_UP, Decimal
from pathlib import Path

import numpy as np
import numpy.typing as npt


def box(kind: str, payload: bytes) -> bytes:
    """Serialize a bounded ISO base media box."""
    return struct.pack(">I4s", len(payload) + 8, kind.encode("ascii")) + payload


def full_box(kind: str, version: int, flags: int, payload: bytes) -> bytes:
    """Serialize a versioned box without platform-endian fields."""
    return box(kind, bytes([version]) + flags.to_bytes(3, "big") + payload)


@dataclass(frozen=True)
class ItemProperty:
    data: bytes
    essential: bool = False


@dataclass
class ImageItem:
    identifier: int
    kind: str
    data: bytes
    properties: list[ItemProperty]
    hidden: bool = False
    references: dict[str, tuple[int, ...]] = field(default_factory=dict)


@dataclass(frozen=True)
class Pattern:
    name: str
    depth: int
    monochrome: bool
    configuration: bytes
    image_data: bytes
    rgba: npt.NDArray[np.uint8]


class HeifWriter:
    """Write only the explicitly constructed test items using idat-relative extents."""

    @staticmethod
    def write(items: list[ImageItem], primary: int) -> bytes:
        properties: list[bytes] = []
        associations = bytearray(struct.pack(">I", len(items)))
        information = bytearray(struct.pack(">H", len(items)))
        locations = bytearray(b"\x44\x00" + struct.pack(">H", len(items)))
        references = bytearray()
        data = bytearray()
        for item in items:
            information += full_box(
                "infe",
                2,
                int(item.hidden),
                struct.pack(">HH4s", item.identifier, 0, item.kind.encode("ascii"))
                + b"image\x00",
            )
            locations += struct.pack(
                ">HHHHII", item.identifier, 1, 0, 1, len(data), len(item.data)
            )
            data += item.data
            associations += struct.pack(">HB", item.identifier, len(item.properties))
            for value in item.properties:
                if value.data not in properties:
                    properties.append(value.data)
                index = properties.index(value.data) + 1
                if index >= 128:
                    raise ValueError(
                        "Fixture property count exceeds its 7-bit association field"
                    )
                associations.append(index | (128 if value.essential else 0))
            for kind, targets in item.references.items():
                references += box(
                    kind,
                    struct.pack(">HH", item.identifier, len(targets))
                    + b"".join(struct.pack(">H", target) for target in targets),
                )
        handler = full_box(
            "hdlr", 0, 0, b"\x00" * 4 + b"pict" + b"\x00" * 12 + b"Pictures\x00"
        )
        metadata = handler + full_box("pitm", 0, 0, struct.pack(">H", primary))
        metadata += full_box("iinf", 0, 0, information) + full_box(
            "iloc", 1, 0, locations
        )
        metadata += box(
            "iprp",
            box("ipco", b"".join(properties)) + full_box("ipma", 0, 0, associations),
        )
        metadata += full_box("iref", 0, 0, references) + box("idat", data)
        return box("ftyp", b"heic\x00\x00\x00\x00mif1heic") + full_box(
            "meta", 0, 0, metadata
        )


def spatial(width: int, height: int) -> ItemProperty:
    """Describe dimensions before transformative properties."""
    return ItemProperty(full_box("ispe", 0, 0, struct.pack(">II", width, height)))


def color(full_range: bool) -> ItemProperty:
    """Explicitly signal the BT.709 test signal and its quantization range."""
    return ItemProperty(
        box("colr", b"nclx" + struct.pack(">HHHB", 1, 1, 1, 128 if full_range else 0))
    )


def coded(pattern: Pattern, identifier: int = 1, hidden: bool = False) -> ImageItem:
    """Create a coded item with its decoder configuration and exact component depths."""
    depths = bytes([pattern.depth]) * (1 if pattern.monochrome else 3)
    properties = [
        spatial(64, 48),
        color(pattern.monochrome),
        ItemProperty(box("hvcC", pattern.configuration), True),
        ItemProperty(full_box("pixi", 0, 0, bytes([len(depths)]) + depths)),
    ]
    return ImageItem(identifier, "hvc1", pattern.image_data, properties, hidden)


def native_pattern(path: Path, depth: int, monochrome: bool, ffmpeg: Path) -> Pattern:
    """Decode elementary streams with FFmpeg, independently of the library."""
    units = [
        unit
        for unit in re.split(b"\x00\x00\x00?\x01", path.read_bytes())
        if len(unit) >= 2
    ]
    parameters = [unit for unit in units if unit[0] >> 1 in (32, 33, 34)]
    image_data = b"".join(
        struct.pack(">I", len(unit)) + unit
        for unit in units
        if 16 <= unit[0] >> 1 <= 21
    )
    sequence = next(unit for unit in parameters if unit[0] >> 1 == 33)
    unescaped = re.sub(b"\x00\x00\x03", b"\x00\x00", sequence)
    header = (
        b"\x01"
        + unescaped[3:15]
        + b"\xf0\x00\xfc"
        + bytes(
            [0xFC | (0 if monochrome else 1), 0xF8 | (depth - 8), 0xF8 | (depth - 8)]
        )
        + b"\x00\x00\x0f"
    )
    configuration = (
        header
        + bytes([len(parameters)])
        + b"".join(
            bytes([0x80 | (unit[0] >> 1)]) + struct.pack(">HH", 1, len(unit)) + unit
            for unit in parameters
        )
    )
    pixel_format = "gray" if monochrome else {8: "yuv420p", 10: "yuv420p10le"}[depth]
    command = [
        str(ffmpeg),
        "-hide_banner",
        "-loglevel",
        "error",
        "-threads",
        "1",
        "-i",
        str(path),
        "-frames:v",
        "1",
        "-pix_fmt",
        pixel_format,
        "-f",
        "rawvideo",
        "pipe:1",
    ]
    raw = subprocess.run(command, capture_output=True, check=True).stdout
    samples = np.frombuffer(
        raw, dtype=np.uint8 if depth == 8 else np.dtype("<u2")
    ).astype(np.int64)
    expected = reference_rgba(samples, depth, monochrome)
    return Pattern(path.stem, depth, monochrome, configuration, image_data, expected)


def reference_rgba(
    samples: npt.NDArray[np.int64], depth: int, monochrome: bool
) -> npt.NDArray[np.uint8]:
    """Evaluate Q20 with decimal coefficients and independent NumPy arrays."""

    def fixed(value: Decimal) -> int:
        return int((value * (1 << 20)).to_integral_value(rounding=ROUND_HALF_UP))

    maximum = (1 << depth) - 1
    unit = 1 << (depth - 8)
    y_factor = fixed(Decimal(255) / (maximum if monochrome else 219 * unit))
    luma = (
        samples[: 64 * 48].reshape(48, 64) - (0 if monochrome else 16 * unit)
    ) * y_factor
    result = np.empty((48, 64, 4), dtype=np.uint8)
    result[:, :, 3] = 255
    if monochrome:
        gray = np.clip((luma + (1 << 19)) >> 20, 0, 255)
        result[:, :, :3] = gray[:, :, None]
        return result
    cb = samples[64 * 48 : 64 * 48 * 5 // 4].reshape(24, 32).repeat(2, axis=0).repeat(
        2, axis=1
    ) - (1 << (depth - 1))
    cr = samples[64 * 48 * 5 // 4 :].reshape(24, 32).repeat(2, axis=0).repeat(
        2, axis=1
    ) - (1 << (depth - 1))
    kr, kb = Decimal("0.2126"), Decimal("0.0722")
    factor = Decimal(255) / (224 * unit)
    red = luma + cr * fixed(factor * 2 * (1 - kr))
    blue = luma + cb * fixed(factor * 2 * (1 - kb))
    green = luma + cb * fixed(-factor * 2 * kb * (1 - kb) / (1 - kr - kb))
    green += cr * fixed(-factor * 2 * kr * (1 - kr) / (1 - kr - kb))
    for channel, values in enumerate((red, green, blue)):
        result[:, :, channel] = np.clip((values + (1 << 19)) >> 20, 0, 255)
    return result


def grid(pattern: Pattern, first: int, primary: int) -> list[ImageItem]:
    """Construct four tiles with right and bottom edges cropped by the canvas."""
    tiles = [coded(pattern, first + index, True) for index in range(4)]
    header = struct.pack(">BBBBHH", 0, 0, 1, 1, 100, 80)
    tiles.append(
        ImageItem(
            primary,
            "grid",
            header,
            [spatial(100, 80), color(pattern.monochrome)],
            references={"dimg": tuple(item.identifier for item in tiles)},
        )
    )
    return tiles


def main() -> None:
    """Build small fixtures and record verified hashes of camera originals."""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--ffmpeg", type=Path, required=True)
    arguments = parser.parse_args()
    root = Path(__file__).resolve().parent
    patterns = {
        name: native_pattern(root / filename, depth, mono, arguments.ffmpeg)
        for name, filename, depth, mono in (
            ("base", "pattern-8bit.hevc", 8, False),
            ("ten", "pattern-10bit.hevc", 10, False),
            ("mono", "pattern-mono.hevc", 8, True),
            ("deblock", "deblock-8bit.hevc", 8, False),
            ("sao", "sao-10bit.hevc", 10, False),
            ("scaling", "scaling-8bit.hevc", 8, False),
        )
    }
    manifest: list[dict[str, str | int | list[str]]] = []

    def save(
        name: str,
        items: list[ImageItem],
        primary: int,
        expected: npt.NDArray[np.uint8],
        features: list[str],
    ) -> None:
        encoded = HeifWriter.write(items, primary)
        (root / name).write_bytes(encoded)
        manifest.append(
            {
                "file": name,
                "width": expected.shape[1],
                "height": expected.shape[0],
                "inputSha256": hashlib.sha256(encoded).hexdigest().upper(),
                "rgbaSha256": hashlib.sha256(expected.tobytes()).hexdigest().upper(),
                "maxPixels": 16 * 1024 * 1024,
                "features": features,
            }
        )

    for key, filename in (
        ("base", "basic-8bit.heic"),
        ("ten", "basic-10bit.heic"),
        ("mono", "monochrome.heic"),
        ("deblock", "deblocking.heic"),
        ("sao", "sao.heic"),
        ("scaling", "scaling-lists.heic"),
    ):
        pattern = patterns[key]
        save(filename, [coded(pattern)], 1, pattern.rgba, [key])
    base = patterns["base"]
    for turns in (1, 2, 3):
        item = coded(base)
        item.properties.append(ItemProperty(box("irot", bytes([turns])), True))
        save(
            f"rotate-{turns * 90}.heic",
            [item],
            1,
            np.rot90(base.rgba, turns),
            ["rotation"],
        )
    for axis in (0, 1):
        item = coded(base)
        item.properties.append(ItemProperty(box("imir", bytes([axis])), True))
        save(
            f"mirror-{axis}.heic", [item], 1, np.flip(base.rgba, axis=axis), ["mirror"]
        )
    item = coded(base)
    item.properties.extend(
        [
            ItemProperty(box("irot", b"\x01"), True),
            ItemProperty(box("imir", b"\x00"), True),
        ]
    )
    save(
        "rotate-mirror.heic",
        [item],
        1,
        np.flip(np.rot90(base.rgba), axis=0),
        ["rotation", "mirror"],
    )
    crop = struct.pack(">IIIIIIII", 40, 1, 24, 1, 0, 1, 0, 1)
    item = coded(base)
    item.properties.append(ItemProperty(box("clap", crop), True))
    save("crop.heic", [item], 1, base.rgba[12:36, 12:52], ["clean-aperture"])
    canvas = np.tile(base.rgba, (2, 2, 1))[:80, :100]
    save("grid.heic", grid(base, 1, 5), 5, canvas, ["grid", "edge-crop"])
    identity = ImageItem(
        2, "iden", b"", [spatial(64, 48), color(False)], references={"dimg": (1,)}
    )
    save(
        "identity.heic",
        [coded(base, hidden=True), identity],
        2,
        base.rgba,
        ["identity"],
    )
    mono = patterns["mono"]
    auxiliary = ItemProperty(
        full_box("auxC", 0, 0, b"urn:mpeg:mpegB:cicp:systems:auxiliary:alpha\x00"), True
    )
    alpha = coded(mono, 2, True)
    alpha.properties.append(auxiliary)
    alpha.references["auxl"] = (1,)
    transparent = base.rgba.copy()
    transparent[:, :, 3] = mono.rgba[:, :, 0]
    save("alpha.heic", [coded(base), alpha], 1, transparent, ["alpha"])
    master = coded(base)
    master.properties.append(ItemProperty(box("irot", b"\x01"), True))
    rotated_alpha = replace(
        alpha, properties=alpha.properties + [ItemProperty(box("irot", b"\x01"), True)]
    )
    save(
        "alpha-rotated.heic",
        [master, rotated_alpha],
        1,
        np.rot90(transparent),
        ["alpha", "rotation"],
    )
    alpha_grid = grid(mono, 6, 10)
    alpha_grid[-1].hidden = True
    alpha_grid[-1].properties.append(auxiliary)
    alpha_grid[-1].references["auxl"] = (5,)
    grid_transparent = canvas.copy()
    grid_transparent[:, :, 3] = np.tile(mono.rgba, (2, 2, 1))[:80, :100, 0]
    save(
        "grid-alpha.heic",
        grid(base, 1, 5) + alpha_grid,
        5,
        grid_transparent,
        ["grid", "alpha"],
    )
    for filename, width, height, expected in (
        (
            "20260201_164721.heic",
            4000,
            3000,
            "CD66C3B43E6A8D1F0319AAE29EC30EF38B85DB54C84855EB1E09D5ED5181F705",
        ),
        (
            "20260223_110514.heic",
            16320,
            12240,
            "6E92D035F8C45558A7BA59D5B3F55B66E7AFD17B554DA89105FECB5F6C213397",
        ),
    ):
        manifest.append(
            {
                "file": filename,
                "width": width,
                "height": height,
                "inputSha256": hashlib.sha256((root / filename).read_bytes())
                .hexdigest()
                .upper(),
                "rgbaSha256": expected,
                "maxPixels": max(16 * 1024 * 1024, width * height),
                "features": ["camera", "grid"],
            }
        )
    (root / "manifest.json").write_text(
        json.dumps(manifest, indent=2) + "\n", encoding="utf-8"
    )
    logging.info("Wrote %d HEIC cases and their complete RGBA hashes", len(manifest))


if __name__ == "__main__":
    logging.basicConfig(level=logging.INFO, format="%(message)s")
    main()
