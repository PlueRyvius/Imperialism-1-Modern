"""Native Open/Save dialogs, driven from the editor server.

The server runs on the same machine as the person using it, so it can put a
real Explorer dialog on screen rather than making them navigate a list in the
page.  That also means a chosen path needs no sandboxing: the file was picked
by hand in the OS dialog, which is the consent.

Tk is run in a **short-lived subprocess**, not in-process:

* The HTTP server answers requests on worker threads, and Tk misbehaves off
  the main thread.
* A dialog that hangs cannot wedge the editor — the call simply times out.
* No Tk state, event loop, or hidden root window leaks into a long-running
  process.

The child prints the chosen path on stdout and nothing at all when cancelled.
"""
from __future__ import annotations

import os
import subprocess
import sys

#: Long enough to think about where to put a file, short enough that a dialog
#: lost behind another window does not block the editor forever.
TIMEOUT_SECONDS = 300

FILETYPES = "(('Imperialism maps', '*.map'), ('All files', '*.*'))"

_CHILD = """
import sys
import tkinter as tk
from tkinter import filedialog

root = tk.Tk()
root.withdraw()
# Without this the dialog can open behind the browser window that triggered it.
root.attributes('-topmost', True)
kwargs = dict(title={title!r}, filetypes={filetypes},
              initialdir={initialdir!r} or None)
if {save!r}:
    path = filedialog.asksaveasfilename(
        defaultextension='.map', initialfile={initialfile!r} or None, **kwargs)
else:
    path = filedialog.askopenfilename(**kwargs)
root.destroy()
if path:
    # Write bytes explicitly: on Windows sys.stdout defaults to the console
    # codepage, which mangles accented characters in paths.
    sys.stdout.buffer.write(path.encode('utf-8'))
"""


class DialogUnavailable(RuntimeError):
    """Raised when no native dialog can be shown on this machine."""


def _run(script: str) -> str | None:
    try:
        done = subprocess.run(
            [sys.executable, "-c", script],
            capture_output=True, text=True, encoding="utf-8",
            timeout=TIMEOUT_SECONDS,
        )
    except subprocess.TimeoutExpired:
        return None
    if done.returncode != 0:
        raise DialogUnavailable(
            (done.stderr or "the file dialog could not be opened").strip().splitlines()[-1]
        )
    chosen = done.stdout.strip()
    return os.path.normpath(chosen) if chosen else None


def ask_open(initialdir: str = "") -> str | None:
    """Show an Open dialog. Returns the chosen path, or None if cancelled."""
    return _run(_CHILD.format(
        title="Open map", filetypes=FILETYPES, initialdir=initialdir,
        initialfile="", save=False,
    ))


def ask_save(initialdir: str = "", initialfile: str = "") -> str | None:
    """Show a Save As dialog. Returns the chosen path, or None if cancelled."""
    return _run(_CHILD.format(
        title="Save map as", filetypes=FILETYPES, initialdir=initialdir,
        initialfile=initialfile, save=True,
    ))
