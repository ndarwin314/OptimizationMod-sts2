using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Migrations;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace OptimizationMod.OptimizationModCode.Save;

[Migration(typeof (SerializableRun), 20, 21)]
public class SerializableRunV20ToV21: MigrationBase<SerializableRun>
{
    protected override void ApplyMigration(MigratingData saveData)
    {
        Log.Info("SerializableRun migration v20 -> v21: Attempting to compress deck");
        var deck = saveData.GetAs<List<SerializableCard>>("deck");
        var newDeck = Serializable.DeckHelper(deck);
        saveData.Set("deck", newDeck);

    }
}