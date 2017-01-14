using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AutoAllegro.Models
{
    public class AllegroRefundReason
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int Id { get; set; }
        public string Reason { get; set; }
    }
}
