# Kavros Clue Helper v0.7.8

v0.7.8: Puzzle numbers sit on the tiles again at 1440p and 4K, and they do not blink. Towers go 5, blank, then 1. Slider clicks move the whole row. The overlay shows up in screenshots. Save debug on the panel stores a picture and the detection log. A compass clue stays locked, and the overlay follows the game window.

Settings shows **Made by Zenity · v0.7.8**. The full notes are in CHANGELOG.md.

# Kavros Clue Helper v0.7.7

v0.7.7: The panel stays above the game and the other Kavros helpers, not above Discord or a browser. Lockbox and tower numbers no longer blink. Five medium clues that were missed now have cards. Port Khazard uses the Lunar Khazard Teleport. Thanks to PvML-Admin.

Settings shows **Made by Zenity · v0.7.7**. The full notes are in CHANGELOG.md.

# Kavros Clue Helper v0.7.6

v0.7.6: Clue cards use Fastest, Alternatively, Third option, Final step, and Clue. The lockbox no longer flickers. Scan tracking holds your position through missed frames. The tower puzzle locks on faster. The yellow search bar is removed. Thanks to PvML-Admin.

Settings shows **Made by Zenity · v0.7.6**. The full notes are in CHANGELOG.md.

# Kavros Clue Helper v0.7.5

v0.7.5: Master emote cards use the full-width map. Lodestone icons are drawn on every map, and the round compass replaces the letter N. Elite and master routes are filled in. Jatizso and Neitiznot use Ctrl+T > Monsters > Yaks. Mos Le'Harmless is not available on Kavros.

Settings shows **Made by Zenity · v0.7.5**. The full notes are in CHANGELOG.md.

# Kavros Clue Helper v0.7.4

v0.7.4: Settings opens in front of the floating panel again. The lag and frozen camera are fixed. The panel was taking focus from the game on a timer, and it no longer does.

Settings shows **Made by Zenity · v0.7.4**. The full notes are in CHANGELOG.md.

# Kavros Clue Helper v0.7.3

v0.7.3: the helper stays on the Kavros window you chose. Another client in front does not take over. The floating panel stays visible when you click another application.

Settings shows **Made by Zenity · v0.7.3**. The full notes are in CHANGELOG.md.

# Kavros Clue Helper v0.7.2

v0.7.2: the Wilderness Agility Course stump treasure map is recognised. The dig is a few steps south of the course gate, by the stumps. The level 44 wilderness obelisk, then north-east, is the other way.

Settings shows **Made by Zenity · v0.7.2**. The full notes are in CHANGELOG.md.

# Kavros Clue Helper v0.7.1

v0.7.1: fairy ring codes and walks were checked against the wiki list. ALP is no longer used for the Lighthouse or the Barbarian Outpost shore. A Fastest fairy ring step says "(portable fairy ring required to be the fastest)". 26 clues gained a fairy ring option.

Settings shows **Made by Zenity · v0.7.1**. The full notes are in CHANGELOG.md.

# Kavros Clue Helper v0.7.0

v0.7.0: new routes for clues that had no teleport, faster routes for 33 existing cards, scan dots you have already checked, and a treasure-map route by the Wilderness Agility Course.

Settings shows **Made by Zenity · v0.7.0**. The full notes are in CHANGELOG.md.

# Kavros Clue Helper v0.6.5

v0.6.5: fix update check (TLS 1.2), ship exe.config.

Settings shows **Made by Zenity · v0.6.5**. Install this build by downloading Setup.exe. Copies of 0.6.4 cannot download it themselves, because their update check fails before TLS 1.2 is offered.

# Kavros Clue Helper v0.6.4

18 November 2026 · Portable Windows release

## Finding the destination

- Coordinate, treasure map, search and NPC cards give a named destination and travel directions. The “under-cook” clue takes you from the Lumbridge lodestone into the castle kitchen, down the trapdoor and to the crate in the cellar.
- Added offline detail maps for all 345 catalogue entries with a known position, including interiors and upper floors. Use the wheel to zoom, drag to pan and right-click to reset a location card. The gold pin stays on the destination as you move the map.
- Fixed recognition of the Falador crossroads statue drawing. The card gives the dig offset of one tile south and three east. Added the reported 05°35′ south, 26°30′ east clue with directions to the desert west of Pollnivneach.
- Removed the repeated “Reference location” line. The helper keeps the current directions visible during background reads and replaces them when it recognises the next clue.

## Elite and Master scans

- The reader recognises the reported Elite scrolls for Brimhaven Dungeon, Wilderness volcano, Varrock and the Grand Exchange, Piscatoris Hunter Area, East or West Ardougne, Falador, Fremennik Slayer Dungeons, the desert east of the Elid and north of Nardah, Zanaris and Taverley Dungeon.
- All 20 supported Elite regions and both Master regions show travel directions alongside their possible dig spots, even before the helper finds the player. The scan map supports wheel zoom, drag to pan and right-click to set your own position.
- Heart of Gielinor tracking uses the shape of the platforms around the player. A grey marker keeps your last confirmed position visible when the tracker needs a new match.

## Interface and verification

- Fixed the clipped bottom puzzle option in Settings. Checked the layout at 100%, 125% and 150% scale.
- Replayed the reported Elite scrolls with both floor messages, the Falador and Galahad treasure maps, the Lumbridge crate and the desert coordinate clue. Checked all 24 catalogue treasure maps at three sizes.
- The Heart tracker matched the supplied game capture and four repeat crops within one tile. Continuous walking in the live Heart instance still needs an in-game test. The Wendlewick farmhouse has written directions. Its exact tile and the Wendlewick Elite scan spots remain unverified.

## Running the update

Exit the old helper, extract the entire ZIP into a new folder and open **KavrosClueHelper.exe**. Settings shows **Made by Zenity · v0.6.4**.
