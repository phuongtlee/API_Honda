namespace WebAPI.Data
{
    public class ProductDto
    {
        public string NameProduct { get; set; }
        public double Price { get; set; }
        public string Description { get; set; }
        public string Category { get; set; }
        public IFormFile Image { get; set; } 
    }
}
