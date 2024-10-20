namespace WebAPI.Data
{
    public class RepairSchedules
    {
        public string Carname { get; set; }
        public string Cartype { get; set; }
        public string DamageDescription { get; set; } 
        public DateTime Date { get; set; }
        public string Service { get; set; }
        public string Staff { get; set; }
        public string Uid { get; set; }
        public string Id { get; set; }
        public string Username { get; set; }
        public string Status { get; set; }
        public List<string> ImageUrls { get; set; }  
    }
}

