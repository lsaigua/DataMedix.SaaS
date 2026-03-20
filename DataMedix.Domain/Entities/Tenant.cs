using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace DataMedix.Domain.Entities
{
    [Table("tenant")]
    public class Tenant
    {
        public Guid id { get; set; }
        public string name { get; set; } = default!;
        public string subdomain { get; set; } = default!;
        public bool isactive { get; set; }
    }
}
