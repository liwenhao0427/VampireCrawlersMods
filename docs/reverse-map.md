# Vampire Crawlers Reverse Map

Baseline:

- Game: Vampire Crawlers
- Unity: 6000.0.62f1
- Build GUID: 8a901aaee48543148e76ec2bc34b2547
- Runtime: Windows x64 IL2CPP

## Analysis Commands

```powershell
.\scripts\Run-Cpp2IL.ps1
.\scripts\Run-Il2CppDumper.ps1
```

## Candidate Runtime Targets

| Status | Assembly/Type | Method | Purpose | Source | Notes |
| --- | --- | --- | --- | --- | --- |
| Verified | `Nosebleed.Pancake.View.EnemyView` | `LateUpdate`, `OnDestroy` | Refresh and remove enemy health labels | Cpp2IL + BepInEx interop | Low-risk UI overlay; does not mutate enemy model. |
| Verified | `Nosebleed.Pancake.View.EnemyView` | `CurrentHealth`, `CurrentRowIndex`, `EnemyModel.MaxHealth` | Per-enemy and front-row health values | Cpp2IL + BepInEx interop | Front row is calculated from the maximum tracked row index. |
| Verified | `Nosebleed.Pancake.View.EnemyView` | `LateUpdate`, `OnDestroy`, `CurrentHealth` | Turn damage samples | Cpp2IL + BepInEx interop | Samples the same live health value used by the health overlay and counts only actual health drops. |
| Verified | `Nosebleed.Pancake.GameLogic.GameStates.PlayerTurnState` | `OnEnterState` | Turn damage boundary | Cpp2IL + BepInEx interop | Starts a new displayed turn and finalizes the previous one. |
| Verified | `Nosebleed.Pancake.InputSystem.InputManager` | `Update` | Toggle developer console with `~` | Cpp2IL + BepInEx interop | Console is an overlay and does not mutate game prefabs. |
| Verified | `Nosebleed.Pancake.GameConfig.Accessors.Globals` | `Progression.TotalCoins.ModifyValue` | Add coins from developer console | Cpp2IL + BepInEx interop | Uses existing `IntAsset` notification path. |
| Build-verified | `Nosebleed.Pancake.Achievements.AchievementManager` | `DEBUG_ForceUnlockAllAchievements` | Developer console achievement unlock command | Cpp2IL + BepInEx interop | Invoked only on loaded scene instance. Needs in-game smoke test. |
| Build-verified | `Nosebleed.Pancake.MetaProgression.ProgressionData` | `DEBUG_UnlockRelics`, `DEBUG_ResetCardGemSlots`, `DEBUG_ResetGemFrequencies` | Developer console progression debug commands | Cpp2IL + BepInEx interop | Accessed through `Globals.Progression`. Needs in-game smoke test. |
| Build-verified | `Nosebleed.Pancake.Shops.Blacksmith.CardBlacksmith` | `DEBUG_UnlockAndDiscoverCards` | Developer console card unlock command | Cpp2IL + BepInEx interop | Invoked only when the blacksmith scene object is loaded. |
| Build-verified | `Nosebleed.Pancake.Shops.Blacksmith.GemJeweller` | `DEBUG_UnlockAllGems` | Developer console gem unlock command | Cpp2IL + BepInEx interop | Invoked only when the jeweller scene object is loaded. |
| Build-verified | `Nosebleed.Pancake.Shops.Crawlers.CharacterShop` | `DEBUG_AddCharacterSlot` | Developer console character slot command | Cpp2IL + BepInEx interop | Invoked only when the character shop scene object is loaded. |
| Build-verified | `Nosebleed.Pancake.Shops.Crawlers.CharacterShop` | `DEBUG_UnlockCrawlerAchievements` | Developer console crawler achievement command | Cpp2IL + BepInEx interop | Invoked only when the character shop scene object is loaded. |
| Build-verified | `Nosebleed.Pancake.Shops.Arcana.ArcanaShop` | `DEBUG_UnlockBuildingAndFirstArcana`, `DEBUG_UnlockArcanaAchievements` | Developer console arcana debug commands | Cpp2IL + BepInEx interop | Invoked only when the arcana shop scene object is loaded. |
| Build-verified | `Nosebleed.Pancake.Shops.Blacksmith.BlacksmithShopController` | `DEBUG_UnlockJewellerRelic` | Developer console blacksmith unlock command | Cpp2IL + BepInEx interop | Invoked only when the blacksmith controller is loaded. |
| Build-verified | `Nosebleed.Pancake.LevelSelect.MapLevelController` | `DEBUG_RefreshAllLevels`, `DEBUG_UnlockDungeonAchievements` | Developer console map debug commands | Cpp2IL + BepInEx interop | Invoked only when the world map controller is loaded. |
| Build-verified | `Nosebleed.Pancake.Models.PlayerModel`, `PlayerStats`, `SimpleStat`, `PlayerXpBarView`, `ChooseCardModal`, `CardConfig`, `GemConfig`, `GemInsertionSequence`, `CardStatOfferingTableModal`, `OfferingTableEventView`, `DungeonModel.CurrentPassiveEvent` | `Health.SetValue`, `SetBaseStat`, `StatChanged`, `LevelUp`, `TryOpenChooseCardModal`, `DoViewLevelUp`, `ChooseCardModal.PopulateCardRewardChoices`, `AddOneXp`, `SubtractOneXp`, `AddCardConfigToHand`, `OnReceiveGem`, `Open`, `CardConfig.GetEffectDescription`, private `onPlayEffect/onDrawEffect/onDiscardEffect` | In-run console actions: heal, edit stats/level/xp, choose card/gem, open sacrifice-card permanent-stat menu | Cpp2IL + BepInEx interop | Card/gem selector is blocked outside combat; opening only builds the title index, while the visible page lazily reads icon, mana cost, effect description, and replacement Sprite filename. Card descriptions first try the original card description API, then fall back to each configured `CardEffect` because card and gem descriptions use different config paths. Level-up has a fallback direct modal open; sacrifice menu targets the card-to-stat offering table and checks current passive event. |
| Build-verified | `Nosebleed.Pancake.Achievements.AchievementsSummaryScreen`, `AchievementItemView`, `AchievementManager`, `AchievementConfig`, `ProgressionData` | `OnEnable`, `SelectUnlock`, private `_selectedItem`, `DEBUG_ForceUnlockAchievement`, `DEBUG_ResetAchievement`, `DEBUG_ForceUnlockAllAchievements`, `ForceUnlockAchievement`, `RefreshView`, `UpdateDescriptionBox` | Reuse the original achievements menu and add buttons for selected unlock/reset plus unlock-all/reset-all | Cpp2IL + BepInEx interop | Separate `VampireCrawlers.UnlockController` plugin; controls are injected only while the original achievement menu is open. |
| Build-verified | `Nosebleed.Pancake.GameConfig.CardConfig`, `Nosebleed.Pancake.View.CardView` | `CardConfig.OnEnable`, `CardView.SetCardConfig` | Replace `CardConfig.sprites[index]` from same-name local PNG files | Cpp2IL + BepInEx interop + Addressables catalog inspection | Separate `VampireCrawlers.ImageReplacer` plugin; default lookup is `images/<SpriteName>.png`, with optional manifest fallback. Runtime now logs observed card Sprite names to confirm the exact filename needed. Needs in-game visual smoke test. |
| Build-verified | `Nosebleed.Pancake.GameConfig.GemConfig`, `GemChoiceView`, `GemSelectionView`, `AnimatedGemView`, `InsertGemModal`, `JewellerGemView` | `SetGem`, `SetupGemView`, `GemConfig.GemSprite` | Replace gem icon sprites from same-name local PNG files | Cpp2IL + BepInEx interop + Addressables catalog inspection | Separate `VampireCrawlers.ImageReplacer` plugin; applies before common gem display entrypoints. Runtime now logs observed gem Sprite names to confirm the exact filename needed. Needs in-game visual smoke test. |

## Patch Rules

- Record each hook target here before enabling it in code.
- Prefer stable manager lifecycle methods over per-frame combat loops.
- First commit for each target should log only; behavior changes come after the log path is verified in game.
- If Unity version or build GUID changes, rerun both analysis scripts and mark stale targets before patching.
