using System.ComponentModel.DataAnnotations;

namespace GE.BandSite.Database;

public abstract class Entity
{
    [Key]
    public Guid Id { get; set; }
}
