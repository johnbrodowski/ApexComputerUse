namespace ApexComputerUse
{
    /// <summary>
    /// Shared entry point for every transport (Form1, Telegram, Pipe, HTTP). Wraps
    /// <see cref="CommandProcessor.Process"/> with a single error boundary so a failure
    /// in a processor path cannot tear down a transport handler. Transports still own
    /// their own input parsing and output formatting — that variance is legitimate —
    /// but command semantics live in exactly one place.
    /// </summary>
    public sealed class CommandDispatcher
    {
        private readonly CommandProcessor _processor;

        public CommandDispatcher(CommandProcessor processor)
        {
            _processor = processor;
        }

        public CommandResponse Dispatch(CommandRequest req)
        {
            try
            {
                return _processor.Process(req);
            }
            catch (Exception ex)
            {
                // Processor already catches inside its lock; this is a belt-and-braces
                // boundary for transport-level invariants (e.g. an adapter mutating req
                // or a future pre-dispatch hook throwing).
                return new CommandResponse { Success = false, Message = ex.Message };
            }
        }
    }
}
