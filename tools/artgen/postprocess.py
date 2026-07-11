#!/usr/bin/env python
"""생성된 아트 후처리 → 게임 투입.

- 유닛/몬스터: 단색 배경 키잉 → 최대 연결성분 마스킹 → 타이트 크롭 → 512px 리사이즈
  → Assets/StreamingAssets/art/units/{id}.png
- 배경(bg_*): 그대로 Assets/StreamingAssets/art/bg/ 로 복사 (1080 세로 리사이즈)

사용: venv/bin/python postprocess.py [--src ../../art/units]
"""
import argparse, os, shutil

import numpy as np
from PIL import Image
from scipy import ndimage as ndi


def key_and_crop(path: str, out: str, size: int = 512):
    img = Image.open(path).convert("RGBA")
    arr = np.array(img).astype(int)
    # 배경색: 네 모서리 평균 (플랫 배경 전제)
    corners = [arr[4, 4, :3], arr[4, -4, :3], arr[-4, 4, :3], arr[-4, -4, :3]]
    bg = np.mean(corners, axis=0)
    dist = np.sqrt(((arr[:, :, :3] - bg) ** 2).sum(axis=2))
    alpha = np.clip((dist - 24) / 24 * 255, 0, 255).astype(np.uint8)

    mask = alpha > 40
    labels, n = ndi.label(mask)
    if n > 1:
        sizes = ndi.sum(mask, labels, range(1, n + 1))
        # 전체 면적 3% 미만 파편 제거 (최대 성분은 항상 유지)
        biggest = np.argmax(sizes) + 1
        keep = np.zeros_like(mask)
        for i, s in enumerate(sizes, start=1):
            if i == biggest or s > mask.size * 0.03:
                keep |= labels == i
        keep = ndi.binary_dilation(keep, iterations=2)
        alpha[~keep] = 0

    out_arr = np.array(img)
    out_arr[:, :, 3] = alpha
    ys, xs = np.where(alpha > 10)
    if len(ys) == 0:
        print(f"  ⚠ {os.path.basename(path)}: 전경 없음 — 스킵")
        return False
    m = 8
    out_arr = out_arr[max(0, ys.min() - m):ys.max() + m, max(0, xs.min() - m):xs.max() + m]

    result = Image.fromarray(out_arr)
    result.thumbnail((size, size), Image.LANCZOS)
    os.makedirs(os.path.dirname(out), exist_ok=True)
    result.save(out)
    return True


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--src", default="../../art/units")
    ap.add_argument("--units-out", default="../../Assets/StreamingAssets/art/units")
    ap.add_argument("--bg-out", default="../../Assets/StreamingAssets/art/bg")
    args = ap.parse_args()

    done = skipped = 0
    for name in sorted(os.listdir(args.src)):
        if not name.endswith(".png"):
            continue
        src = os.path.join(args.src, name)
        if name.startswith("bg_"):
            os.makedirs(args.bg_out, exist_ok=True)
            img = Image.open(src)
            img.thumbnail((1080, 1620), Image.LANCZOS)
            img.save(os.path.join(args.bg_out, name))
            done += 1
        else:
            if key_and_crop(src, os.path.join(args.units_out, name)):
                done += 1
            else:
                skipped += 1
    print(f"[postprocess] 완료 {done}건, 스킵 {skipped}건")


if __name__ == "__main__":
    main()
