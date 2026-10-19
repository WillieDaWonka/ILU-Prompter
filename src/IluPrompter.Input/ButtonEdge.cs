namespace IluPrompter.Input;

/// <summary>One button press or release decoded from a device report (shared by the providers' pure helpers).</summary>
internal readonly record struct ButtonEdge(string ControlId, bool IsPressed);
