using Google.Cloud.Firestore;

namespace WebAPI.Data
{
    [FirestoreData] // Thêm thuộc tính này
    public class Banner
    {
        [FirestoreProperty] // Đánh dấu thuộc tính Firestore
        public string Title { get; set; }

        [FirestoreProperty]
        public string NewsContent { get; set; }

        [FirestoreProperty]
        public DateTime CreatedAt { get; set; }

        [FirestoreProperty]
        public string ImageUrl { get; set; }

        [FirestoreProperty]
        public string Id { get; set; }
    }
}
