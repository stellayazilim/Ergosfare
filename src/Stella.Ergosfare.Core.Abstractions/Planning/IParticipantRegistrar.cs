using System.Diagnostics.CodeAnalysis;

namespace Stella.Ergosfare.Core.Abstractions.Planning;

/// <summary>Connects generated closed participant types to the configured container.</summary>
public interface IParticipantRegistrar
{
    /// <summary>Registers a participant while leaving activation and lifetime ownership to the container.</summary>
    void Register<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TParticipant>()
        where TParticipant : class;
}
