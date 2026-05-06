using System.Text.Json;

namespace ApexComputerUse
{
    public partial class CommandProcessor
    {
        // -- Annotation verbs ----------------------------------------------
        // All annotation operations key on the stable element-hash. Callers may pass either
        // a numeric Id (resolved against the most recent /elements scan) or a Hash directly.
        // The hash is the canonical form because it survives session restarts; the id is
        // only stable within a session, so id-based mutation requires a fresh /elements call.

        private CommandResponse CmdAnnotate(CommandRequest req)
        {
            if (ElementAnnotations == null) return Fail("ElementAnnotationStore not initialised.");
            if (!TryResolveTargetHash(req, out string hash, out string err)) return Fail(err);

            string note = req.Value ?? req.Prompt ?? "";
            if (string.IsNullOrEmpty(note))
                return Fail("'annotate' requires a note in the value field.");

            (string? ct, string? n, string? aid) = LookupDescriptorForReq(req);
            var record = ElementAnnotations.SetNote(hash, note, ct, n, aid);
            return Ok($"Annotated {hash[..Math.Min(8, hash.Length)]}: {note}",
                      JsonSerializer.Serialize(record, FormatAdapter.s_compact));
        }

        private CommandResponse CmdUnannotate(CommandRequest req)
        {
            if (ElementAnnotations == null) return Fail("ElementAnnotationStore not initialised.");
            if (!TryResolveTargetHash(req, out string hash, out string err)) return Fail(err);
            var record = ElementAnnotations.SetNote(hash, null);
            return Ok($"Cleared note on {hash[..Math.Min(8, hash.Length)]}",
                      JsonSerializer.Serialize(record, FormatAdapter.s_compact));
        }

        private CommandResponse CmdExclude(CommandRequest req)
        {
            if (ElementAnnotations == null) return Fail("ElementAnnotationStore not initialised.");
            if (!TryResolveTargetHash(req, out string hash, out string err)) return Fail(err);
            (string? ct, string? n, string? aid) = LookupDescriptorForReq(req);
            var record = ElementAnnotations.SetExcluded(hash, true, ct, n, aid);
            return Ok($"Excluded {hash[..Math.Min(8, hash.Length)]} from default /elements responses",
                      JsonSerializer.Serialize(record, FormatAdapter.s_compact));
        }

        private CommandResponse CmdUnexclude(CommandRequest req)
        {
            if (ElementAnnotations == null) return Fail("ElementAnnotationStore not initialised.");
            if (!TryResolveTargetHash(req, out string hash, out string err)) return Fail(err);
            var record = ElementAnnotations.SetExcluded(hash, false);
            return Ok($"Restored {hash[..Math.Min(8, hash.Length)]}",
                      JsonSerializer.Serialize(record, FormatAdapter.s_compact));
        }

        private CommandResponse CmdListAnnotations(CommandRequest req)
        {
            if (ElementAnnotations == null) return Fail("ElementAnnotationStore not initialised.");
            var all = ElementAnnotations.ListAll();
            return Ok($"{all.Length} annotation(s)", JsonSerializer.Serialize(all, FormatAdapter.s_compact));
        }

        private CommandResponse CmdListExcluded()
        {
            if (ElementAnnotations == null) return Fail("ElementAnnotationStore not initialised.");
            var ex = ElementAnnotations.ListExcluded();
            return Ok($"{ex.Length} excluded element(s)", JsonSerializer.Serialize(ex, FormatAdapter.s_compact));
        }

        // -- Hash resolution helpers ---------------------------------------

        /// <summary>
        /// Resolves the request's target hash from either an explicit hash (preferred) or
        /// from a numeric id translated against the most recent /elements scan.
        /// </summary>
        private bool TryResolveTargetHash(CommandRequest req, out string hash, out string error)
        {
            // Explicit hash wins — it's the stable identifier.
            string? explicitHash = req.AutomationId;
            if (!string.IsNullOrEmpty(explicitHash) && !int.TryParse(explicitHash, out _))
            {
                hash  = explicitHash;
                error = "";
                return true;
            }

            // Numeric id path: id may be supplied via AutomationId (matches the existing
            // CommandRequest convention - /elements?id= and /find both reuse that field).
            if (!string.IsNullOrEmpty(explicitHash) && int.TryParse(explicitHash, out int id)
                && _elementHashes.TryGetValue(id, out var h) && !string.IsNullOrEmpty(h))
            {
                hash  = h;
                error = "";
                return true;
            }

            hash  = "";
            error = "Provide either id=<numeric> (from a recent /elements scan) or hash=<element hash>.";
            return false;
        }

        private (string? ct, string? name, string? aid) LookupDescriptorForReq(CommandRequest req)
        {
            // Only attempt descriptor lookup when the caller supplied a numeric id - otherwise
            // we have no way to fetch the descriptor and the existing record's values stick.
            if (!string.IsNullOrEmpty(req.AutomationId)
                && int.TryParse(req.AutomationId, out int id)
                && _elementDescriptors.TryGetValue(id, out var d))
                return (d.ControlType, d.Name, d.AutomationId);
            return (null, null, null);
        }
    }
}
