using KydlCore.Common;
using KydlCore.Core;
using SteveTheTroll.Configuration;
using SteveTheTroll.Features.Trolls.Contracts;
using SteveTheTroll.Features.Trolls.Registry.Infrastructure;

namespace SteveTheTroll.Features.Riding.Infrastructure;

/// <summary>
///     T-213: the rideable troll (MIGRATION_DECISIONS A4). Gate: [General] EnableAll AND [Trolls] Enable AND [Riding] Enable, read live. The halter item is added to the game
///     at start (Jotunn clones the prefab once, Plugin.Awake), so it exists only when the feature was on then; the mount / dismount, hover prompt and ride patches
///     check the gate on every use.
/// </summary>
internal sealed class RidingFeature : IFeature
{
    internal const string FeatureId = "riding";
    private readonly bool _installed;

    internal RidingFeature(bool installedAtStart)
    {
        _installed = installedAtStart;
    }

    public string Id => FeatureId;

    public bool IsEnabled() => ModSettings.RidingOn;

    public bool Start()
    {
        if (!_installed)
        {
            ModLog.Warn("RidingFeature: switched on, but the halter was not added to the game at start - restart the game to get it");
            return _installed;
        }

        TrollFrozenWatch.Excluded = RideSession.IsRidden;
        TrollRiding.Ridden = RideSession.IsRidden;
        ModLog.Verbose("RidingFeature: started");
        return true;
    }

    public bool Stop()
    {
        TrollFrozenWatch.Excluded = null;
        TrollRiding.Ridden = null;
        RideSession.End(Player.m_localPlayer, Domain.EndReason.PlayerDismounted);
        ModLog.Verbose("RidingFeature: stopped (a ride in progress ended; the item stays in the game until restart)");
        return true;
    }
}
