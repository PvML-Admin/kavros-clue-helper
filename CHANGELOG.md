# Kavros Clue Helper

## 0.7.4

**Settings and input**
- Settings opens in front of the floating panel and can be used. The panel is no longer pushed to the front on a timer, so it does not take the mouse away from the game. That was the lag and the camera that would not turn. The panel still stays above other windows, without activating, while Settings is closed.

## 0.7.3

**Client**
- The helper stays on the Kavros window you chose. Another client getting focus, or sitting in front, does not take over reading or tracking. If that window closes, it is found again only by the same process.

**Panel**
- The floating panel stays on screen and above other windows when you click another application or the game loses focus. Exit, Pause and the minimise button behave as before. Minimising the chosen game still hides the panel.

## 0.7.2

**Treasure map**
- The Wilderness Agility Course stump map is recognised from the in-game treasure map scroll. The dig is a few steps south of the Wilderness Course gate, by the stumps. The level 44 wilderness obelisk, then north-east, is the other way.

## 0.7.1

**Fairy rings**
- Codes and walks were checked against the fairy ring list. A wrong direction was corrected where the ring's tile and the clue did not agree, including the Wizards' Tower ring, Tyras Camp, the Gu'Tanoth cave, and the walk from Miscellania to Etceteria.
- ALP is the Feldip Hills ring near Gu'Tanoth, so it is no longer used for the Lighthouse or the Barbarian Outpost shore. Those cards use the Games necklace, the Barbarian Course, and Relekka.
- When a fairy ring is the Fastest step, that step says "(portable fairy ring required to be the fastest)". The note that a portable ring can be made in ;;ig, and an unlimited one costs 5 Kavros dollars, stays on every ring step.
- 26 clues gained a fairy ring as Alternatively or Or. Hazelmere is fairy ring CLS, with the Yanille lodestone as the other way onto the island.

## 0.7.0

**Routes**
- 155 clues that had no usable teleport now have a Fastest route, an alternative, and a final step. 33 cards that already had a teleport were given a shorter walk. Hazelmere's ring and the 120 capes are never the first option.
- Every fairy ring step includes this note: a portable fairy ring can be made in ;;ig, and an unlimited one can be bought for 5 Kavros dollars.
- Queen Sigrid is in Etceteria castle. The banana plantation salute, Swensen, the Karamja Volcano dungeon, and the Pirates' Hideout (north-east from the Wilderness Course) use the newer routes.
- Wendlewick and the Witchaven fishing platform are not on Kavros. Those cards say they are not available, and the reader will not offer them.
- Entrana is the hops-patch teleport only. That is the only way onto the island here. The crystal-tree island west of Rellekka keeps the Barbarian Course route only, because the crossing is still unconfirmed. Spider island east of Yanille is walkable from Hazelmere's island over the log bridges and from the Yanille coast, so both routes stay.

**Scans**
- A dig spot you have already stood in range of turns grey. Clear, or a new scan, puts the colours back.
- After you tab away and come back, the helper searches the whole minimap again. It does not ask you to right-click if the minimap is still on screen.

**Treasure map**
- The map by the Wilderness Agility Course stumps uses the Wilderness Course teleport, then a short walk. The picture of that scroll was not in the files for this release, so the image match is the existing map template.

## 0.6.10

Thanks to PvML-Admin for this one.

**Clue panel**
- The last clue solve stays on the panel after you close the scroll, so you can still read the text, the map, and the directions while you walk.
- The badge says **Scroll closed** while that saved solve is showing. It does not say Found.
- Opening the same scroll again switches the badge back to **Clue found**. Nothing else is reset.
- Clear removes the saved solve. A new clue step replaces it.
- Puzzles are unchanged. A slider, lockbox, towers, knot, or compass still goes away when that interface closes, because the guidance only applies while the board is open.

## 0.6.9

**Scans**
- Once the minimap lock is on, a missed frame no longer drops you. The marker stays on the last good tile, the next search looks ahead of your movement, and a bad jump is ignored.
- A saved scan or clue no longer comes back as Found after you close the scroll. Found only appears while that scroll, scan, or puzzle is actually on screen.

**Puzzles**
- The slider count follows the moves you make. A frame in the middle of a slide is ignored, so the highlight and the "left" number stay put until the tile lands, then the count goes down.
- Right-clicking a tower or lockbox still keeps the last reading while the menu covers the board.

**Panel**
- Searching is plain text with moving dots, not a badge. Found stays a badge.
- Settings is the same height as the minimise button.
- Exit matches Pause and Clear. Rescan stays gold.

**Clues**
- Dig Site Exam Centre: get the key from a male barbarian in Barbarian Village, then the Archaeology Guild teleport and walk to the chest under the dragon head. A Digsite pendant or the Varrock lodestone is the other way.
- Luthas is an amulet of glory to Karamja, then a short walk south-east. The Karamja lodestone is the backup. No boat.

## 0.6.8

**Puzzles**
- Puzzle boards are looked for before a clue scroll is read. A slider, lockbox, towers or knot on screen takes over even if a clue was already open, and the capture is no longer cropped down to that old clue.
- Knot puzzles are also checked at the smaller interface sizes.
- Right-clicking a tower or lockbox no longer makes the guidance vanish. The last reading stays while the menu covers the board, and it drops only after the board itself is gone.

**Maps**
- Location maps and the compass zoom out across the whole surface map. Scan maps show the world around the region when you zoom out.
- The hint bars are gone from the compass, the scan map and clue maps. Tile numbers are no longer shown on the clue card. The pin on the map is unchanged.
- Every surface lodestone has its own icon, on the compass and on scan maps. Click a lodestone on either map to set your position there.

**Scans**
- If the scroll says you are too far away, or the minimap cannot be locked, the panel tells you to right-click your location on the map. It no longer sits on "Looking for your minimap".
- A position is only published once the minimap compass anchor agrees, so a guess is not drawn as your tile.
- Meerkat range is turned on when the scroll text or the pace count includes the extra five paces. The checkbox still overrides it.
- Zanaris clues and the Zanaris scan use Ctrl+T or ;;t, then Locations, then Zanaris.

**Clues**
- The new Mysterious Clue Scroll window is read ahead of other parchment in the scene. "Yawn in the rogues' general store" matches the Wilderness Bandit Camp clue.
- Hobgoblin Peninsula now routes through the Crafting Guild menu and no longer mentions snape grass. Falador lodestone remains the alternative.
- Terrorbird enclosure is a walk west from the Gnome Course. The farming-patch teleport stays as the alternative.
- The West Ardougne Recruiter is a few steps east of Dave's spellbook Ardougne teleport. The Ardougne lodestone stays as the other way there.
- Twirl on the Barbarian Village bridge is a short walk east from the Barbarian Village teleport. Edgeville lodestone, then south, stays as the alternative.
- A faster teleport does not replace the lodestone (or other) backup. Both stay on the card.
- Luthas is reached with an amulet of glory to Karamja, then a short walk south-east into the banana plantation. The Karamja lodestone, then east, is the backup. Charter ships and the Port Sarim crossing are not used.
- The Karamja shipyard, Musa Point docks, Entrana, and the Miscellania clues no longer send you onto a boat. Braindeath Island and the Witchaven fishing platform have no verified island teleport, so those cards say so instead of sending you to Pirate Pete or Holgart.
- Wendlewick still has no verified tile, so its marker was left unset.

**Install**
- The Windows installer shows a navy and gold Kavros splash while it unpacks.

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
