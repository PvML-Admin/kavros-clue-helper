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
