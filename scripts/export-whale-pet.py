"""Export the default whale outfit from a local Coopanion source into a NoteDesk pet package."""
import argparse
import hashlib
import io
import json
import math
from pathlib import Path
from tempfile import NamedTemporaryFile

from PIL import Image, ImageChops, ImageDraw

FRAME = (192, 208)
COUNTS = (6, 8, 8, 4, 5, 8, 6, 6, 6)
SCALE = 2
RIG = (280 * SCALE, 280 * SCALE)
ORIGIN = (12, 8)
FRAMES = ("idle", "walk", "left", "wave", "celebrate", "idle", "reminder", "idle", "idle")


def read_json(file):
    return json.loads(file.read_text(encoding="utf-8-sig"))


def write_atomic(destination, data):
    with NamedTemporaryFile(dir=destination.parent, delete=False) as stream:
        temporary = Path(stream.name)
        try:
            stream.write(data)
            stream.close()
            temporary.replace(destination)
        finally:
            temporary.unlink(missing_ok=True)


class Whale:
    def __init__(self, manifest_path):
        self.root = manifest_path.resolve().parent
        manifest = read_json(manifest_path)
        if manifest.get("id") != "whale" or manifest.get("api") != 2 or manifest.get("model") != "model.json":
            raise ValueError("Choose the built-in whale/figure.json from Coopanion's source.")
        self.name = manifest.get("name", {}).get("zh", "DeepSeek 大肥鱼")
        model = read_json(self.root / "model.json")
        self.pivots = model["pivots"]
        self.layers = []
        for part in sorted(model["parts"], key=lambda part: part["z"]):
            if part.get("parent") == "skirtSit":
                continue
            asset = self.load(part.get("src", f'tex/{part["tex"]}.png'))
            layer = self.layer(asset, part["box"])
            self.layers.append((part, layer))
        self.eyes = model["feat"]["eyes"]
        self.sprites = model["feat"]["sprites"]
        self.units = model["units"]
        self.faces = {face: self.face(face) for face in ("neutral", "happy", "surprised", "blink")}

    def load(self, filename):
        asset = (self.root / filename).resolve()
        if not asset.is_relative_to(self.root) or asset.suffix.lower() != ".png":
            raise ValueError(f"Unexpected whale texture path: {filename}")
        with Image.open(asset) as image:
            return image.convert("RGBA")

    @staticmethod
    def point(x, y):
        return (round((x + ORIGIN[0]) * SCALE), round((y + ORIGIN[1]) * SCALE))

    def layer(self, image, box):
        x, y, width, height = box
        left, top = self.point(x, y)
        right, bottom = self.point(x + width, y + height)
        result = Image.new("RGBA", RIG)
        result.alpha_composite(image.resize((right - left, bottom - top), Image.Resampling.LANCZOS), (left, top))
        return result

    def face(self, expression):
        origin = (510, 570)
        face = Image.new("RGBA", (420, 270))
        def place(image, box):
            face.alpha_composite(image, (box[0] - origin[0], box[1] - origin[1]))

        for key, eye in self.eyes.items():
            if expression != "neutral":
                prefix = "sleep" if expression == "blink" else expression
                sprite = f"{prefix}_{key}"
                place(self.load(f"feat/{sprite}.png"), self.sprites[sprite])
                continue
            ball = self.load(f"feat/{key}_ball.png")
            iris = self.load(f"feat/{key}_iris.png")
            merged = Image.new("RGBA", ball.size)
            merged.alpha_composite(iris, (eye["iris"][0] - eye["ball"][0], eye["iris"][1] - eye["ball"][1]))
            alpha = ball.getchannel("A")
            ball.alpha_composite(merged)
            a, b, c = eye["lidFit"]
            x, y = eye["ball"][:2]
            mask = Image.new("L", ball.size, 255)
            ImageDraw.Draw(mask).polygon(
                [(0, -1), (ball.width, -1)] +
                [(i, round(a * (i + x) ** 2 + b * (i + x) + c - y))
                 for i in range(ball.width, -1, -2)], fill=0)
            ball.putalpha(ImageChops.multiply(alpha, mask))
            place(ball, eye["ball"])
            place(self.load(f"feat/{key}_lash.png"), eye["lash"])
        mouth = ("neutral" if expression == "blink" else expression) + "_mouth"
        x, y = self.sprites[mouth][:2]
        place(self.load(f"feat/{mouth}.png"), (x, y + (9 if expression == "happy" else 0)))
        s = self.units["S"]
        return self.layer(face, (128 + (origin[0] - self.units["X0"]) * s,
                                 256 - (self.units["FEET"] - origin[1]) * s,
                                 face.width * s, face.height * s))

    def render(self, kind, progress, index):
        walk = kind in ("walk", "left")
        phase = math.sin(2 * math.pi * progress)
        moving = math.sin(math.pi * progress)
        angle = {
            "legBack": 13 * phase if walk else 0,
            "legFront": -13 * phase if walk else 0,
            "armNear": -42 * moving if kind in ("wave", "celebrate") else -6 * phase if walk else 0,
            "armFar": 14 * moving if kind == "celebrate" else 6 * phase if walk else 0,
            "tailBend": (7 if walk else 3) * phase,
            "finNear": 4 * phase,
            "finFar": -3 * phase,
            "ahoge": (10 if kind == "reminder" else 4) * phase,
        }
        expression = ("happy" if kind in ("wave", "celebrate") else
                      "surprised" if kind == "reminder" else
                      "blink" if kind == "idle" and index == 4 else "neutral")
        pose = Image.new("RGBA", RIG)
        face_done = False
        for part, original in self.layers:
            if not face_done and part["z"] > 9:
                pose.alpha_composite(self.faces[expression])
                face_done = True
            parent = part.get("parent")
            degrees = angle.get(parent, 0)
            pivot = self.pivots.get("tail" if parent == "tailBend" else parent)
            layer = (original.rotate(degrees, Image.Resampling.BICUBIC, center=self.point(*pivot))
                     if degrees and pivot else original)
            if part.get("blend") == "multiply":
                shadow = ImageChops.multiply(pose, layer)
                shadow.putalpha(pose.getchannel("A"))
                pose = Image.composite(shadow, pose, layer.getchannel("A"))
            else:
                pose.alpha_composite(layer)
        if not face_done:
            pose.alpha_composite(self.faces[expression])
        if kind == "left":
            pose = pose.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
        cell = Image.new("RGBA", FRAME)
        bob = 11 * moving if kind == "celebrate" else 2 * abs(phase) if walk else phase
        cell.alpha_composite(pose.resize((192, 192), Image.Resampling.LANCZOS), (0, 8 - round(bob)))
        return cell

    def export(self, output):
        atlas = Image.new("RGBA", (8 * FRAME[0], 11 * FRAME[1]))
        for row, (kind, count) in enumerate(zip(FRAMES, COUNTS)):
            for col in range(count):
                frame = self.render(kind, col / count, col)
                alpha = frame.getchannel("A").histogram()
                if alpha[0] < 500 or sum(alpha[220:]) < 500:
                    raise ValueError(f"Empty or opaque frame: row {row}, column {col}")
                atlas.alpha_composite(frame, (col * FRAME[0], row * FRAME[1]))
        buffer = io.BytesIO()
        atlas.save(buffer, format="PNG", optimize=True)
        png = buffer.getvalue()
        if len(png) > 16 * 1024 * 1024:
            raise ValueError("The generated atlas exceeds NoteDesk's 16 MB limit.")
        output.mkdir(parents=True, exist_ok=True)
        sprite = "spritesheet-" + hashlib.sha256(png).hexdigest()[:12] + ".png"
        write_atomic(output / sprite, png)
        manifest = output / "pet.json"
        data = json.dumps({
            "id": "coopanion-whale",
            "displayName": self.name,
            "description": "Personal conversion from local Coopanion source. See upstream artwork notices.",
            "spritesheetPath": sprite,
            "spriteVersionNumber": 2,
        }, ensure_ascii=False, indent=2) + "\n"
        write_atomic(manifest, data.encode("utf-8"))
        return manifest.resolve()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("figure", type=Path, help="the local Coopanion whale/figure.json")
    parser.add_argument("output", type=Path, help="directory for the generated pet.json and PNG")
    args = parser.parse_args()
    print(Whale(args.figure).export(args.output))


if __name__ == "__main__":
    main()
