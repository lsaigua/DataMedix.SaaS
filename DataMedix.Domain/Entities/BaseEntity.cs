using System;
using System.Collections.Generic;
using System.Text;

namespace DataMedix.Domain.Entities
{
    public abstract class BaseEntity
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
