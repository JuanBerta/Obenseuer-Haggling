# Haggling

A new **Haggling** skill for Obenseuer. Trade with traders to level it up, and get better prices as you go.

## Features
- **A real skill on the Character tab.** It looks like the game's own skills, starts unlearned, and shows the "New skill learned" and level-up popups.
- **XP from trading.** Every purchase or sale with a trader gives XP, more for bigger trades. Each trade on the same day gives a little less, so the grind stays fair.
- **Better prices.** Up to 12% off what you buy and 12% more for what you sell at level 10. The prices shown in the trade screen already include your bonus.
- **No money loops.** A trader never pays you more for an item than you'd pay to buy it back.
- **Your state matters.** Alcohol shrinks the price bonus but gives extra XP, like the game's skills (worse odds, more learning). Your Drinking skill shrinks the penalty, and mushrooms change your effective level using the game's own formula.
- **Level 10 perk, Silver Tongue:** traders who don't normally buy stolen goods will buy them from you at the stolen price. They still won't buy back what was stolen from them.
- XP is saved with your save game and belongs to that character only.

Every number can be changed in the config file or with ConfigurationManager.

## Install
1. Install BepInEx 6 (Unity.Mono, x64) for Obenseuer.
2. Put `Obenseuer-Haggling.dll` in `BepInEx\plugins\Obenseuer-Haggling\`.

## Build
`dotnet build -c Release` (set `/p:ObenseuerDir=...` if the game is not in the default Steam folder).

Made by JuanBerta, with AI assistance (Claude).
