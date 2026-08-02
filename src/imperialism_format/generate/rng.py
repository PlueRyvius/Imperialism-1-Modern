"""Deterministic randomness, seeded from a keyword.

The original does this too — "Imperialism generates random worlds based on a
key word... if you know the key word for your favourite worlds these same
worlds can be recreated" (`README.TXT` §IV). We cannot reproduce *its* worlds,
since its algorithm is not ours, but the property is worth keeping: a world can
be named, shared and regenerated exactly.

`random.Random` is seeded rather than used directly, so generation never
disturbs the global random state a caller might be relying on, and two
generators running from different keywords cannot interfere.
"""
from __future__ import annotations

import hashlib
import random


def seed_from(keyword: str) -> int:
    """Turn a keyword into a stable 64-bit seed.

    Hashed rather than passed to `random.seed` as a string: Python's string
    hashing is salted per process unless PYTHONHASHSEED is fixed, so the same
    keyword would give different worlds on different runs.
    """
    digest = hashlib.sha256(keyword.strip().lower().encode("utf-8")).digest()
    return int.from_bytes(digest[:8], "big")


def generator(keyword: str) -> random.Random:
    return random.Random(seed_from(keyword))


def weighted_choice(rng: random.Random, weights: dict):
    """Pick a key with probability proportional to its weight.

    `random.choices` would do, but this keeps the call sites reading as
    "choose a terrain from this distribution" and tolerates an all-zero
    distribution by falling back to a uniform pick rather than raising.
    """
    total = sum(weights.values())
    if total <= 0:
        return rng.choice(list(weights))
    roll = rng.random() * total
    for key, weight in weights.items():
        roll -= weight
        if roll <= 0:
            return key
    return next(reversed(weights))
