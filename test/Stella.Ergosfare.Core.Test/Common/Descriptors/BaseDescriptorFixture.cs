using Stella.Ergosfare.Test.Fixtures;

namespace Stella.Ergosfare.Core.Test.Common;


/// <summary>
/// Provides a base class for tests that require access to a shared <see cref="DescriptorFixture"/>.
/// </summary>
/// <param name="descriptorFixture">
/// The <see cref="DescriptorFixture"/> instance provided by xUnit's <c>IClassFixture</c>.
/// </param>
public abstract class BaseDescriptorFixture(
    DescriptorFixture descriptorFixture):
    IClassFixture<DescriptorFixture>
{

    /// <summary>
    /// Gets  the shared <see cref="DescriptorFixture"/> instance for use in derived test classes.
    /// </summary>
    protected DescriptorFixture DescriptorFixture { get; } = descriptorFixture;
}