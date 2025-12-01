namespace SMacro.Domain;

public interface IMacroItem
{
    ushort Order { get; }
    
    MacroItemType ItemType { get; }
}