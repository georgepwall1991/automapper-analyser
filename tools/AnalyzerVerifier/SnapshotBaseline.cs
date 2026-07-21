namespace AnalyzerVerifier;

public static class SnapshotBaseline
{
    public static void EnsurePopulated(int diagnosticCount, string failureContext)
    {
        if (diagnosticCount > 0)
        {
            return;
        }

        throw new InvalidDataException(
            "Refusing to replace the sample diagnostics snapshot with an empty baseline. "
            + "Restore and build the sample project, then retry. Context: "
            + failureContext);
    }
}
