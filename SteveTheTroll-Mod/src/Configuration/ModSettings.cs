using System.Collections;
using System.Reflection;
using BepInEx.Configuration;
using KydlCore.Common;
using SteveTheTroll.Features.Trolls.Combat.Domain;
using SteveTheTroll.Ui;
using UnityEngine;

namespace SteveTheTroll.Configuration;

/// <summary>
///     All settings in one place (user rule, every mod):
///     * PLAYER CONFIG (BepInEx entries below, bound once from Plugin.Awake): only what a player should change. Clear
///     descriptions, ranges,
///     drop-downs (enums). Descriptions never mention older versions, old names or old values.
///     * DEV-ONLY values (Tunable fields below): fixed values chosen by the user, NOT in the player's file. The user's
///     KydlTools mod (F7) finds
///     every static Tunable here by itself and lets the user change it in game - nothing to register.
///     When the user settles a value in game, it becomes the new fixed value / default here.
/// </summary>
internal static class ModSettings
{
    // [General]: the mod switch and one Enable per functionality (PROJECT_RULES 11), all in one place (user 2026-11-15)
    public static ConfigEntry<bool> EnableMod, SlippersEnable, CostumeEnable, TrollsEnable, HubEnable, RidingEnable;

    // [TrollHub]: the Troll Hub building (D-135); its switch (General / Enable Troll Hub) needs Enable Trolls too
    public static ConfigEntry<bool> HubAutoAssign;

    // [Trolls]: the attack key and two switches for what tamed trolls may break / mine (user 2026-11-15)
    public static ConfigEntry<KeyCode> AttackKey;
    public static ConfigEntry<bool> BreakWalls, MineGammeltroll;

    // [DebugLog]
    public static ConfigEntry<bool> DebugLog;

    // --- dev-only fixed values (Tunables): not in the config file, KydlTools changes them for testing -----------------------------------
    // --- T-008a troll taming (D-037 / D-038): the user's numbers. ---
    public static readonly Tunable<float> TameSeconds = new("TrollTaming", "TameSeconds", 60f, 5f, 600f,
        "Seconds a wild troll needs a slipper wearer near it to become tamed (user: 60 s).");

    public static readonly Tunable<float> TameRange = new("TrollTaming", "TameRange", 250f, 10f, 300f,
        "Metres from the wearer within which a wild troll is being tamed (user: 150 m; F7 value 250 m baked 2026-10-28).");

    public static readonly Tunable<float> OwnTrollRange = new("TrollTaming", "OwnTrollRange", 300f, 50f, 1000f,
        "Metres: a wearer whose own tamed troll is this close tames no second one; that troll walks to the wearer instead (user: 300 m).");

    // --- D-042 tamed trolls (always while tamed) and the troll spawn boost: the user's numbers. ---
    public static readonly Tunable<float> TamedHealthFactor = new("TrollTamed", "HealthFactor", 3f, 1f, 10f,
        "Tamed troll max health x this (user: 3).");

    public static readonly Tunable<float> TamedDamageFactor = new("TrollTamed", "DamageFactor", 3f, 1f, 10f,
        "Tamed troll attack damage x this (user: 3).");

    public static readonly Tunable<float> DefendRange = new("TrollTamed", "DefendRange", 50f, 5f, 100f,
        "Metres: own tamed trolls this close defend the player when the player is hit (user: 50 m, TEST-021).");

    public static readonly Tunable<float> FarmRange = new("TrollTamed", "FarmRange", 50f, 5f, 100f,
        "Metres around where a troll was sent (then around the owner's bed) in which it looks for its focus item (user: 50 m, TEST-021).");

    public static readonly Tunable<float> FarmQuickRange = new("TrollTamed", "FarmQuickRange", 20f, 5f, 60f,
        "Metres: first, fast search around the troll (focus item, then the other allowed items) (user: 20 m).");

    public static readonly Tunable<float> FarmMaxRange = new("TrollTamed", "FarmMaxRange", 150f, 50f, 200f,
        "Metres: last search around the troll; nothing here -> it walks to the owner's bed (user: 150 m).");

    public static readonly Tunable<float> FarmLongRange = new("TrollTamed", "FarmLongRange", 500f, 150f, 1000f,
        "Metres: long-range search around the troll, used only when nothing was found within FarmMaxRange. The troll walks towards the nearest source it knows of and then works as usual (user 2026-10-31: 500 m). Only objects this game session knows about are found (all of them in single player / on the host, the loaded area for a client on a dedicated server).");

    public static readonly Tunable<float> FarmReach = new("TrollTamed", "FarmReach", 2f, 1f, 6f,
        "Metres (flat) from a tree trunk / log / rock at which a farming troll stops walking and swings (user: 2 m, TEST-021).");

    public static readonly Tunable<bool> FarmSkipYoung = new("TrollTamed", "SkipYoungGrowth", true,
        "D-153: farming trolls leave saplings and bushes (Bush01, Beech_small, FirTree_small, shrub ...) alone - only full trees, logs, rocks and other tall sources are farmed.");

    public static readonly Tunable<bool> DepotRemoveEmpty = new("TrollBackpack", "RemoveEmptyDepots", true,
        "D-155: a depot structure built by trolls (frame + its 4 chests) disappears as soon as all its chests are empty. Chests built by players are never touched.");

    public static readonly Tunable<float> AutoFarmSeconds = new("TrollTamed", "AutoFarmSeconds", 120f, 10f, 600f,
        "Seconds a newly tamed troll waits at the player for an order; no order -> it farms the default priority on its own (user: 2 minutes).");

    public static readonly Tunable<float> HoldNearPlayerRange = new("TrollTamed", "HoldNearPlayerRange", 4f, 0f, 15f,
        "Metres: a tamed troll that is not following stands still while a player is this close (D-063, to open the backpack; 0 = off).");

    public static readonly Tunable<bool> TrollMapPins = new("TrollTamed", "MapPins", true,
        "Shows each own tamed troll on the map (costume head icon) at its live / last known place (D-081).");

    public static readonly Tunable<int> GlobalTameCap = new("TrollTaming", "GlobalTameCap", BreakRules.GlobalTameCap, 1,
        50, "D-123: most living tamed trolls one player may have in total, near or far (user: 15).");

    public static readonly Tunable<float> WadeDepth = new("TrollTamed", "WadeDepth", BreakRules.WadeDepth, 0.3f, 5f,
        "D-129: water depth (m) from which tamed trolls walk WadeFactor x faster (user: 3x speed from 1.5 m into the water).");

    public static readonly Tunable<float> WadeFactor = new("TrollTamed", "WadeFactor", BreakRules.WadeFactor, 1f, 5f,
        "D-129: tamed trolls walk / run this many times their vanilla speed in water from WadeDepth (user: 3x).");

    public static readonly Tunable<float> WaterKeepDistance = new("TrollTamed", "WaterKeepDistance",
        BreakRules.WaterKeepDistance, 0f, 40f,
        "D-131: metres a following tamed troll keeps from you while it is in water (room to steer the boat; user: 15 m; 0 = off, the game's 3 m follow).");

    public static readonly Tunable<float> SwimRunFactor = new("TrollTamed", "SwimRunFactor", 3f, 0.5f, 5f,
        "D-122: tamed trolls swim up to this many times their vanilla run speed; following the player they match the player's speed (user: up to 300 %).");

    public static readonly Tunable<float> SwimFollowAdvantage = new("TrollTamed", "SwimFollowAdvantage", 1.5f, 0f, 10f,
        "D-130: m/s a swimming tamed troll stays faster than the player it follows (so it catches up instead of only matching).");

    public static readonly Tunable<float> SwimAcceleration = new("TrollTamed", "SwimAcceleration", 0.5f, 0.05f, 1f,
        "D-130: how quickly a swimming tamed troll reaches its swim speed (vanilla about 0.05; 1 = at once).");

    public static readonly Tunable<float> SwimTurnFactor = new("TrollTamed", "SwimTurnFactor", 2f, 1f, 5f,
        "D-130: swimming tamed trolls turn this many times faster than vanilla.");

    public static readonly Tunable<float> TamedSpeedFactor = new("TrollTamed", "SpeedFactor", 1.25f, 1f, 2f,
        "D-111: tamed trolls move and animate (walk, run, swings) this many times faster (user: +25 %).");

    public static readonly Tunable<float> RageRange = new("TrollTamed", "RageRange", 10f, 0f, 30f,
        "Metres: own trolls this close to the player go into rage when the player is hit - swinging non-stop at everything hostile around (D-082; 0 = off).");

    public static readonly Tunable<float> RageCalmSeconds = new("TrollTamed", "RageCalmSeconds", 3f, 1f, 15f,
        "Seconds without a hit on the player before an enraged troll calms down (D-082).");

    public static readonly Tunable<float> CounterIgnoreSeconds = new("TrollTamed", "CounterIgnoreSeconds", 15f, 0f,
        120f,
        "Seconds a troll ignores hits on itself and keeps farming after it answered a hit (D-084: 3 slams / going for the enemy).");

    public static readonly Tunable<float> CounterChaseSeconds = new("TrollTamed", "CounterChaseSeconds", 30f, 5f, 120f,
        "Longest a troll goes after a creature that hit it again after the quiet time (D-084), in seconds.");

    public static readonly Tunable<float> TrollZoneRadius = new("TrollTamed", "ZoneRadius", 35f, 0f, 120f,
        "Metres around each own troll outside your area that stay loaded so it keeps farming (D-084; 35 since the user test of 2026-10-31: the furthest troll still farmed; 0 = off, the game's areas only).");

    public static readonly Tunable<float> BedHealPerSecond = new("TrollTamed", "BedHealPerSecond", 50f, 0f, 500f,
        "HP per second own tamed trolls regain near the bed (D-085; 0 = off).");

    public static readonly Tunable<float> BedHealRange = new("TrollTamed", "BedHealRange", 50f, 5f, 200f,
        "Metres around the bed where own tamed trolls regain BedHealPerSecond (D-085).");

    public static readonly Tunable<float> GuardOwnerRange = new("TrollTamed", "GuardOwnerRange", 20f, 5f, 60f,
        "Metres: a troll only runs at a creature that goes for the PLAYER when that creature is this close to the troll (D-081; creatures that attack the troll itself or hit the player: DefendRange).");

    public static readonly Tunable<int> CrowdCount = new("TrollTamed", "CrowdCount", 3, 2, 10,
        "This many creatures attacking the player / troll at once -> the troll does 4 ground slams first (D-065).");

    public static readonly Tunable<float> DeadKeepSeconds = new("TrollTamed", "DeadKeepSeconds", 60f, 5f, 600f,
        "Seconds a dead troll stays in the F6 lists before it is removed and its name is free again (user: 60 s).");

    public static readonly Tunable<float> SpawnBoostSeconds = new("TrollTamed", "SpawnBoostSeconds", 120f, 0f, 1800f,
        "Seconds between troll spawn tries (100 % chance, day and night) near a slipper wearer without a tamed troll; the game's own entry tries every 4000 s with 5 % (D-059; 0 = off).");

    public static readonly Tunable<float> SpawnBedRadius = new("TrollTamed", "SpawnBedRadius", 500f, 50f, 2000f,
        "Metres around the bed where troll spawns are boosted (D-084: cap 2 with the slippers and 0-1 tamed trolls, 6 with the whole set and up to 5; outside: the game's own rates).");

    public static readonly Tunable<float> HomeRadius = new("TrollTamed", "HomeRadius", 15f, 5f, 60f,
        "D-097: metres from the bed where own trolls wait / are put on logout (never inside the house).");

    public static readonly Tunable<float> NoHitRadius = new("TrollTamed", "NoHitRadius", 15f, 0f, 60f,
        "D-097: own tamed trolls never swing within this many metres of your bed (the house stays whole; 0 = off).");

    public static readonly Tunable<float> DepotMinFromBed = new("TrollBackpack", "DepotMinFromBed", 15f, 3f, 100f,
        "D-097: nearest a new troll depot stands to the bed (m).");

    public static readonly Tunable<float> DepotMaxFromBed = new("TrollBackpack", "DepotMaxFromBed", 50f, 10f, 150f,
        "D-097: farthest a new troll depot stands from the bed (m); a full troll unloads once this close.");

    public static readonly Tunable<float> SpawnBedMinRadius = new("TrollTamed", "SpawnBedMinRadius", 200f, 0f, 1000f,
        "D-094: boosted trolls spawn in any biome, but never closer than this to the bed (they must not wreck the house before they are tamed). Black Forest trolls closer than this do not spawn either while the boost runs.");

    // --- F6 menu 3D views (slippers / costume): studio light, live in F7 (user 2026-10-28: the costume looked white). Ranges = Ui.ViewerSettings. ---
    public static readonly Tunable<float> ViewerKeyLight = new("Viewer", "KeyLight", ViewerSettings.KeyLightDefault,
        ViewerSettings.KeyLightMin, ViewerSettings.KeyLightMax, "Main light of the F6 3D views (slippers, costume).");

    public static readonly Tunable<float> ViewerFillLight = new("Viewer", "FillLight", ViewerSettings.FillLightDefault,
        ViewerSettings.FillLightMin, ViewerSettings.FillLightMax, "Softer second light of the F6 3D views.");

    public static readonly Tunable<float> ViewerAmbient = new("Viewer", "Ambient", ViewerSettings.AmbientDefault,
        ViewerSettings.AmbientMin, ViewerSettings.AmbientMax,
        "Light from everywhere in the F6 3D views (grey level 0 - 1).");

    // --- T-008b backpack on tamed trolls (D-053): placement behind Spine2 in the troll's frame; the user tunes it in game (F7). ---
    public static readonly Tunable<float> PickupRange = new("TrollBackpack", "PickupRange", 50f, 5f, 100f,
        "Metres around a tamed troll from which wood, ores and scrap go into its backpack (user: 50 m).");

    public static readonly Tunable<float> BackpackScale = new("TrollBackpack", "Scale", 0.671f, 0.1f, 1.5f,
        "World size of the backpack (1 = the construction's 2 x 2 x 2 m).");

    public static readonly Tunable<float> BackpackBack = new("TrollBackpack", "Back", -0.733f, -1f, 3f,
        "Metres behind the troll's upper spine (Spine2) where the struts sit.");

    public static readonly Tunable<float> BackpackUp = new("TrollBackpack", "Up", 0.127f, -3f, 3f,
        "Metres up (+) / down (-) from the upper spine.");

    public static readonly Tunable<float> BackpackSide =
        new("TrollBackpack", "Side", 0f, -2f, 2f, "Metres to the troll's right (+) / left (-).");

    public static readonly Tunable<float> BackpackPitch =
        new("TrollBackpack", "Pitch", 0f, -90f, 90f, "Tilt forward / back, degrees.");

    public static readonly Tunable<float> BackpackYaw = new("TrollBackpack", "Yaw", 0f, -180f, 180f,
        "Turn around the up axis, degrees.");

    public static readonly Tunable<float> BackpackRoll =
        new("TrollBackpack", "Roll", 0f, -90f, 90f, "Lean left / right, degrees.");

    public static readonly Tunable<bool> ZoneClaimNearBand = new("TrollZones", "ClaimNearBand", false,
        "D-142: claim unowned objects also in kept zones the game already lists as near. OFF since the user tests of 2026-10-31 (the furthest troll still farmed, also with a 35 m radius); switch ON if a troll near the player stops harvesting.");

    public static readonly Tunable<float> HubNoSpawnRadius = new("TrollHub", "NoSpawnRadius", 50f, 0f, 150f,
        "User 2026-11-06: no wild creature spawns within this many metres of a Troll Hub - the same base area a workbench has (the game's PlayerBase effect area), so raids behave as at a workbench. 0 = off. Read when the game starts (restart after a change); pieces already built get the new value after a restart too.");

    public static readonly Tunable<float> HubSignFontSize = new("TrollHub", "SignFontSize", 20f, 5f, 100f,
        "D-135: font size of the Troll Hub sign (user: 20 after the second test; F7 changes it live); the letters stay on one horizontal line, the board grows with the name.");

    public static readonly Tunable<float> ExampleSpeed = new("Example", "ExampleSpeed", 1f, 0f, 10f,
        "Example dev-only value: replace or remove. Read it with ModSettings.ExampleSpeed.Value.");

    // --- D-166: the slippers carry their trolls in the bundle (prefab SlipperSkinParts, attach_skin); these two tint the GROUND look (SlipperDropModel) only. Placement defaults = Unity fit.
    public static readonly Tunable<float> SlipperBrightness = new("Slipper", "Brightness", 0.84f, 0.3f, 1.7f,
        "Brightness of the blue plush slipper (1 = as modelled; 0.84 = the user's choice, TEST-005).");

    public static readonly Tunable<float> SlipperRimBrightness = new("Slipper", "RimBrightness", 0.88f, 0.3f, 1.7f,
        "Brightness of the fluffy rim around the opening (1 = as modelled; 0.88 = the user's choice, TEST-005).");

    // --- D-077 Troll Club in the hand (TEST-032: "the club goes into the head mesh"): turns / slides the club around the grip, live (F7).
    // Axes in the hand: Outward = the tip towards the player's right in the rest pose, Up = the tip upwards, Twist = round the club.
    public static readonly Tunable<float> ClubOutward = new("Club", "TiltOutward", 20f, -60f, 60f,
        "Tilts the Troll Club's tip away from the head (degrees, + = outward).");

    public static readonly Tunable<float> ClubUp = new("Club", "TiltUp", 0f, -60f, 60f,
        "Tilts the Troll Club's tip up (+) / down (-), in degrees.");

    public static readonly Tunable<float> ClubTwist = new("Club", "Twist", 0f, -180f, 180f,
        "Turns the Troll Club round its own length, in degrees.");

    public static readonly Tunable<float> ClubSlide = new("Club", "Slide", 0f, -0.4f, 0.4f,
        "Slides the Troll Club through the hand (metres, + = the hand holds it further down).");

    // --- T-213 rideable troll: placement values to tune in game with KydlTools (F7); the settled values become the defaults here. All in metres, 0 = as exported from Unity. ---
    // 2026-11-06 (user: "controls in F7 to adjust the player's position in the chair a bit better", "make the seat not so wide"): how the rider sits (degrees, in the troll's frame) and how wide the seat is.
    // 2026-11-06 (user: "take the hands position from the moose holding reins and put it into our sitting position - sits like on a chair but the reins are in the hands"): the rider keeps the chair pose, the arms are bent to hold the reins.
    public static readonly Tunable<bool> RideHandsOn = new("TrollRiding", "HandsOnReins", true,
        "The rider's hands hold the reins (arms bent to the hand positions below). Off = the plain animation.");

    public static readonly Tunable<float> RideHandX = new("TrollRiding", "HandSpread", 0.22f, 0f, 0.8f,
        "Hands: distance of each hand from the middle line (m).");

    public static readonly Tunable<float> RideHandY = new("TrollRiding", "HandHeight", 0.15f, -0.6f, 0.8f,
        "Hands: height above the seat point (m, in the rider's frame).");

    public static readonly Tunable<float> RideHandZ = new("TrollRiding", "HandForward", 0.5f, 0f, 1.0f,
        "Hands: how far in front of the seat point (m).");

    public static readonly Tunable<float> RideRiderYaw = new("TrollRiding", "RiderYaw", 0f, -90f, 90f,
        "Rider turned left/right on the seat (degrees).");

    public static readonly Tunable<float> RideRiderPitch = new("TrollRiding", "RiderPitch", 0f, -45f, 45f,
        "Rider leaning forward (+) / back (-) on the seat (degrees).");

    public static readonly Tunable<float> RideRiderRoll = new("TrollRiding", "RiderRoll", 0f, -45f, 45f,
        "Rider leaning to the right (+) / left (-) on the seat (degrees).");

    public static readonly Tunable<bool> RideSeatGlue = new("TrollRiding", "SeatGlue", true,
        "Rider glued to the chair: the seat point follows the chair's bone exactly in every frame, position and lean (on). Off = the older behaviour: eased while the troll stands, lean only in attacks.");

    public static readonly Tunable<float> RideSeatTilt = new("TrollRiding", "SeatTilt", 1f, 0f, 1f,
        "Share of the seat's lean the rider takes over while the troll attacks or staggers (0 = the rider stays upright, 1 = leans exactly like the seat).");

    public static readonly Tunable<float> RideSeatWidth = new("TrollRiding", "SeatWidth", 1f, 0.3f, 1.5f,
        "Width of the seat (1 = as modelled, smaller = narrower).");

    public static readonly Tunable<float> RideSeatX = new("TrollRiding", "SeatX", 0f, -1.5f, 1.5f,
        "Seat position left/right (m, + = right of the troll).");

    public static readonly Tunable<float> RideSeatY = new("TrollRiding", "SeatY", -0.5f, -3f, 3f,
        "Seat position up/down (m, relative to the seat anchor).");

    /// 2026-11-06 (user: the reins are not in the rider's hands - the chair pose keeps the hands down): the sitting animation. 0 = lox (hands forward on reins), 1 = asksvin, 2 = moose, 3 = chair (the old one).
    public static readonly Tunable<int> RideAnimation = new("TrollRiding", "Animation", 3, 0, 3,
        "Rider pose: 0 lox, 1 asksvin, 2 moose, 3 chair (default; the hands are put on the reins by HandsOnReins). Takes effect on the next mount.");

    internal static readonly string[] RideAnimations = { "attach_lox", "attach_asksvin", "attach_moose", "attach_chair" };

    public static readonly Tunable<float> RideSeatZ = new("TrollRiding", "SeatZ", -0.15f, -3f, 3f,
        "Seat position back/forward (m, + = towards the troll head).");

    public static readonly Tunable<float> RideBodyX = new("TrollRiding", "BodyX", 0f, -1f, 1f,
        "Straps + seat model left/right (m).");

    public static readonly Tunable<float> RideBodyY = new("TrollRiding", "BodyY", 0f, -2f, 2f,
        "Straps + seat model up/down (m).");

    public static readonly Tunable<float> RideBodyZ = new("TrollRiding", "BodyZ", 0f, -2f, 2f,
        "Straps + seat model back/forward (m).");

    public static readonly Tunable<float> RideHeadX = new("TrollRiding", "HeadX", 0f, -1f, 1f,
        "Bit + rings model left/right (m).");

    public static readonly Tunable<float> RideHeadY = new("TrollRiding", "HeadY", 0f, -2f, 2f,
        "Bit + rings model up/down (m).");

    public static readonly Tunable<float> RideHeadZ = new("TrollRiding", "HeadZ", 0f, -2f, 2f,
        "Bit + rings model back/forward (m).");

    public static readonly Tunable<float> RideLeashSag = new("TrollRiding", "LeashSag", 0.12f, 0f, 0.6f,
        "How much the leash hangs between the bit and your hand (fraction of its length).");

    // 2026-11-06: the reins run around the head and neck, not through them; this widens (>1) or narrows the detour.
    public static readonly Tunable<float> RideReinWiden = new("TrollRiding", "ReinWiden", 1f, 0.6f, 2f,
        "How far out the reins pass beside the troll's head and neck (1 = default).");

    public static readonly Tunable<float> RideLeashWidth = new("TrollRiding", "LeashWidth", 0.1f, 0.02f, 0.4f,
        "Width of the leather leash (m).");

    /// Is the mod switched on ([General] Enable mod)? Off = inert: no effects, no menus; nothing built is removed.
    public static bool On => EnableMod == null || EnableMod.Value;

    /// A functionality is on only when Enable mod AND its own Enable are on (the rule itself is Functions.IsOn). Read live: the features (Bootstrap.ModInstaller)
    /// start / stop their tabs, KY-Items entries and ticks at once. The prefabs of a part that was off at game start are not added, so switching it ON needs a restart.
    public static bool SlippersOn => Functions.IsOn(On, SlippersEnable == null || SlippersEnable.Value);

    public static bool CostumeOn => Functions.IsOn(On, CostumeEnable == null || CostumeEnable.Value);
    public static bool TrollsOn => Functions.IsOn(On, TrollsEnable == null || TrollsEnable.Value);

    /// The rideable troll (T-213): [General] Enable mod AND Enable Trolls AND Enable Riding. Read live; the item needs a restart to appear.
    public static bool RidingOn => Functions.IsOn(TrollsOn, RidingEnable == null || RidingEnable.Value);

    /// The Troll Hubs (D-135): [General] Enable mod AND Enable Trolls AND Enable Troll Hub. Read live like the others.
    public static bool HubsOn => Functions.IsOn(TrollsOn, HubEnable == null || HubEnable.Value);

    /// 2-star tamed trolls break Ashlands fortress walls ([Trolls] Two star trolls break fortress walls).
    public static bool WallsOn => BreakWalls == null || BreakWalls.Value;

    /// Tamed trolls mine / break the Petrified Gammeltroll ([Trolls] Trolls mine Petrified Gammeltroll).
    public static bool GammeltrollOn => MineGammeltroll == null || MineGammeltroll.Value;

    /// Trolls with no hub chosen go to the nearest one ([TrollHub] AutoAssign).
    public static bool HubAuto => HubAutoAssign == null || HubAutoAssign.Value;

    /// The value the player had in an entry that moved or was renamed (still in the file as an orphan before DropRemoved): the first of the
    /// given (section, key, section, key, ...) pairs that holds a bool; true when there is none.
    private static bool Old(ConfigFile c, params string[] sectionAndKey)
    {
        // OrphanedEntries is not a public member of every BepInEx 5 build (ConfigTools.DropRemoved reaches it the same way)
        PropertyInfo orphans = typeof(ConfigFile).GetProperty("OrphanedEntries", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        var dict = orphans != null ? orphans.GetValue(c, null) as IDictionary : null;
        for (int i = 0; dict != null && i + 1 < sectionAndKey.Length; i += 2)
        {
            var def = new ConfigDefinition(sectionAndKey[i], sectionAndKey[i + 1]);
            bool b;
            if (dict.Contains(def) && bool.TryParse((dict[def] as string ?? "").Trim(), out b))
            {
                return b;
            }
        }

        return true;
    }

    /// Expected outcome: every player entry is bound (returns that check).
    public static bool Bind(ConfigFile c)
    {
        string[]
            savedLines =
                ConfigTools.Capture(c); // before the first Bind: every Bind saves the file with what BepInEx parsed

        // 2026-11-15 (user): the Enable switches moved to [General] under new names; the player's old values are carried over (read from the
        // old entries before DropRemoved deletes them).
        EnableMod = c.Bind("General", "Enable mod", Old(c, "General", "EnableAll"),
            "Switches the whole mod on / off while playing. Off: no effects and no menus from this mod; things already built stay in the world.");
        SlippersEnable = c.Bind("General", "Enable Slippers", Old(c, "General", "Enable slippers", "Slippers", "Enable"),
            "The Fluffy Slippers with the little plush trolls on your feet. Off: their menu tab goes away at once; the item itself is added when the game starts, so switching this on needs a game restart.");
        CostumeEnable = c.Bind("General", "Enable Costume", Old(c, "General", "Enable costume", "Costume", "Enable"),
            "The troll costume (head, suit, trousers), the Troll Club and the set effects. Off: their menu tab goes away at once; the items are added when the game starts, so switching this on needs a game restart.");
        TrollsEnable = c.Bind("General", "Enable Trolls", Old(c, "General", "Enable trolls", "Trolls", "Enable"),
            "Taming and farming of real trolls with the slippers, their backpack and depots, map pins and extra trolls near your bed. Off: the troll menu tabs and the background troll work (pick-up, depots, orders, map pins) stop at once; everything else follows after a game restart, and switching this on also needs a restart.");
        HubEnable = c.Bind("General", "Enable Troll Hub", Old(c, "General", "Enable trollHub", "TrollHub", "Enable"),
            "The Troll Hub building: where your trolls go when you log out and unload, with a sign you can name. Off: trolls use the bed and the Troll Hub tab goes away at once; the building is added when the game starts, so switching this on needs a game restart.");
        RidingEnable = c.Bind("General", "Enable Riding", Old(c, "General", "Enable riding", "Riding", "Enable"),
            "The Troll Halter and Seat: craft it (workbench level 3), keep it in your inventory and use the alternative interact key on a tamed troll to ride it. Off: riding stops at once; the item is added when the game starts, so switching this on needs a game restart.");
        AttackKey = c.Bind("Trolls", "AttackKey", KeyCode.Mouse3,
            "Look at a creature, tree, log or rock and press this: your nearest tamed troll attacks / breaks it (Mouse3 = the mouse's back button, Mouse 4). Looking at nothing: it goes farming. None = off.");
        BreakWalls = c.Bind("Trolls", "Two star trolls break fortress walls", true,
            "On: your tamed trolls with two stars break Ashlands fortress walls (on your order, or when a wall is in the way to an enemy). Off: no troll damages them.");
        MineGammeltroll = c.Bind("Trolls", "Trolls mine Petrified Gammeltroll", true,
            "On: your tamed trolls break and mine the Petrified Gammeltroll like other rocks. Off: they leave it alone.");
        HubAutoAssign = c.Bind("TrollHub", "AutoAssign", true,
            "On: a troll you did not send to a hub yourself uses the hub nearest to it. Off: only trolls you assigned in the Troll Hub tab use a hub, the others use the bed.");
        DebugLog = c.Bind("DebugLog", "Enable", false,
            "Writes detailed lines about everything the mod does to the BepInEx log (for bug reports). Leave off while playing normally.");

        ConfigTools.ApplyCommaFix(c, savedLines); // after every Bind: "3,5" typed by hand means 3.5, not 35
        ConfigTools.DropRemoved(c);

        EnableMod.SettingChanged += (s, e) => ModLog.Info("Config: Enable mod = " + EnableMod.Value +
                                                          (EnableMod.Value
                                                              ? ", the mod is on"
                                                              : ", the mod is inert (no effects or menus)"));
        ModLog.Verbose("Config: bound (Enable mod " + EnableMod.Value + ", slippers " + SlippersOn + ", costume " +
                       CostumeOn + ", trolls " + TrollsOn + ", attack key " + AttackKey.Value + ", debug log " +
                       DebugLog.Value + ")");
        return EnableMod != null && SlippersEnable != null && CostumeEnable != null && TrollsEnable != null &&
               HubEnable != null && HubAutoAssign != null && RidingEnable != null && AttackKey != null && BreakWalls != null && MineGammeltroll != null && DebugLog != null;
    }
}
