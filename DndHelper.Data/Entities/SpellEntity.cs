namespace DndHelper.Data.Entities;

public class SpellEntity
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Level { get; set; }
    public string School { get; set; } = string.Empty;

    /// <summary> Class names wrapped in pipes e.g. "|Wizard|Sorcerer|", so a LIKE query can match whole names. </summary>
    public string ClassDelimited { get; set; } = "|";

    public bool Concentration { get; set; }
    public bool Ritual { get; set; }
    public string SourceKey { get; set; } = string.Empty;
    public string SourceName { get; set; } = string.Empty;
    public string Json { get; set; } = string.Empty;
}