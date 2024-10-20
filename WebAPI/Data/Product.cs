using Google.Cloud.Firestore;
using System;

namespace WebAPI.Data
{
    [FirestoreData]  // Đánh dấu lớp là một Firestore document
    public class Product
    {
        [FirestoreProperty]  // Đánh dấu trường là một thuộc tính Firestore
        public string NameProduct { get; set; }
        
        [FirestoreProperty]  // Đánh dấu trường là một thuộc tính Firestore
        public string Id { get; set; }

        [FirestoreProperty]
        public double? Price { get; set; }  // Chuyển từ decimal sang double

        [FirestoreProperty]
        public string Description { get; set; }

        [FirestoreProperty]
        public string Category { get; set; }

        [FirestoreProperty]
        public DateTime AddedDate { get; set; }  // Firestore hỗ trợ kiểu DateTime

        [FirestoreProperty]
        public string ImageUrl { get; set; }  

    }
}
