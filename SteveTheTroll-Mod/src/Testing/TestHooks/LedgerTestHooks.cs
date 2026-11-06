using System.IO;
using KydlCore.Common;
using SteveTheTroll.Features.Trolls.Registry.Domain;
using SteveTheTroll.Features.Trolls.Registry.Infrastructure;

namespace SteveTheTroll.Testing.TestHooks;

/// <summary>NEXT_STEPS item 4 step 4: hooks for the troll ledger and its read-back. Every hook calls the mod's real TrollLedger on the real files of the running world.</summary>
internal static class LedgerTestHooks
{
    /// Refreshes the troll list and writes the ledger now. Expected outcome: the bytes written, 0 when nothing changed, -1 on a failure.
    [TestHook("stt.ledgerWrite")]
    private static int Write()
    {
        TrollRegistry.Trolls();
        return TrollLedger.WriteNow();
    }

    /// Does the ledger file have an entry for the followed troll with this `missing` flag? Expected outcome: true when it has.
    [TestHook("stt.ledgerEntry")]
    private static bool Entry(bool missing)
    {
        string why;
        foreach (LedgerEntry e in TrollLedger.ReadFile(out why))
        {
            if (e.Id == TestHooks.FollowedId())
            {
                return e.Missing == missing;
            }
        }

        return false;
    }

    /// The read-back now (the join delay and the once-per-world rule skipped). Expected outcome: how many troll(s) of the file are missing from the world.
    [TestHook("stt.ledgerReconcile")]
    private static int Reconcile()
    {
        return TrollLedger.Reconcile(true);
    }

    /// The read-back now, and did it find the FOLLOWED troll missing from the world? Expected outcome: true when it did.
    [TestHook("stt.ledgerReconcileFindsFollowed")]
    private static bool ReconcileFindsFollowed()
    {
        TrollLedger.Reconcile(true);
        foreach (LedgerEntry e in TrollLedger.LastMissing)
        {
            if (e.Id == TestHooks.FollowedId())
            {
                return true;
            }
        }

        return false;
    }

    /// Turns the file into one of ANOTHER world with the same name (world uid + 1), the way two worlds with the same name would meet. Expected outcome: true when the file was changed.
    [TestHook("stt.ledgerForeign")]
    private static bool Foreign()
    {
        string path = TrollLedger.FilePath();
        if (!File.Exists(path))
        {
            return false;
        }

        string text = File.ReadAllText(path);
        string world;
        long uid, player;
        LedgerRules.Parse(text, out world, out uid, out player);
        string changed = text.Replace("\"worldUid\": " + uid, "\"worldUid\": " + (uid + 1));
        File.WriteAllText(path, changed);
        return changed != text;
    }

    /// The folder name of the running world's ledger (name_uid). Expected outcome: the folder name.
    [TestHook("stt.ledgerFolder")]
    private static string Folder()
    {
        return TrollLedger.WorldFolder();
    }
}
