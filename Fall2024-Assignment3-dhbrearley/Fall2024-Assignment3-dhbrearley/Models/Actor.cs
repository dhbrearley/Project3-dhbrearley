using System;
using System.ComponentModel.DataAnnotations;
namespace Fall2024_Assignment3_dhbrearley.Models {
	public class Actor
	{
        public int Id { get; set; }

        [Required]
		public string Name { get; set; }

        [Required]
		public string Gender { get; set; }

        [Required]
		public int Age { get; set; }

        [Required]
		public string Link { get; set; }

		public byte[]? Photo { get; set; }

	}
}

