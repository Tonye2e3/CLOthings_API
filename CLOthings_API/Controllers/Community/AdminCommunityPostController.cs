using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using CLOthings_API.Models;

// 這支 Controller 是社群貼文的「後台管理」專用，加在類別上的 [Authorize(Roles = ...)]
// 會套用到裡面每一個方法，不用每個方法都各自加一次——只有 Admin、SuperAdmin 能打這支的任何端點。
// 跟 CommunityPostController 的差別：那支是給前台一般使用者用的（瀏覽、發文、留言...），
// 這支是給後台管理畫面用的（AdminCommunityPostListView.vue、AdminCommunityPostDetailView.vue）。
[Route("api/[controller]")]
[ApiController]
[Authorize(Roles = "Admin,SuperAdmin")]
public class AdminCommunityPostController : ControllerBase
{
    private readonly CLOthingsContext _context;
    public AdminCommunityPostController(CLOthingsContext context)
    {
        _context = context;
    }

    // GET: api/AdminCommunityPost
    // 回傳「全部狀態」的貼文（public、hide、check 都有）。
    // 跟 CommunityPostController.GetCommunityPost() 組法幾乎一樣，差別只是這裡沒有
    // .Where(c => c.Status == "public") 這個篩選，因為後台本來就要看到全部狀態才能管理。
    [HttpGet]
    public async Task<IEnumerable<CommunityPostDTO>> GetAllPosts()
    {
        return await _context.CommunityPost.Select(c => new CommunityPostDTO
        {
            CommunityPostId = c.CommunityPostId,
            UserId = c.UserId,
            Content = c.Content,
            PostDate = c.PostDate,
            Status = c.Status,
            User = new UserSummaryDTO
            {
                UserId = c.User.UserId,
                Name = c.User.Username,
                Avatar = c.User.UserProfile.Select(p => p.Avatar).FirstOrDefault()
            },
            Images = c.PostImage
                .OrderBy(i => i.SortOrder)
                .Select(i => new PostImageDTO
                {
                    PostImageId = i.PostImageId,
                    ImageFileName = i.ImageFileName,
                    SortOrder = i.SortOrder
                }).ToList(),
            LikesCount = c.PostLike.Count(),
            CommentsCount = c.PostComment.Count(),
            TaggedProducts = c.PostTaggedProduct
                .Select(t => new TaggedProductDTO
                {
                    PostTaggedProductId = t.PostTaggedProductId,
                    ProductId = t.ProductId,
                    ProductRoute = t.ProductRoute,
                    Name = t.Product.ProductName,
                    Image = t.Product.ProductImg.Select(pi => pi.ProductImgFile).FirstOrDefault(),
                    Price = t.Product.Price
                }).ToList()
        }).ToListAsync();
    }
}