"""Local web server for the Imperialism map editor.

    python tools/map_editor/server.py path/to/s1.map

The browser owns rendering and input; this process owns the file.  The client
never sees or constructs map bytes — it sends field edits and gets back the
cells that changed.  That is what keeps the undecoded parts of the format
(the trailer, the unused_* bytes) intact no matter what the UI does.

Uses only the standard library, on purpose: the format package has no runtime
dependencies and a single-user localhost tool is not a reason to give it some.
"""
from __future__ import annotations

import argparse
import base64
import json
import os
import sys
import webbrowser
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from urllib.parse import urlparse

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "..", "src"))

from imperialism_format.constants import (  # noqa: E402
    COUNTRIES_1882, DEVELOPED_TERRAIN_RESOURCE, DIRECTIONS, RESOURCE,
    TERRAIN_TYPE, TERRAIN_UNDERLAY, TOWN_TYPE,
)

sys.path.insert(0, os.path.dirname(__file__))
import dialogs  # noqa: E402
import validate  # noqa: E402
from scenario_session import ScenarioSession  # noqa: E402
from session import WIRE_FIELDS  # noqa: E402

STATIC_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "static")
CONTENT_TYPES = {".html": "text/html", ".js": "text/javascript",
                 ".css": "text/css", ".ico": "image/x-icon"}


def encode_field(session, name):
    """Pack one cell field across the whole grid.

    Single-byte fields go over as base64; anything wider stays a plain array.
    At 6,480 cells the difference is a few hundred kilobytes per load.
    """
    values = [getattr(c, name) for c in session.map_session.map_file.cells]
    if all(0 <= v <= 255 for v in values):
        return {"enc": "b64", "data": base64.b64encode(bytes(values)).decode()}
    return {"enc": "raw", "data": values}


class Handler(BaseHTTPRequestHandler):
    session: ScenarioSession = None

    def log_message(self, fmt, *args):  # quieter console
        pass

    # --- plumbing ---------------------------------------------------------

    def _send(self, code, body, content_type="application/json"):
        payload = body if isinstance(body, bytes) else json.dumps(body).encode()
        self.send_response(code)
        self.send_header("Content-Type", content_type)
        self.send_header("Content-Length", str(len(payload)))
        self.send_header("Cache-Control", "no-store")
        self.end_headers()
        self.wfile.write(payload)

    def _body(self):
        length = int(self.headers.get("Content-Length") or 0)
        return json.loads(self.rfile.read(length) or b"{}")

    def _cells(self, positions):
        return [self.session.map_session.cell_dict(x, y) for x, y in positions]

    # --- routing ----------------------------------------------------------

    def do_GET(self):
        path = urlparse(self.path).path
        try:
            if path.startswith("/api/"):
                return self._api_get(path)
            return self._static(path)
        except Exception as exc:  # surface errors in the UI rather than a blank page
            self._send(500, {"error": str(exc)})

    def do_POST(self):
        path = urlparse(self.path).path
        try:
            self._api_post(path)
        except Exception as exc:
            self._send(500, {"error": str(exc)})

    def _static(self, path):
        rel = "index.html" if path in ("/", "") else path.lstrip("/")
        full = os.path.normpath(os.path.join(STATIC_DIR, rel))
        if not full.startswith(STATIC_DIR) or not os.path.isfile(full):
            return self._send(404, {"error": "not found"})
        ext = os.path.splitext(full)[1]
        with open(full, "rb") as f:
            self._send(200, f.read(), CONTENT_TYPES.get(ext, "application/octet-stream"))

    def _api_get(self, path):
        s = self.session
        m = s.map_session
        if path == "/api/map":
            return self._send(200, {
                "width": m.map_file.width,
                "height": m.map_file.height,
                "wrapX": m.wrap_x,
                "path": m.path,
                "fields": {f: encode_field(s, f) for f in WIRE_FIELDS},
                "overlays": s.spatial_overlays(),
                "units": s.units(),
            })
        if path == "/api/scenario":
            return self._send(200, s.summary())
        if path == "/api/info":
            return self._send(200, s.info_dict())
        if path == "/api/units":
            return self._send(200, s.units())
        if path == "/api/tables":
            return self._send(200, {
                "terrain": TERRAIN_TYPE, "underlay": TERRAIN_UNDERLAY,
                "resource": RESOURCE, "town": TOWN_TYPE,
                "countries": COUNTRIES_1882, "directions": DIRECTIONS,
                # Served rather than duplicated in JS: the paint tools and the
                # validator must agree on which terrain mandates which resource.
                "developedTerrain": DEVELOPED_TERRAIN_RESOURCE,
            })
        if path == "/api/diff":
            return self._send(200, {"cells": m.dirty_cells()})
        if path == "/api/validate":
            return self._send(200,
                              {"issues": validate.check(m.map_file, s.scenario)})
        if path == "/api/state":
            return self._send(200, {
                "dirty": m.dirty_cells().__len__(),
                "dirtyFiles": s.dirty(),
                "canUndo": bool(s.undo_stack),
                "canRedo": bool(s.redo_stack),
                "path": m.path,
                "scenarioPath": s.scenario_path,
                "infoPath": s.info_path,
            })
        return self._send(404, {"error": "no such endpoint"})

    def _api_post(self, path):
        s = self.session
        m = s.map_session
        body = self._body()
        if path == "/api/edit":
            edits = body.get("edits", [])
            # Refuse before applying rather than reporting afterwards, so the
            # map and the .scn are never simultaneously wrong — and so the
            # carry destination is worked out while the cell still knows its
            # province.
            if not body.get("acceptStranding"):
                stranded = s.would_strand(edits)
                if stranded:
                    return self._send(409, {"error": "would strand records",
                                            "stranded": stranded})
            changed = s.apply_map(edits, body.get("label", ""))
            return self._send(200, {"cells": self._cells(changed)})
        if path == "/api/units/move":
            return self._send(200, {"record": s.move_record(
                body["uid"], body["x"], body["y"]), "units": s.units()})
        if path == "/api/units/delete":
            s.delete_record(body["uid"])
            return self._send(200, {"units": s.units()})
        if path == "/api/units/add":
            return self._send(200, {"record": s.add_record(
                body["tag"], body["x"], body["y"],
                value=int(body.get("value", 0))), "units": s.units()})
        if path == "/api/scenario/edit":
            records = s.apply_scenario(body.get("edits", []), body.get("label", ""))
            return self._send(200, {"records": records, "scenario": s.summary()})
        if path == "/api/info/edit":
            s.apply_info(body.get("edits", []), body.get("label", ""))
            return self._send(200, {"info": s.info_dict()})
        if path in ("/api/undo", "/api/redo"):
            result = s.undo() if path.endswith("undo") else s.redo()
            # A step can land in any of the three files, so say which moved and
            # let the client refresh only that.
            return self._send(200, {
                "cells": self._cells(result.get("cells", [])),
                "scenario": s.summary() if result.get("scenario") else None,
                "info": s.info_dict() if result.get("info") else None,
            })
        if path == "/api/save":
            return self._send(200, {"saved": s.save()})
        if path == "/api/browse/save":
            # Save As names a *scenario*, not a file: type "s0" and the map,
            # the .scn and the .inf all follow it. A half-cloned scenario the
            # game cannot load would be worse than none.
            chosen = dialogs.ask_save(
                initialdir=os.path.dirname(os.path.abspath(m.path)),
                initialfile=os.path.basename(m.path))
            if not chosen:
                return self._send(200, {"cancelled": True})
            return self._send(200, {"saved": s.save_as(chosen)})
        if path == "/api/browse/open":
            # Opening replaces the document outright, discarding undo history,
            # so check before putting a dialog on screen rather than after.
            dirty = s.dirty()
            if (dirty["map"] or dirty["scenario"] or dirty["info"]) \
                    and not body.get("discardChanges"):
                return self._send(409, {"error": "unsaved changes", "dirty": dirty})
            chosen = dialogs.ask_open(
                initialdir=os.path.dirname(os.path.abspath(m.path)))
            if not chosen:
                return self._send(200, {"cancelled": True})
            Handler.session = ScenarioSession.open(chosen, wrap_x=m.wrap_x)
            return self._send(200, {"opened": chosen})
        return self._send(404, {"error": "no such endpoint"})


def main(argv=None):
    ap = argparse.ArgumentParser(description="Imperialism map editor")
    ap.add_argument("map", nargs="?",
                    help="path to a .map file; omit to be asked for one")
    ap.add_argument("--port", type=int, default=8731)
    ap.add_argument("--no-wrap", action="store_true",
                    help="treat the map as not wrapping east-west")
    ap.add_argument("--no-browser", action="store_true")
    args = ap.parse_args(argv)

    # Launched by double-clicking rather than from a shell, there is no path to
    # pass, so ask for one the same way Open does.
    chosen = args.map or dialogs.ask_open()
    if not chosen:
        print("No map chosen - nothing to edit.")
        return
    if not os.path.exists(chosen):
        raise SystemExit(f"No such file: {chosen}")

    Handler.session = ScenarioSession.open(chosen, wrap_x=not args.no_wrap)
    url = f"http://127.0.0.1:{args.port}/"
    server = ThreadingHTTPServer(("127.0.0.1", args.port), Handler)
    session = Handler.session
    print(f"Editing {chosen} ({session.map_session.map_file.width}x"
          f"{session.map_session.map_file.height})")
    for label, path in (("scenario", session.scenario_path),
                        ("briefing", session.info_path)):
        print(f"  {label}: {path or '(none alongside this map)'}")
    print(f"Open {url} - Ctrl+C to stop")
    if not args.no_browser:
        webbrowser.open(url)
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        print("\nstopped")


if __name__ == "__main__":
    main()
