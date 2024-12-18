using System.Drawing;

namespace WebAPI.Data
{
    public class Vehicle
    {
        public string Id { get; set; }
        public string VehicleName { get; set; }
        public string IdUser { get; set; }
        public string VIN { get; set; }
        public string Brand {get; set;}
        public string Model { get; set;}
        public string Color { get; set;}
        public string LicensePlate { get; set;}
        public int KM { get; set; }
        public string PurchaseDate { get; set; }
        //public string Image {  get; set;}   

    }
}
