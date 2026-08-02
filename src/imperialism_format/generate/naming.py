"""Names for a generated world.

The original's own pools are not in the executable's string table — they live
in the `.gob` archives, which are still undecoded — so these are ours, written
in the style the shipped generated worlds use. Their countries are short and
invented (Zimm, Pram, Devron, Deneb, Alexen, Patagon, Kem, Issa, Zinlu, Loke),
their provinces mix the English-sounding with the invented (Brantown, Sussex,
Demerest, Urbnaia, Frogmorton, Factoria).

**Port cities come in two flavours, not one.** Measured on `s9`, `s11` and
`s15`: the zone table ends with one port city per country, the sixteen minors
named "<country> City" and the seven Great Powers given ordinary place names
from a separate pool. An earlier reading had them all "<something> City",
which left the powers — including the one you play — with no port at all.

One convention is documented rather than guessed: a random map names each
capital *"(country name) City"* (`README.TXT` §XI-B), which is how the game's
own province finder locates a country. That covers the minors; the powers'
names are a pool, so ours is written in the same style rather than copied.

Names are drawn without replacement from a shuffled pool, so a world never
repeats one, and the pools are long enough that exhausting them is not a
practical concern. If one ever is, numbering keeps it deterministic rather
than raising in the middle of a generation.
"""
from __future__ import annotations

import random

COUNTRIES = [
    "Zimm", "Pram", "Devron", "Deneb", "Alexen", "Patagon", "Kem", "Issa",
    "Zinlu", "Loke", "Marrow", "Vestal", "Corren", "Ashkar", "Belden",
    "Toran", "Quessa", "Halden", "Ryn", "Ostara", "Calder", "Wend", "Fenn",
    "Sarnia", "Trask", "Umber", "Vantel", "Orrin", "Draska", "Elenor",
    "Gascar", "Hesper", "Ilvane", "Jarrow", "Kaldis", "Lorn", "Meridia",
    "Norsk", "Ovar", "Pellen", "Quarn", "Rhodan", "Selvan", "Tirene",
    "Ulmark", "Varn", "Wessel", "Xanth", "Ysar", "Zorrel",
]

#: Port cities for the seven Great Powers. The shipped generated worlds draw
#: ordinary-sounding harbour names here rather than "<country> City" — ours are
#: written in that style, since the game's own pool lives in the undecoded
#: `.gob` archives and its contents are game data either way.
PORT_CITIES = [
    "Ardmouth", "Bellport", "Calderhaven", "Dunmarch", "Estover", "Fairhythe",
    "Granholm", "Harwick", "Ingleby", "Jarnsund", "Kelmouth", "Larkhaven",
    "Marstrand", "Nordhavn", "Ostwick", "Portrey", "Quayside", "Redhythe",
    "Salthaven", "Torbeck", "Ulverston", "Vansund", "Westmouth", "Yarmere",
    "Alderport", "Brackwater", "Corbray", "Deepstrand", "Eddington",
    "Fenwick", "Gullhaven", "Holmness", "Inverkeld", "Kirkwall", "Lynhaven",
]

PROVINCES = [
    "Brantown", "Sussex", "Demerest", "Urbnaia", "Frogmorton", "Factoria",
    "Ashford", "Blackmoor", "Cairnwell", "Dunmore", "Eastmarch", "Fernhollow",
    "Greyfield", "Highwater", "Ironvale", "Kestrel", "Langmere", "Marchwood",
    "Northreach", "Oakhurst", "Pinebrook", "Quarryhill", "Redmarsh",
    "Stonebridge", "Thornwick", "Underhill", "Vinemont", "Westerly",
    "Yarrow", "Aldenbury", "Belhaven", "Coldridge", "Downgate", "Elmsworth",
    "Fallowmere", "Glenmark", "Harrowgate", "Inglewood", "Jarnsdale",
    "Kirkholm", "Lindfell", "Millbrook", "Netherby", "Orchardton",
    "Penhallow", "Quinby", "Ravensmoor", "Southfold", "Tarnbeck",
    "Upminster", "Verewood", "Waltham", "Yewdale", "Amberly", "Bracken",
    "Castlereagh", "Dunhollow", "Edgemere", "Fairwater", "Grimsby",
    "Hollowfield", "Ivybridge", "Joreth", "Kingsmere", "Lowmarsh",
    "Merrowdale", "Norbury", "Oldcastle", "Pikewater", "Quillon",
    "Rushmoor", "Sedgewick", "Thistledown", "Umberfield", "Vantry",
    "Whitcombe", "Yarnbrook", "Alderstone", "Bexley", "Cranmere",
    "Deepdene", "Everton", "Fenwick", "Garrowby", "Hazelmere", "Illingham",
    "Jessup", "Kelmscott", "Larkhill", "Moreton", "Newholm", "Oxenford",
    "Padstow", "Quenby", "Rosthwaite", "Stanwick", "Tredmoor", "Ulverston",
    "Vernham", "Warkworth", "Yeoville", "Abbotsford", "Bridlington",
    "Chalkwell", "Dorwick", "Ellingham", "Fordham", "Greenhithe",
    "Hartsmere", "Ingleby", "Jarrowfield", "Kinross", "Ludlow", "Mereton",
    "Northwold", "Ottershaw", "Pemberton", "Quarrendon", "Rothbury",
    "Selbourne", "Tavistock", "Ufton", "Vardon", "Willoughby", "Yatesbury",
]

# Sea names are built rather than listed: a world needs one per ocean zone,
# and there can be sixty of them. The shipped generated worlds do the same —
# "Koha Ocean", "Strait of Sosenia", "Puginum Channel", "Golpaugre Bay" — a
# made-up word against a small set of water words.
SEA_ROOTS = [
    "Koha", "Seniss", "Mavaok", "Cav", "Ponierre", "Nerue", "Preitis",
    "Loossundo", "Senuse", "Huam", "Loinede", "Dotonhan", "Stoptfist",
    "Sielinaa", "Vurovia", "Hongtid", "Vange", "Vadiga", "Romia", "Eacuf",
    "Becusta", "Bivcoca", "Lupsgatch", "Golpaugre", "Manne", "Sosenia",
    "Puginum", "Kall", "Bazampo", "Kis", "Zlale", "Arden", "Bremmel",
    "Calder", "Dunmar", "Erevan", "Fossan", "Gilden", "Harrow", "Ithel",
    "Jarn", "Kelvin", "Lorne", "Marsk", "Nordath", "Oster", "Pellow",
    "Quarn", "Ravel", "Sunder", "Talven", "Ulmen", "Vesk", "Wexford",
    "Yarrow", "Zandar", "Alvet", "Borran", "Cressel", "Dorval", "Enmark",
    "Fenwold", "Garrow", "Hesk", "Irvane", "Joss", "Kirrow", "Lamand",
]

SEA_FORMS = [
    "{} Sea", "{} Sea", "{} Sea", "{} Ocean", "{} Bay", "{} Channel",
    "Strait of {}", "{} Sound", "Gulf of {}", "{} Reach",
]


class Pool:
    """Draw names without repeating, deterministically."""

    def __init__(self, rng: random.Random, names: list, label: str = "Region"):
        self.remaining = list(names)
        rng.shuffle(self.remaining)
        self.label = label
        self.overflow = 0

    def take(self) -> str:
        if self.remaining:
            return self.remaining.pop()
        # Exhausting a pool must not stop a generation half-built.
        self.overflow += 1
        return f"{self.label} {self.overflow}"


def sea_names(rng: random.Random, count: int) -> list:
    """`count` distinct water names, built the way the shipped worlds are."""
    roots = list(SEA_ROOTS)
    rng.shuffle(roots)
    names, index = [], 0
    while len(names) < count:
        root = roots[index % len(roots)]
        form = SEA_FORMS[(index // len(roots) + index) % len(SEA_FORMS)]
        candidate = form.format(root)
        if candidate not in names:
            names.append(candidate)
        index += 1
        if index > count * 20:              # pathological pool exhaustion
            names.append(f"Sea {len(names) + 1}")
    return names


def capital_name(country: str) -> str:
    """A random map names each capital "(country) City" — README.TXT §XI-B.

    True of the sixteen minor nations. A Great Power's port takes a name from
    `PORT_CITIES` instead; see the module docstring.
    """
    return f"{country} City"
