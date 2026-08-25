namespace CLOthings_API.DTOs.Shop
{
    public class ReviewDto
    {
        public int ReviewId { get; set; }
        public int Rating { get; set; }
        public string ReviewComment { get; set; }
        public DateTimeOffset ReviewDatetime { get; set; }
        public string ReviewImg { get; set; }
        public string UserName { get; set; }   // 誰評的
    }
}
