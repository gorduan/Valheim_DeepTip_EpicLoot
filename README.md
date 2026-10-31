# DeepTip EpicLoot

Rest the mouse on an EpicLoot item and every property in its tooltip becomes explainable: hover a highlighted term and a
[DeepTip](https://thunderstore.io/c/valheim/p/Gorduan/DeepTip/) window tells you exactly what it means. Terms inside the
window open further windows, a click pins them.

## What is explained

- **Every magic effect** (all 181 of EpicLoot 0.15.4), e.g. *Duelist +8%*: what the value on the item means and what the
  effect really does, checked against EpicLoot's code (formula, conditions, how values from several items add up), plus
  where EpicLoot's own description does not match the code.
- **Values per rarity** (Magic to Ancient), **where an effect can appear** (item types, weapon skills, conditions) and
  **which effects exclude each other** (as links). These come from the definitions the game has loaded, so they always
  match your EpicLoot version and the server's config.
- **General terms**: rarity tiers, effect stars, roll range, number of effects, shard slots and shards, runes, item sets
  and their counter, legendary and mythic items, unidentified items, the enchanting table and its tabs, enchanting
  materials.
- Item name parts that come from effects (e.g. *Duelist's …*) link to their effect as well.

Languages: English and German. Other languages fall back to English.

## Requirements

- [DeepTip](https://thunderstore.io/c/valheim/p/Gorduan/DeepTip/) 1.0.0 or newer
- [EpicLoot](https://thunderstore.io/c/valheim/p/RandyKnapp/EpicLoot/) 0.15.4

Client side only; the server does not need it.

## Credits

Gorduan Community (GC). Free to use, modify and re-upload (MIT). EpicLoot is made by RandyKnapp and contributors; this
mod only reads its definitions and language files.
