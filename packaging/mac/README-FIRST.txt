EMBERHOLD - macOS first run
===========================

The game is not notarized with Apple (that requires a paid developer
account), so macOS quarantines it after download. One-time unlock:

  1. Unzip (you probably already did).
  2. RIGHT-CLICK (or Ctrl-click) Emberhold.app  ->  Open
  3. In the dialog, click "Open".

That's it - from then on it launches normally with a double-click.

If you double-clicked first and got "Emberhold can't be opened" or
"is damaged", do ONE of these, then launch again:

  - System Settings -> Privacy & Security -> scroll down -> click
    "Open Anyway" next to the Emberhold message;  or
  - Open Terminal and run (adjust the path to wherever the app is):
        xattr -cr ~/Downloads/Emberhold.app

Pick the right download:
  - Apple Silicon Macs (M1 / M2 / M3 / M4):  the arm64 zip
  - Intel Macs:                              the x64 zip

Your profile and saves are stored per-user and survive updates.
