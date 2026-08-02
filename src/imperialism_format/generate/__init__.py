"""World generation, modelled on the game's own.

Five shipped scenarios are the original generator's output — the tutorials
`s9`-`s12` and `s15` — so the numbers here are measured from real generated
worlds rather than invented. See `docs/world-generation.md`.
"""
from . import build, naming, politics, rng, scenario, world

__all__ = ["build", "naming", "politics", "rng", "scenario", "world"]
