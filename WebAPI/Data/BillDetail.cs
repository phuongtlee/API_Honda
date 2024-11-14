namespace WebAPI.Data
{
    public class BillDetail
    {
        public string Id { get; set; }
        public string CustomerUid { get; set; }  
        public string CustomerName { get; set; } 
        public string CarName { get; set; }      
        public string Date { get; set; }         
        public DateTime CreatedAt { get; set; }
        public List<ServiceBill> Services { get; set; } 
        public decimal TotalCost { get; set; }
        public string StaffName { get; set; }
    }
}
