using Google.Cloud.Firestore;
using System;

namespace WebAPI.Data
{
    [FirestoreData]  
    public class Product
    {
        [FirestoreProperty]  
        public string NameProduct { get; set; }
        
        [FirestoreProperty] 
        public string Id { get; set; }

        [FirestoreProperty]
        public double? Price { get; set; }  

        [FirestoreProperty]
        public string Description { get; set; }

        [FirestoreProperty]
        public string Category { get; set; }

        [FirestoreProperty]
        public DateTime AddedDate { get; set; }  

        [FirestoreProperty]
        public string ImageUrl { get; set; }  

    }
}
