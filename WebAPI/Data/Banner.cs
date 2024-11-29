using Google.Cloud.Firestore;

namespace WebAPI.Data
{
    [FirestoreData] 
    public class Banner
    {
        [FirestoreProperty] 
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
