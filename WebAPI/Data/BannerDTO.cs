namespace WebAPI.Data
{
    public class BannerDTO
    {
        public string Title { get; set; }
        public string NewsContent { get; set; }
        public IFormFile Image { get; set; }
    }
}
