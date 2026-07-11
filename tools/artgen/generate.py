#!/usr/bin/env python
"""로컬 아트 생성기 — Animagine XL 4.0 (SDXL 아니메 특화) on Apple Silicon MPS.

사용:
  venv/bin/python generate.py --prompt "1boy, chibi, ..." --out out.png [--seed 42]
  venv/bin/python generate.py --batch assets.json   # [{name, prompt, width, height, seed}]

프롬프트는 단부루 태그 스타일. 치비 게임 아트 기본 태그가 자동으로 붙는다.
"""
import argparse, json, os, sys, time

import torch
from diffusers import StableDiffusionXLPipeline

MODEL = "cagliostrolab/animagine-xl-4.0"

# 게임 아트 공통 스타일 앵커 (모든 에셋에 동일 적용 → 일관성)
STYLE_SUFFIX = (
    ", masterpiece, high score, great score, absurdres"
)
NEGATIVE = (
    "lowres, bad anatomy, bad hands, text, error, missing finger, extra digits, fewer digits, "
    "cropped, worst quality, low quality, low score, bad score, average score, signature, "
    "watermark, username, blurry, realistic, photo, 3d, "
    "multiple views, character sheet, reference sheet, multiple boys, multiple girls, 2boys, 2girls"
)

_pipe = None

def pipe():
    global _pipe
    if _pipe is None:
        print("[artgen] 모델 로딩 (첫 실행은 ~7GB 다운로드)...", flush=True)
        _pipe = StableDiffusionXLPipeline.from_pretrained(
            MODEL, torch_dtype=torch.float16, use_safetensors=True
        ).to("mps")
    return _pipe

def generate(prompt: str, out: str, width: int = 832, height: int = 1216,
             seed: int = 0, steps: int = 28, guidance: float = 6.0):
    generator = torch.Generator("cpu").manual_seed(seed)
    start = time.time()
    image = pipe()(
        prompt=prompt + STYLE_SUFFIX,
        negative_prompt=NEGATIVE,
        width=width, height=height,
        num_inference_steps=steps, guidance_scale=guidance,
        generator=generator,
    ).images[0]
    os.makedirs(os.path.dirname(out) or ".", exist_ok=True)
    image.save(out)
    print(f"[artgen] {out} ({time.time()-start:.0f}s, seed={seed})", flush=True)

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--prompt")
    ap.add_argument("--out", default="out.png")
    ap.add_argument("--width", type=int, default=832)
    ap.add_argument("--height", type=int, default=1216)
    ap.add_argument("--seed", type=int, default=0)
    ap.add_argument("--steps", type=int, default=28)
    ap.add_argument("--batch", help="JSON 파일: [{name, prompt, width, height, seed}]")
    ap.add_argument("--outdir", default=".")
    args = ap.parse_args()

    if args.batch:
        items = json.load(open(args.batch))
        for i, item in enumerate(items):
            out = os.path.join(args.outdir, item["name"] + ".png")
            print(f"[artgen] ({i+1}/{len(items)}) {item['name']}", flush=True)
            generate(item["prompt"], out,
                     item.get("width", 832), item.get("height", 1216),
                     item.get("seed", i * 7 + 1), args.steps)
    elif args.prompt:
        generate(args.prompt, args.out, args.width, args.height, args.seed, args.steps)
    else:
        ap.error("--prompt 또는 --batch 필요")

if __name__ == "__main__":
    main()
