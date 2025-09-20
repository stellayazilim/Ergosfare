namespace Ergosfare.Test;

/// <summary>
/// A placeholder test class to prevent xUnit from throwing discovery errors
/// when no other tests are present in a project or test assembly.
/// </summary>
public class DummyTests
{
    /// <summary>
    /// A dummy test method that is skipped.
    /// It exists solely to satisfy xUnit's requirement for at least one test.
    /// </summary>
    [Fact(Skip = "Placeholder to prevent xUnit discovery error.")]
    public void DummyTest() { }
}