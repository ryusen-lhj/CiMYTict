using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CiMYTict.Models
{
    public class Promotion
    {
        public int PromotionId { get; set; }

        [Required, StringLength(30)]
        public string Code { get; set; } = string.Empty; // e.g., "CINE20", "STUDENT10"

        [StringLength(200)]
        public string Description { get; set; } = string.Empty;

        [Range(1, 100)]
        public int DiscountPercentage { get; set; }

        [Range(0.01, 1000)]
        [Column(TypeName = "decimal(10,2)")]
        public decimal MaxDiscountAmount { get; set; }

        public DateTime ValidFrom { get; set; }
        public DateTime ValidUntil { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
