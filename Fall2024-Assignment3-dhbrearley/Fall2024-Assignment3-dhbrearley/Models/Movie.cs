using System.ComponentModel.DataAnnotations;

namespace Fall2024_Assignment3_dhbrearley.Models {

    public class Movie 
    {
        [Key]
        public int Id { get; set; }
    
        [Required]
        public string Title { get; set; }

        [Required]
        public string Link { get; set; }

        [Required]
        public string Genre { get; set; }

        [Required]
        public int Release { get; set; }

        public byte[]? Photo { get; set; }
    }
}




