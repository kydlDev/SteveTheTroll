# Code audit SteveTheTroll

## unused

== SteveTheTroll: 15 never referenced
  SteveTheTroll/SteveTheTroll-Mod/src/Features/Riding/Domain/RideRules.cs:66 member CollectingPaused
  SteveTheTroll/SteveTheTroll-Mod/src/Features/Riding/Domain/RideRules.cs:69 member OrdersSuspended
  SteveTheTroll/SteveTheTroll-Mod/src/Features/Riding/Domain/RideRules.cs:72 member FollowsOrderKey
  SteveTheTroll/SteveTheTroll-Mod/src/Features/Riding/Domain/RideRules.cs:75 member GuardsTheRider
  SteveTheTroll/SteveTheTroll-Mod/src/Features/Riding/Domain/RideRules.cs:82 member DamageHitsPlayer
  SteveTheTroll/SteveTheTroll-Mod/src/Features/Riding/Infrastructure/RideSession.cs:178 member Recover
  SteveTheTroll/SteveTheTroll-Mod/src/Features/Trolls/Backpack/Domain/BackpackRules.cs:29 member Collects
  SteveTheTroll/SteveTheTroll-Mod/src/Features/Trolls/Combat/Domain/BreakRules.cs:138 member StaggerTaken
  SteveTheTroll/SteveTheTroll-Mod/src/Features/Trolls/Combat/Domain/CombatDecision.cs:45 member WhyNot
  SteveTheTroll/SteveTheTroll-Mod/src/Features/Trolls/Farming/Infrastructure/TripRules.cs:51 member EventOf
  SteveTheTroll/SteveTheTroll-Mod/src/Features/Trolls/Taming/Domain/TamingRules.cs:126 member Defends
  SteveTheTroll/SteveTheTroll-Mod/src/Features/Trolls/World/Domain/ZoneKeepRules.cs:32 member InClassicBand
  SteveTheTroll/SteveTheTroll-Mod/src/Testing/TestHooks/LedgerTestHooks.cs:9 class LedgerTestHooks
  SteveTheTroll/SteveTheTroll-Mod/src/Testing/TestHooks/SlipperTestHooks.cs:7 class SlipperTestHooks
  SteveTheTroll/SteveTheTroll-Mod/src/Testing/TestHooks/ZoneTestHooks.cs:11 class ZoneTestHooks

## dupes
== same name in several files
  AddHub                       HubTabs.cs:161, TestHooks.Part3.cs:276
  Apply                        SignFonts.cs:86, DepotGround.cs:235, TrollDepot.Part2.cs:28
  Begin                        TrollCounter.cs:241, TrollSlam.cs:38, TrollArrival.cs:65
  Build                        HubPrefab.cs:36, SlipperDropModel.cs:26, DepotPrefab.cs:131, TrollDepot.cs:371, TrollLedger.cs:219, HubTab.cs:30
  BusyReason                   IdleRules.cs:37, TrollIdle.cs:248
  Check                        HubRules.cs:77, TrollReturn.cs:86, TrollFrozenWatch.cs:131
  Clear                        FarmUnloadLedger.cs:40, TrollJournal.cs:140, TrollMapPins.cs:117
  Collect                      BackpackPickup.cs:196, PlainRocks.cs:53
  Decide                       RideRules.cs:37, Approach.cs:33, FarmGate.cs:75, OrderRules.cs:50
  Describe                     TrollGuard.Query.cs:34, TrollHold.cs:25, TrollMoveDiag.cs:19, TrollOwnerDiag.cs:14
  Disarm                       CombatTestHooks.Part2.cs:45, MobClearWatch.cs:39, MobSpawnBlock.cs:41, RouteClearWatch.cs:44
  DistanceToSegment            TrollPath.cs:208, RouteClearWatch.cs:54
  Execute                      BuildDumpCommand.cs:43, ClearCommand.cs:35, ClearDepotsCommand.cs:31, ComingPreviewCommand.cs:19, FillBackpackCommand.cs:24, LevelGroundCommand.cs:23
  FilePath                     RideSession.cs:58, TrollLedger.cs:105
  FloorLift                    DepotData.cs:46, DepotPrefab.cs:36
  Follow                       TrollCommands.cs:43, TestHooks.Part3.cs:366
  FollowedFarming              TestHooks.FollowedSpot.cs:134, ZoneTestHooks.cs:95
  Forget                       CostumeUnlock.cs:50, TrollGuard.Query.cs:9
  Format                       LedgerRules.cs:43, CliffTestHooks.Baked.cs:54
  FromEdge                     HubGeometry.cs:36, DepotRules.cs:19
  HeldByOther                  TargetRules.cs:107, FarmReservations.cs:21
  Holds                        HomeRules.cs:37, TrollHold.cs:57
  IconOf                       CostumeItems.cs:162, HubPrefab.cs:204
  InRange                      BackpackRules.cs:41, TrollBedHeal.cs:26
  Inside                       NoHitRules.cs:12, TrollNoHit.cs:84, BuildDumpCommand.cs:153
  InsideAny                    NoHitRules.cs:32, TrollNoHit.cs:78
  Install                      CostumeItems.cs:100, HubPrefab.cs:31, RideItem.cs:24, SlipperItem.cs:50, DepotGround.cs:65, DepotPrefab.cs:30
  IsHub                        HubUnlockRules.cs:12, HubPlacement.cs:79
  LegSeconds                   TargetRules.cs:29, StuckRules.cs:203
  MakeModel                    CostumeMenuData.cs:157, SlipperMenuData.cs:71
  MeshSpan                     DepotPrefab.cs:76, SlipperTestHooks.cs:46
  Moved                        FrozenRules.cs:32, TestHooks.cs:254
  Nearest                      HubGeometry.cs:13, HubRegistry.cs:134, HubSpeed.cs:44, GuardRules.cs:38, TrollOrders.cs:273
  Order                        TrollGuard.cs:155, TrollWalls.cs:46, BuildDumpCommand.cs:80
  Place                        TrollDepot.cs:420, ArenaTestHooks.cs:360
  Prune                        TrollPath.cs:197, TamedTrollSpeed.cs:62
  Reachable                    DepotSpots.cs:207, IdleRules.cs:74
  Reconcile                    TrollLedger.cs:170, LedgerTestHooks.cs:37
  Refresh                      HubRegistry.cs:57, SignFonts.cs:66, SlipperUpgradeCostPatch.cs:40, TrollRegistry.cs:192, TamedTrollStats.cs:46
  Release                      TrollDepot.cs:250, FarmReservations.cs:48
  Remove                       TrollDepot.Part2.cs:298, HubTabs.cs:176
  ReservedByOther              DepotRules.cs:37, TrollDepot.cs:227
  Reset                        ExploreTestHooks.cs:193, TestHooks.cs:59
  Ridden                       RidingPatches.cs:130, RidingTestHooks.cs:61
  SelfCheck                    BackpackStore.cs:59, TrollDefend.cs:42, TrollFarming.cs:72, ValheimTrollMovement.cs:71, TamedTrollSpeed.cs:47, TamedTrollStats.cs:33
  SetActive                    HubPlacement.cs:29, GapTestHooks.cs:295
  Shown                        TrollIdle.cs:196, TrollOrders.cs:442, TrollCommands.cs:36
  Slippers                     ModelKyPages.cs:58, CombatTestHooks.Part2.cs:213
  Target                       TrollReturn.cs:49, CombatTestHooks.cs:151
  TargetMethods                ClubBreaks.cs:30, SetTooltipPatch.cs:94, TrollFarmHits.cs:29, CostumePatches.cs:14, CostumePatches.cs:59, TrollsCombatPatches.cs:34
  Track                        SlipperUpgradeCostPatch.cs:30, TrollFlight.cs:21, TrollZones.Part2.cs:148
  Watch                        TrollIdle.cs:206, TrollOrders.cs:94
  Waypoint                     TargetRules.cs:69, FarmHomeTrip.cs:77
  Within                       TrollZones.Part2.cs:50, ClearCommand.cs:136
  Write                        MountFile.cs:14, BackpackStore.cs:105, LedgerTestHooks.cs:13

== identical bodies (>=5 lines)

