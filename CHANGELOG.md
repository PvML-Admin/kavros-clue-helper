# Kavros Clue Helper

## 0.6.7

A small hotfix for two things that slipped through in 0.6.6.

**Floating panel banner**
- The "Updated to ..." banner on the floating clue panel no longer covers the Rescan / Pause buttons. The panel now grows a little to make room for it and shrinks back afterwards.
- The banner now disappears on its own after 5 seconds instead of staying forever.

**Settings window**
- What's new now has a **Close** button in its header. You can also press **Esc**, or click the What's new button again, to close it.
- The bottom banner in Settings also clears itself after 5 seconds. The only exception is "Update now", which stays until you click it so you don't miss an update.
- These patch notes are more detailed.

## 0.6.6

**New look**
- Settings and the floating panel have a navy and gold restyle (thanks to PvML-Admin for the redesign).

**Routes and teleports**
- Every route uses the same layout: the **Fastest** way first, then an **Alternatively** option when a lodestone is still reasonably close, then the final step to the clue.
- Closer Kavros teleports are now suggested where they beat the lodestone, for example **Ctrl+T or ;;t** menu teleports (Artisan's Workshop, Gnome Agility Course, Barbarian Course, Sawmill, Taverley), Ring of wealth to Miscellania, Amulet of farming patches, and fairy rings.
- Clue fixes:
  - Port Khazard: no more Karamja boat route (Kavros has no boat NPCs). It now uses the Yanille lodestone, Khazard Teleport, fairy ring DJP or the Ardougne farming patch.
  - Louisa: the marker now points to the side room where she actually stands on Kavros.
  - Sir Kay, Miscellania, Party Pete, the Gnomeball referee, the Odd Old Man, the Barbarian Course emote and the Ardougne Monastery crate all have corrected or faster routes.
  - Doric, the Dwarven Mine chests, Wizards' Tower and several Falador and Taverley clues have new faster routes.

**Clue reading**
- Text clues are read from the inner parchment only, so the scroll border and the game background no longer confuse the reader.
- Stray junk words from the border are ignored, and a clue that fails to read is retried instead of being remembered as unknown.
- This fixes clues that didn't get recognised before, such as "The beasts to my east...", the "ME AM THE CALC" anagram and similar short clues.

**Updates and patch notes**
- No more popups. Update checks show a small banner at the bottom of the window, with an **Update now** button when a new version is out.
- After updating, a banner tells you what version you're on, and **What's new** in Settings shows these notes.

**Kavros only**
- The helper only attaches to a game window whose title contains "Kavros". It keeps working through revision updates, but won't run on other servers.

**Floating panel**
- The floating panel is tied to the Kavros window. It stays on top of the game while you play, and hides when you tab out to another app.

**Slider puzzles**
- The solver always goes straight for the best route it can find. There's no more "Use quick route" button.
- Whole-row moves are used, since Kavros lets you slide a full row or column in one click.
- Only the next click is highlighted, with a pulsing outline, an arrow and a "N left" counter, so it's much easier to keep track of where to click.
- Identical-looking tiles (like the two near-matching tree pieces) are tracked properly, so they no longer get swapped by mistake.

## 0.6.5

- Fixed the update check failing to connect (it now uses TLS 1.2).
- The app's .exe.config file now ships with the installer, which the update check needs.
