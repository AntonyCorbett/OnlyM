namespace OnlyM.Tests;

/// <summary>
/// Test classes that create an OperatorViewModel. Each one registers with WeakReferenceMessenger.Default (shared
/// by the whole process), so they must not run in parallel: a request message such as ResetManualOrderMessage
/// would reach another class's view model too, and a second reply throws.
/// </summary>
[CollectionDefinition(Name)]
public sealed class OperatorViewModelCollection
{
    public const string Name = "OperatorViewModel";
}
