namespace CLOthings_API.DTOs.Shop
{
    public class CreateReviewDto
    {
        public int OrderDetailId { get; set; }
        public int Rating { get; set; }
        public string ReviewComment { get; set; }
    }
}
