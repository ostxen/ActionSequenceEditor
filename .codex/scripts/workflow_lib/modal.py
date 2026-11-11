"""Fixed Windows save-dialog cancellation, never arbitrary UI interaction."""
import ctypes
from ctypes import wintypes
import os

from .core import blocked


def save_dialog(pid):
    if os.name != "nt":
        blocked("Fixed save-dialog recovery is Windows-only.")
    user = ctypes.windll.user32
    user.GetDlgItem.argtypes = [wintypes.HWND, ctypes.c_int]
    user.GetDlgItem.restype = wintypes.HWND
    user.GetWindowTextW.argtypes = [wintypes.HWND, wintypes.LPWSTR, ctypes.c_int]
    user.GetClassNameW.argtypes = [wintypes.HWND, wintypes.LPWSTR, ctypes.c_int]
    found = []

    def text(hwnd, class_name=False):
        buffer = ctypes.create_unicode_buffer(256)
        (user.GetClassNameW if class_name else user.GetWindowTextW)(hwnd, buffer, 256)
        return buffer.value

    @ctypes.WINFUNCTYPE(wintypes.BOOL, wintypes.HWND, wintypes.LPARAM)
    def visit(hwnd, _):
        owner = wintypes.DWORD()
        user.GetWindowThreadProcessId(hwnd, ctypes.byref(owner))
        if owner.value == pid and user.IsWindowVisible(hwnd) and text(hwnd, True) == "#32770" and text(hwnd) in ("保存", "Save", "Save As", "另存为"):
            button = user.GetDlgItem(hwnd, 2)
            if button and user.IsWindowEnabled(button) and text(button).replace("&", "") in ("取消", "Cancel"):
                found.append({"pid": pid, "dialog": int(hwnd), "class": "#32770", "title": text(hwnd), "cancelButton": int(button), "operation": "Post WM_COMMAND IDCANCEL only; no file selection, save, overwrite or process termination"})
        return True

    user.EnumWindows(visit, 0)
    if len(found) != 1:
        blocked("Expected exactly one project-owned native save dialog with IDCANCEL; no action taken.")
    return found[0]


def cancel_save_dialog(preview):
    current = save_dialog(preview["pid"])
    if current != preview:
        blocked("Native save dialog changed; no action taken.")
    user = ctypes.windll.user32
    user.PostMessageW.argtypes = [wintypes.HWND, wintypes.UINT, wintypes.WPARAM, wintypes.LPARAM]
    if not user.PostMessageW(current["dialog"], 0x0111, 2, current["cancelButton"]):
        blocked("Cancel message was not accepted; inspect state, do not replay.")
    return {"status": "cancel_requested", "pid": current["pid"], "dialog": current["dialog"], "savedFiles": False}
