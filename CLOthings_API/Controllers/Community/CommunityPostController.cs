using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CLOthings_API.Models;

[Route("api/[controller]")]
[ApiController]
public class CommunityPostController : ControllerBase
{
    private readonly CLOthingsContext _context;
    public CommunityPostController(CLOthingsContext context)
    {
        _context = context;
    }

    // GET: api/CommunityPost
    [HttpGet]
    public async Task<IEnumerable<CommunityPostDTO>> GetCommunityPost()
    {
        return _context.CommunityPost.Select(c => new CommunityPostDTO
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
        });
    }

    // GET: api/CommunityPost/5
    [HttpGet("{communitypostid}")]
    public async Task<CommunityPostDTO> GetCommunityPost(int communitypostid)
    {
        var communitypost = await _context.CommunityPost
            .Where(c => c.CommunityPostId == communitypostid)
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
            .FirstOrDefaultAsync();

        if (communitypost == null)
        {
            return null;
        }

        return communitypost;
    }

    // GET: api/CommunityPost/user/5
    // 依 userId 查詢「某個人自己發的貼文」，UserProfileView.vue 要用這個端點。
    [HttpGet("user/{userid}")]
    public async Task<IEnumerable<CommunityPostDTO>> GetCommunityPostByUser(int userid)
    {
        return await _context.CommunityPost
            .Where(c => c.UserId == userid)
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

    // PUT: api/CommunityPost/5
    [HttpPut("{communitypostid}")]
    public async Task<ResultDTO> PutCommunityPost(int? communitypostid, CommunityPostDTO communitypostDTO)
    {
        if (communitypostid != communitypostDTO.CommunityPostId)
        {
            return new ResultDTO { OK = false, Code = 400 };
        }

        CommunityPost post = await _context.CommunityPost.FindAsync(communitypostDTO.CommunityPostId);
        if (post == null)
        {
            return new ResultDTO { OK = false, Code = 404 };
        }
        else
        {
            // 只更新使用者實際能編輯的欄位（Content、Status、TaggedProducts），
            // User、Images、LikesCount 這些是查詢時組出來的「唯讀資訊」，不能拿來寫回資料庫。
            post.Content = communitypostDTO.Content;
            post.Status = communitypostDTO.Status;
            _context.Entry(post).State = EntityState.Modified;

            // 標記商品用「先刪光、再照 DTO 傳來的清單重新新增」的方式同步，
            // 不用一筆一筆比對誰是新增的、誰是刪除的，跟 POST 那邊新增的寫法保持一致風格。
            var oldTags = _context.PostTaggedProduct.Where(t => t.CommunityPostId == post.CommunityPostId);
            _context.PostTaggedProduct.RemoveRange(oldTags);

            if (communitypostDTO.TaggedProducts != null)
            {
                foreach (var tag in communitypostDTO.TaggedProducts)
                {
                    PostTaggedProduct postTaggedProduct = new PostTaggedProduct
                    {
                        PostTaggedProductId = 0,
                        CommunityPostId = post.CommunityPostId,
                        ProductId = tag.ProductId,
                        ProductRoute = tag.ProductRoute
                    };
                    _context.PostTaggedProduct.Add(postTaggedProduct);
                }
            }

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                return new ResultDTO { OK = false, Code = 500 };
            }
            return new ResultDTO { OK = true, Code = 204 };
        }
    }

    // POST: api/CommunityPost
    [HttpPost]
    public async Task<ResultDTO> PostCommunityPost(CommunityPostDTO communitypostDTO)
    {
        CommunityPost post = new CommunityPost
        {
            CommunityPostId = 0,
            UserId = communitypostDTO.UserId,
            Content = communitypostDTO.Content,
            PostDate = DateTimeOffset.Now,
            Status = communitypostDTO.Status
        };
        _context.CommunityPost.Add(post);
        await _context.SaveChangesAsync();
        // 先存主表，這樣 post.CommunityPostId 才會被 EF 補上剛剛新增的識別碼，
        // 下面圖片、標記商品才知道要掛在哪一篇貼文底下。

        if (communitypostDTO.Images != null)
        {
            foreach (var img in communitypostDTO.Images)
            {
                PostImage postImage = new PostImage
                {
                    PostImageId = 0,
                    CommunityPostId = post.CommunityPostId,
                    ImageFileName = img.ImageFileName,
                    SortOrder = img.SortOrder
                };
                _context.PostImage.Add(postImage);
            }
        }

        if (communitypostDTO.TaggedProducts != null)
        {
            foreach (var tag in communitypostDTO.TaggedProducts)
            {
                PostTaggedProduct postTaggedProduct = new PostTaggedProduct
                {
                    PostTaggedProductId = 0,
                    CommunityPostId = post.CommunityPostId,
                    ProductId = tag.ProductId,
                    ProductRoute = tag.ProductRoute
                };
                _context.PostTaggedProduct.Add(postTaggedProduct);
            }
        }

        await _context.SaveChangesAsync();

        return new ResultDTO { OK = true, Code = 204 };
    }

    // DELETE: api/CommunityPost/5
    [HttpDelete("{communitypostid}")]
    public async Task<ResultDTO> DeleteCommunityPost(int? communitypostid)
    {
        var post = await _context.CommunityPost.FindAsync(communitypostid);
        if (post == null)
        {
            return new ResultDTO { OK = false, Code = 404 };
        }
        try
        {
            _context.CommunityPost.Remove(post);
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return new ResultDTO { OK = false, Code = 500 };
        }
        return new ResultDTO { OK = true, Code = 204 };
    }
}