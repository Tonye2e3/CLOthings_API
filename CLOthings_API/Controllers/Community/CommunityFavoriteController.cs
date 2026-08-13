using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CLOthings_API.Models;

[Route("api/[controller]")]
[ApiController]
public class CommunityFavoriteController : ControllerBase
{
    private readonly CLOthingsContext _context;
    public CommunityFavoriteController(CLOthingsContext context)
    {
        _context = context;
    }

    // GET: api/CommunityFavorite/post/5/user/3
    // 查詢「這個使用者有沒有收藏過這篇貼文」，跟 PostLikeController 的
    // GetPostLikeByPostAndUser 是同一種寫法。有收藏就回傳那筆 Community_Favorite 紀錄
    // （帶著 communityFavoriteId，之後要取消收藏要用它），沒收藏過就回傳 null。
    [HttpGet("post/{communitypostid}/user/{userid}")]
    public async Task<FavoriteDTO> GetCommunityFavoriteByPostAndUser(int communitypostid, int userid)
    {
        var favorite = await _context.CommunityFavorite
            .Where(f => f.CommunityPostId == communitypostid && f.UserId == userid)
            .Select(f => new FavoriteDTO
            {
                CommunityFavoriteId = f.CommunityFavoriteId,
                UserId = f.UserId,
                CommunityPostId = f.CommunityPostId
            })
            .FirstOrDefaultAsync();

        if (favorite == null)
        {
            return null;
        }

        return favorite;
    }

    // GET: api/CommunityFavorite/user/5
    // 查詢「這個使用者收藏的所有貼文」，回傳完整的貼文資料（不是只有 FavoriteDTO），
    // UserProfileView.vue 的收藏頁籤要用這個。
    // 注意：CommunityFavorite 這個實體類別沒有直接關聯到 CommunityPost 的屬性，
    // 所以這裡分兩步查：先撈出收藏的 CommunityPostId 清單，再拿這份清單去查 CommunityPost 表，
    // 組法跟 CommunityPostController.cs 裡 GetCommunityPostByUser 幾乎一樣。
    [HttpGet("user/{userid}")]
    public async Task<IEnumerable<CommunityPostDTO>> GetCommunityFavoriteByUser(int userid)
    {
        var favoritePostIds = await _context.CommunityFavorite
            .Where(f => f.UserId == userid)
            .Select(f => f.CommunityPostId)
            .ToListAsync();

        return await _context.CommunityPost
            .Where(c => favoritePostIds.Contains(c.CommunityPostId))
            .Select(c => new CommunityPostDTO
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
                        Name = t.Product.ProductName
                    }).ToList()
            })
            .ToListAsync();
    }

    // POST: api/CommunityFavorite
    // 收藏：新增一筆 Community_Favorite 紀錄。
    [HttpPost]
    public async Task<ResultDTO> PostCommunityFavorite(FavoriteDTO favoriteDTO)
    {
        CommunityFavorite favorite = new CommunityFavorite
        {
            CommunityFavoriteId = 0,
            UserId = favoriteDTO.UserId,
            CommunityPostId = favoriteDTO.CommunityPostId
        };
        _context.CommunityFavorite.Add(favorite);
        await _context.SaveChangesAsync();

        return new ResultDTO { OK = true, Code = 204 };
    }

    // DELETE: api/CommunityFavorite/5
    // 取消收藏：刪除那筆 Community_Favorite 紀錄，5 要帶 communityFavoriteId
    // （從 GetCommunityFavoriteByPostAndUser 查回來的那個 id）。
    [HttpDelete("{communityfavoriteid}")]
    public async Task<ResultDTO> DeleteCommunityFavorite(int? communityfavoriteid)
    {
        var favorite = await _context.CommunityFavorite.FindAsync(communityfavoriteid);
        if (favorite == null)
        {
            return new ResultDTO { OK = false, Code = 404 };
        }
        try
        {
            _context.CommunityFavorite.Remove(favorite);
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return new ResultDTO { OK = false, Code = 500 };
        }
        return new ResultDTO { OK = true, Code = 204 };
    }
}